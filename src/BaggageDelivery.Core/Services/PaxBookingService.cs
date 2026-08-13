using System.Globalization;
using BaggageDelivery.Core.Enums;
using BaggageDelivery.Core.Globalization;
using BaggageDelivery.Core.Http;
using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Serilog;

namespace BaggageDelivery.Core.Services;

internal sealed class PaxBookingService(
    BaggageDeliveryContext db,
    IOptions<DespatchOptions> despatchOptions,
    IMemoryCache cache,
    TimeProvider time,
    IDespatchCalendar calendar) : IPaxBookingService
{
    // Static tenant reference data — the Despatch connection is single-tenant per
    // deployment, so a process-wide cache key is safe. TTL trades a stale window
    // for not re-querying on every pax page load.
    private const string AtlOptionsCacheKey = "pax:atl-options";
    // Per-client: a process-wide key would serve one airline's runs to another.
    private const string EcoRunsCacheKeyPrefix = "pax:eco-runs:";
    private const string BaggageFallbackCacheKey = "pax:baggage-fallback";
    private static readonly TimeSpan ReferenceDataTtl = TimeSpan.FromMinutes(30);

    public async Task<BookingSummary?> GetSummaryAsync(int jobId, CancellationToken ct)
    {
        var job = await db.TucJobs
            .AsNoTracking()
            .Where(j => j.UcjbId == jobId)
            .Select(j => new
            {
                j.DeliverByTime,
                j.DeliverToContact,
                j.DeliverToPhone,
                j.ProofOfDeliveryEmail,
                j.DeliveryAddressLine3,
                j.DeliveryAddressLine4,
                j.DeliveryAddressLine5,
                j.DeliveryAddressLine6,
                j.DeliveryAddressLine7,
                j.DeliveryAddressLine8,
                j.DeliveryLatitude,
                j.DeliveryLongitude,
                ClientName = j.UcjbClient != null ? j.UcjbClient.UcclName : null,
                ClientRefa = j.UcjbClientRefa,
                JobClientCode = j.UcjbClientCode,
                ClientCode = j.UcjbClient != null ? j.UcjbClient.UcclCode : null
            })
            .FirstOrDefaultAsync(ct);

        if (job is null)
        {
            Log.Warning("GetSummary: tucJob {JobId} not found", jobId);
            return null;
        }
        
        var airlineCode = ExtractAirlineFromWorldTracerRef(job.ClientRefa)
            ?? (string.IsNullOrWhiteSpace(job.JobClientCode) ? job.ClientCode : job.JobClientCode);
        
        var atlOptions = await cache.GetOrCreateAsync(AtlOptionsCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ReferenceDataTtl;
            return (IReadOnlyList<AtlOptionDto>)await db.TblJobLeaveNotHomes
                .AsNoTracking()
                .Where(l => l.Category == "All," && l.AllowLeave)
                .OrderByDescending(l => l.Sequence).ThenBy(l => l.Name)
                .Select(l => new AtlOptionDto(l.LeaveNotHomeId, l.Name))
                .ToListAsync(ct);
        }) ?? [];

        var now = time.GetUtcNow().UtcDateTime;
        var etaUtc = TenantLocalToUtc(job.DeliverByTime, despatchOptions.Value.TimeZone);

        var defaultCountry = despatchOptions.Value.Countries is { Length: > 0 } cs ? cs[0] : "NZ";

        return new BookingSummary(
            JobId: jobId,
            Reference: jobId.ToString(),
            AirlineLabel: string.IsNullOrWhiteSpace(job.ClientName) ? "Your Airline" : job.ClientName,
            AirlineCode: string.IsNullOrWhiteSpace(airlineCode) ? null : airlineCode.Trim(),
            PassengerName: job.DeliverToContact ?? string.Empty,
            PassengerPhone: job.DeliverToPhone,
            PassengerEmail: job.ProofOfDeliveryEmail,
            DeliveryAddress: BuildAddressDto(jobId, job.DeliveryAddressLine3, job.DeliveryAddressLine4,
                job.DeliveryAddressLine5, job.DeliveryAddressLine6, job.DeliveryAddressLine7,
                job.DeliveryAddressLine8, job.DeliveryLatitude, job.DeliveryLongitude, defaultCountry),
            EarliestSlotUtc: etaUtc ?? now,
            LatestSlotUtc: etaUtc ?? now.AddDays(2),
            AtlOptions: atlOptions);
    }
    
    private static AddressUpdateDto BuildAddressDto(
        int jobId,
        string? line3, string? line4, string? line5, string? line6, string? line7, string? line8,
        decimal? latitude, decimal? longitude, string defaultCountry)
    {
        var country = ResolveStoredCountry(jobId, line8, defaultCountry);
        var isUs = country == "US";
        return new AddressUpdateDto
        {
            Line1 = CombineStreet(line3, line4),
            Line2 = null,
            Suburb = isUs ? null : line5,
            City = (isUs ? line5 : line6) ?? string.Empty,
            PostCode = line7,
            Country = country,
            Latitude = latitude,
            Longitude = longitude
        };
    }

    // DeliveryAddressLine8 is legacy free text ("New Zealand", "USA", "NZL", ...),
    // but the pax portal round-trips this value straight back into a request that
    // requires a canonical code. Resolve it here so the common case confirms
    // without the passenger touching anything. An unresolvable value yields empty,
    // which the portal surfaces as an empty Country field to fill in — we don't
    // guess a country on the passenger's behalf.
    private static string ResolveStoredCountry(int jobId, string? line8, string defaultCountry)
    {
        if (string.IsNullOrWhiteSpace(line8))
        {
            return CountryCodes.TryToIso2(defaultCountry, out var fallback) ? fallback : string.Empty;
        }

        if (CountryCodes.TryToIso2(line8, out var iso2))
        {
            return iso2;
        }

        Log.Warning("Job {JobId} has an unrecognised DeliveryAddressLine8 {Country}", jobId, line8);
        return string.Empty;
    }

    // Extracts the 2-letter IATA airline code from a WorldTracer file reference
    // (ucjbClientRefa). Format is 3-letter station + 2-letter airline + sequence,
    // e.g. "AKLNZ12345" -> "NZ". Returns null if the value isn't a WT ref (too
    // short, or no two-letter airline at positions 4-5) so the caller falls back
    // to the client-code chain.
    private static string? ExtractAirlineFromWorldTracerRef(string? refa)
    {
        if (string.IsNullOrWhiteSpace(refa))
        {
            return null;
        }

        var trimmed = refa.Trim();
        if (trimmed.Length < 5)
        {
            return null;
        }

        var code = trimmed.Substring(3, 2);
        return char.IsLetter(code[0]) && char.IsLetter(code[1])
            ? code.ToUpperInvariant()
            : null;
    }

    // Joins the street-number (L3) and street-name (L4) halves of a Despatch
    // address into a single line, trimming and skipping empty parts.
    private static string CombineStreet(string? numberPart, string? streetPart)
    {
        var a = (numberPart ?? string.Empty).Trim();
        var b = (streetPart ?? string.Empty).Trim();
        if (a.Length == 0)
        {
            return b;
        }

        return b.Length == 0 ? a : $"{a} {b}";
    }

    private static DateTime? TenantLocalToUtc(DateTime? local, string? timeZoneCode)
    {
        if (local is null || string.IsNullOrWhiteSpace(timeZoneCode))
        {
            return null;
        }

        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneCode);
            var unspecified = DateTime.SpecifyKind(local.Value, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(unspecified, tz);
        }
        catch (TimeZoneNotFoundException)
        {
            Log.Warning("Tenant timezone {TimeZoneCode} not found on this host", timeZoneCode);
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            Log.Warning("Tenant timezone {TimeZoneCode} is invalid", timeZoneCode);
            return null;
        }
    }

    public async Task<IReadOnlyList<BookingTimeSlot>> GetTimeslotsAsync(int jobId,
        DateTime? localDate, CancellationToken ct)
    {
        var client = await db.TucJobs
            .AsNoTracking()
            .Where(j => j.UcjbId == jobId && j.UcjbClient != null)
            .Select(j => new { j.UcjbClient!.UcclId })
            .FirstOrDefaultAsync(ct);

        if (client is null)
        {
            Log.Warning("GetTimeslots: tucJob {JobId} not found or has no client", jobId);
            return [];
        }

        var runs = await LoadClientRunsAsync(client.UcclId, ct);

        var timeZone = despatchOptions.Value.TimeZone;
        var nowUtc = time.GetUtcNow().UtcDateTime;
        var anchor = TenantToday(localDate, timeZone, nowUtc);

        return runs.Count == 0
            ? await BuildFallbackSlotsAsync(client.UcclId, anchor, nowUtc, timeZone, ct)
            : await BuildRunSlotsAsync(runs, client.UcclId, anchor, nowUtc, timeZone, ct);
    }

    // Runs are per-client: tucClient.EconomyRun1..8, mirroring
    // UTL_fncJob_GetNextAvailableEconomyRun_DateTime. An empty list means the
    // client isn't on run-based delivery and the caller should fall back.
    private async Task<IReadOnlyList<TimeOnly>> LoadClientRunsAsync(int clientId,
        CancellationToken ct)
    {
        var cached = await cache.GetOrCreateAsync($"{EcoRunsCacheKeyPrefix}{clientId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ReferenceDataTtl;

            var row = await db.TucClients
                .AsNoTracking()
                .Where(c => c.UcclId == clientId)
                .Select(c => new
                {
                    c.EconomyRuns,
                    Runs = new[]
                    {
                        c.EconomyRun1, c.EconomyRun2, c.EconomyRun3, c.EconomyRun4,
                        c.EconomyRun5, c.EconomyRun6, c.EconomyRun7, c.EconomyRun8
                    }
                })
                .FirstOrDefaultAsync(ct);

            if (row is null || !row.EconomyRuns)
            {
                return (IReadOnlyList<TimeOnly>)[];
            }

            var configured = row.Runs
                .Where(r => r is not null)
                .Select(r => TimeOnly.FromDateTime(r!.Value))
                .ToList();

            var ordered = configured.Distinct().Order().ToList();
            if (!configured.SequenceEqual(ordered))
            {
                Log.Warning(
                    "GetTimeslots: client {ClientId} has EconomyRun columns out of ascending order "
                    + "({Configured}) — offering them sorted", clientId, configured);
            }

            return ordered;
        });

        return cached ?? [];
    }

    private async Task<IReadOnlyList<BookingTimeSlot>> BuildRunSlotsAsync(
        IReadOnlyList<TimeOnly> runs, int clientId, DateTime anchor, DateTime nowUtc,
        string? timeZone, CancellationToken ct)
    {
        var date = anchor;
        // A non-business anchor rolls forward with the whole day available — the
        // SQL resets @TimeBooked to 00:00:00 in the same situation.
        var honourCurrentTime = true;
        if (!await calendar.IsBusinessDayAsync(date, clientId, ct))
        {
            date = await calendar.AddBusinessDaysAsync(1, date, clientId, ct);
            honourCurrentTime = false;
        }

        var available = runs;
        if (honourCurrentTime)
        {
            var remaining = runs
                .Where(r => TenantLocalToUtc(date.Add(r.ToTimeSpan()), timeZone) is { } utc && utc >= nowUtc)
                .ToList();

            if (remaining.Count == 0)
            {
                // Today's runs are spent; the passenger's earliest option is the
                // next business day, in full.
                date = await calendar.AddBusinessDaysAsync(1, date, clientId, ct);
            }
            else
            {
                available = remaining;
            }
        }

        var slots = new List<BookingTimeSlot>(available.Count);
        for (var i = 0; i < available.Count; i++)
        {
            var runUtc = TenantLocalToUtc(date.Add(available[i].ToTimeSpan()), timeZone);
            if (runUtc is null)
            {
                continue;
            }

            slots.Add(new BookingTimeSlot(
                Id: Guid.NewGuid(),
                RunUtc: runUtc.Value,
                Label: FormatSlotLabel(available[i], isLast: i == available.Count - 1),
                FirstAvailable: slots.Count == 0));
        }

        return slots;
    }

    // Mirrors the speed-38 else-branch of
    // NET_stpBaggageJobBooking_OnHoldInsertJobAndChildren: clients without runs
    // fall back to the global BaggageCutOff/BaggageRebook pair.
    private async Task<IReadOnlyList<BookingTimeSlot>> BuildFallbackSlotsAsync(
        int clientId, DateTime anchor, DateTime nowUtc, string? timeZone, CancellationToken ct)
    {
        var setting = await cache.GetOrCreateAsync(BaggageFallbackCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ReferenceDataTtl;
            return await db.TblEcoSettings
                .AsNoTracking()
                .OrderBy(s => s.SettingId)
                .Select(s => new BaggageFallback(s.BaggageCutOff, s.BaggageRebook))
                .FirstOrDefaultAsync(ct);
        });

        if (setting?.Rebook is not { } rebook)
        {
            Log.Warning(
                "GetTimeslots: no client runs and no tblEcoSetting BaggageRebook — returning empty slot list");
            return [];
        }

        var date = anchor;
        if (!await calendar.IsBusinessDayAsync(date, clientId, ct) || setting.CutOff is { } cutOff
            && TenantLocalToUtc(date.Add(cutOff.TimeOfDay), timeZone) is { } cutOffUtc
            && nowUtc > cutOffUtc)
        {
            date = await calendar.AddBusinessDaysAsync(1, date, clientId, ct);
        }

        var runUtc = TenantLocalToUtc(date.Add(rebook.TimeOfDay), timeZone);
        if (runUtc is null)
        {
            return [];
        }

        return
        [
            new BookingTimeSlot(
                Id: Guid.NewGuid(),
                RunUtc: runUtc.Value,
                Label: FormatSlotLabel(TimeOnly.FromDateTime(rebook), isLast: true),
                FirstAvailable: true)
        ];
    }

    private sealed record BaggageFallback(DateTime? CutOff, DateTime? Rebook);

    private static DateTime TenantToday(DateTime? localDate, string? timeZoneCode, DateTime nowUtc)
    {
        if (localDate is { } d)
        {
            return d.Date;
        }

        if (string.IsNullOrWhiteSpace(timeZoneCode))
        {
            return nowUtc.Date;
        }

        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneCode);
            return TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz).Date;
        }
        catch (TimeZoneNotFoundException)
        {
            return nowUtc.Date;
        }
        catch (InvalidTimeZoneException)
        {
            return nowUtc.Date;
        }
    }

    // The final run of the day has no closing edge, so it reads open-ended.
    private static string FormatSlotLabel(TimeOnly run, bool isLast) =>
        isLast
            ? string.Create(CultureInfo.InvariantCulture, $"After {run:h:mm tt}")
            : string.Create(CultureInfo.InvariantCulture, $"{run:h:mm tt}");

    public async Task ConfirmAsync(ConfirmBookingInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        var leaveId = input.AtlOptionId;
        var deliverByLocal = UtcToTenantLocal(input.DeliveryTimeUtc, despatchOptions.Value.TimeZone);

        // Write back using the canonical Despatch DeliveryAddressLine convention
        // (see BuildAddressDto). The pax form captures a single combined street, so
        // it goes in L4 (street name) with L3 (number) cleared — CombineStreet on
        // read reproduces it. L1 (company) / L2 (building) are deliberately left
        // untouched so a pax edit can't clobber them. For US, L5=city; otherwise
        // L5=suburb, L6=city.
        // Normalise before deriving the column layout: a stale client (the booking
        // GET is service-worker cached for 30 minutes) can still post the legacy
        // free-text country we used to emit.
        if (!CountryCodes.TryToIso2(input.Address.Country, out var country))
        {
            throw new PaxAddressValidationException(
                "We couldn't recognise the country on your delivery address. "
                + "Please check it, or use the address search to select your address.");
        }

        var isUs = country == "US";
        var line5 = isUs ? input.Address.City : input.Address.Suburb;
        var line6 = isUs ? null : input.Address.City;

        var rows = await db.TucJobs
            .Where(j => j.UcjbId == input.JobId)
            .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.DeliverToContact, input.PassengerName)
                    .SetProperty(j => j.DeliverToPhone, input.PassengerPhone)
                    .SetProperty(j => j.ProofOfDeliveryEmail, input.PassengerEmail)
                    .SetProperty(j => j.DeliverToLeaveId, leaveId)
                    .SetProperty(j => j.UcjbToSpecial, input.AccessNotes)
                    .SetProperty(j => j.DeliveryAddressLine3, (string?)null)
                    .SetProperty(j => j.DeliveryAddressLine4, input.Address.Line1)
                    .SetProperty(j => j.DeliveryAddressLine5, line5)
                    .SetProperty(j => j.DeliveryAddressLine6, line6)
                    .SetProperty(j => j.DeliveryAddressLine7, input.Address.PostCode)
                    .SetProperty(j => j.DeliveryAddressLine8, country)
                    .SetProperty(j => j.DeliveryLatitude, input.Address.Latitude)
                    .SetProperty(j => j.DeliveryLongitude, input.Address.Longitude)
                    .SetProperty(j => j.DeliverByTime, deliverByLocal)
                    .SetProperty(j => j.UcjbStatus, (int)JobStatus.New),
                ct);

        if (rows == 0)
        {
            throw new InvalidOperationException(
                $"tucJob {input.JobId} not found — pax confirmation not persisted");
        }
        
        try
        {
            await db.JobDeliveryJourneys.AddAsync(new JobDeliveryJourney
            {
                JobId = input.JobId,
                ChangeType = nameof(DeliveryJourneyChangeType.BaggageDeliveryBooking),
                UpdatedAt = time.GetUtcNow().UtcDateTime,
                UpdatedByType = nameof(DeliveryJourneyUpdatedByType.System),
                Comments = "Baggage delivery booking created by passenger via self-service link"
            }, ct);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warning(ex,
                "Pax confirmation JobId={JobId}: failed to record JobDeliveryJourney entry (audit-only, ignored)",
                input.JobId);
        }

        Log.Information("Pax confirmation released: JobId={JobId}", input.JobId);
    }

    private static DateTime? UtcToTenantLocal(DateTime utc, string? timeZoneCode)
    {
        if (string.IsNullOrWhiteSpace(timeZoneCode))
        {
            return null;
        }

        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneCode);
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);
        }
        catch (TimeZoneNotFoundException)
        {
            Log.Warning("Tenant timezone {TimeZoneCode} not found on this host", timeZoneCode);
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            Log.Warning("Tenant timezone {TimeZoneCode} is invalid", timeZoneCode);
            return null;
        }
    }
}