using System.Globalization;
using BaggageDelivery.Core.Enums;
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
    TimeProvider time) : IPaxBookingService
{
    // Static tenant reference data — the Despatch connection is single-tenant per
    // deployment, so a process-wide cache key is safe. TTL trades a stale window
    // for not re-querying on every pax page load.
    private const string AtlOptionsCacheKey = "pax:atl-options";
    private const string EcoRunsCacheKey = "pax:eco-runs";
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
                // Branding inputs — resolved in memory below. The airline code is
                // extracted from the WorldTracer file reference in ucjbClientRefa
                // (station+airline+sequence); the denormalised job code and the
                // client's own code are kept only as fallbacks.
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
                .Where(l => l.Category == "All,")
                .OrderBy(l => l.Sequence).ThenBy(l => l.Name)
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
            DeliveryAddress: BuildAddressDto(job.DeliveryAddressLine3, job.DeliveryAddressLine4,
                job.DeliveryAddressLine5, job.DeliveryAddressLine6, job.DeliveryAddressLine7,
                job.DeliveryAddressLine8, job.DeliveryLatitude, job.DeliveryLongitude, defaultCountry),
            EarliestSlotUtc: etaUtc ?? now,
            LatestSlotUtc: etaUtc ?? now.AddDays(2),
            AtlOptions: atlOptions);
    }
    
    private static AddressUpdateDto BuildAddressDto(
        string? line3, string? line4, string? line5, string? line6, string? line7, string? line8,
        decimal? latitude, decimal? longitude, string defaultCountry)
    {
        var isUs = IsUsCountry(line8);
        return new AddressUpdateDto
        {
            Line1 = CombineStreet(line3, line4),
            Line2 = null,
            Suburb = isUs ? null : line5,
            City = (isUs ? line5 : line6) ?? string.Empty,
            PostCode = line7,
            Country = string.IsNullOrWhiteSpace(line8) ? defaultCountry : line8,
            Latitude = latitude,
            Longitude = longitude
        };
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

    private static bool IsUsCountry(string? country)
    {
        var c = (country ?? string.Empty).Trim();
        return c.Equals("US", StringComparison.OrdinalIgnoreCase)
            || c.Equals("USA", StringComparison.OrdinalIgnoreCase)
            || c.Equals("United States", StringComparison.OrdinalIgnoreCase);
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

    public async Task<IReadOnlyList<BookingTimeSlot>> GetTimeslotsAsync(int jobId, DateTime? localDate,
        CancellationToken ct)
    {
        var runs = await cache.GetOrCreateAsync(EcoRunsCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ReferenceDataTtl;
            var setting = await db.TblEcoSettings
                .AsNoTracking()
                .OrderBy(s => s.SettingId)
                .Select(s => new[]
                {
                    s.EconomyRun1,
                    s.EconomyRun2,
                    s.EconomyRun3,
                    s.EconomyRun4,
                    s.EconomyRun5
                })
                .FirstOrDefaultAsync(ct);
            // Empty array (not null) is a valid cached "no config" result.
            return setting ?? [];
        }) ?? [];

        if (runs.Length == 0)
        {
            Log.Warning("GetTimeslots: tblEcoSetting has no rows — returning empty slot list");
            return [];
        }

        var timeZone = despatchOptions.Value.TimeZone;
        var now = time.GetUtcNow().UtcDateTime;
        var anchor = TenantToday(localDate, timeZone, now);

        var slots = new List<BookingTimeSlot>(capacity: 4);
        var firstAvailableAssigned = false;
        for (var i = 0; i < runs.Length - 1; i++)
        {
            var start = runs[i];
            var end = runs[i + 1];
            if (start is null || end is null)
            {
                continue;
            }

            var startUtc = TenantLocalToUtc(anchor.Add(start.Value.TimeOfDay), timeZone);
            var endUtc = TenantLocalToUtc(anchor.Add(end.Value.TimeOfDay), timeZone);
            if (startUtc is null || endUtc is null)
            {
                continue;
            }

            var firstAvailable = !firstAvailableAssigned && endUtc > now;
            if (firstAvailable)
            {
                firstAvailableAssigned = true;
            }

            slots.Add(new BookingTimeSlot(
                Id: Guid.NewGuid(),
                StartUtc: startUtc.Value,
                EndUtc: endUtc.Value,
                Label: FormatSlotLabel(start.Value, end.Value),
                FirstAvailable: firstAvailable));
        }

        return slots;
    }

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

    private static string FormatSlotLabel(DateTime start, DateTime end) =>
        string.Create(CultureInfo.InvariantCulture, $"{start:h:mm tt} - {end:h:mm tt}");

    public async Task ConfirmAsync(ConfirmBookingInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        var leaveId = input.AtlOptionId;
        var deliverByLocal = UtcToTenantLocal(input.TimeSlotEndUtc, despatchOptions.Value.TimeZone);

        // Write back using the canonical Despatch DeliveryAddressLine convention
        // (see BuildAddressDto). The pax form captures a single combined street, so
        // it goes in L4 (street name) with L3 (number) cleared — CombineStreet on
        // read reproduces it. L1 (company) / L2 (building) are deliberately left
        // untouched so a pax edit can't clobber them. For US, L5=city; otherwise
        // L5=suburb, L6=city.
        var isUs = IsUsCountry(input.Address.Country);
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
                    .SetProperty(j => j.DeliveryAddressLine8, input.Address.Country)
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

        // Audit-only: record the pax-confirm on JobDeliveryJourney so the dispatcher's
        // timeline shows the booking was created via the passenger self-service link.
        // ChangeType / UpdatedByType are persisted-string contracts shared with
        // DespatchWeb.Enums — keep the strings in sync if those enums are renamed.
        // Best-effort: a journey-write failure must not undo the tucJob update.
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