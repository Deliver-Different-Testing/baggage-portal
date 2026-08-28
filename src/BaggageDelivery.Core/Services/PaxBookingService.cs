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
    // Per-client and per-date: the roll-forward answer depends on the client's site
    // calendar, and a holiday-driven roll must not survive into the next day.
    private const string NextBusinessDayCacheKeyPrefix = "pax:next-business-day:";
    private static readonly TimeSpan ReferenceDataTtl = TimeSpan.FromMinutes(30);

    // Steve's ask: enough options that tomorrow is always visible, not just what's
    // left of today.
    private const int TargetSlotCount = 8;
    // A client with one run a day needs eight days to fill the list; the cap only
    // exists so a calendar that never returns a business day can't spin.
    private const int MaxDaysWalked = 14;
    // Same default despatchweb uses when a speed has no duration
    // (Repositories/JobRepository.cs:4091).
    private const int DefaultWindowMinutes = 180;

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
                j.DeliveryAddressLine2,
                j.DeliveryAddressLine3,
                j.DeliveryAddressLine4,
                j.DeliveryAddressLine5,
                j.DeliveryAddressLine6,
                j.DeliveryAddressLine7,
                j.DeliveryAddressLine8,
                j.DeliveryLatitude,
                j.DeliveryLongitude,
                ClientName = j.UcjbClient != null ? j.UcjbClient.UcclName : null,
                ClientPhone = j.UcjbClient != null ? j.UcjbClient.UcclPhone : null,
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
        
        // Excluded by LeaveNotHomeId, not by name: the rows are shared with
        // despatchweb and a tenant rewording one must not be able to put it back
        // in front of a passenger.
        var excludedAtlIds = despatchOptions.Value.ExcludedAtlOptions
            .Select(o => (int)o)
            .ToArray();

        var atlOptions = await cache.GetOrCreateAsync(AtlOptionsCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ReferenceDataTtl;
            return (IReadOnlyList<AtlOptionDto>)await db.TblJobLeaveNotHomes
                .AsNoTracking()
                .Where(l => l.Category == "All," && l.AllowLeave
                    && !((IEnumerable<int>)excludedAtlIds).Contains(l.LeaveNotHomeId))
                .OrderByDescending(l => l.Sequence).ThenBy(l => l.Name)
                .Select(l => new AtlOptionDto(l.LeaveNotHomeId, l.Name))
                .ToListAsync(ct);
        }) ?? [];

        var defaultAtlOptionId = atlOptions
            .FirstOrDefault(o => o.Id == (int)despatchOptions.Value.DefaultAtlOption)?.Id
            ?? atlOptions.FirstOrDefault()?.Id;

        var now = time.GetUtcNow().UtcDateTime;
        var etaUtc = TenantLocalToUtc(job.DeliverByTime, despatchOptions.Value.TimeZone);

        var defaultCountry = despatchOptions.Value.Countries is { Length: > 0 } cs ? cs[0] : "NZ";

        return new BookingSummary(
            JobId: jobId,
            // The WorldTracer file reference, shown to the passenger as the File
            // Reference. Empty when the job doesn't carry one — the portal hides the
            // tag rather than showing the internal id behind the link.
            FileReference: (job.ClientRefa ?? string.Empty).Trim(),
            AirlineLabel: string.IsNullOrWhiteSpace(job.ClientName) ? "Your Airline" : job.ClientName,
            // The airline's own line first — it's the one the passenger's baggage
            // file is with. The tenant's number is the backstop for clients that
            // carry no phone on tucClient.
            SupportPhone: FirstNonBlank(job.ClientPhone, despatchOptions.Value.SupportPhone),
            AirlineCode: string.IsNullOrWhiteSpace(airlineCode) ? null : airlineCode.Trim(),
            PassengerName: job.DeliverToContact ?? string.Empty,
            PassengerPhone: job.DeliverToPhone,
            PassengerEmail: job.ProofOfDeliveryEmail,
            DeliveryAddress: BuildAddressDto(jobId, job.DeliveryAddressLine2, job.DeliveryAddressLine3,
                job.DeliveryAddressLine4, job.DeliveryAddressLine5, job.DeliveryAddressLine6,
                job.DeliveryAddressLine7, job.DeliveryAddressLine8, job.DeliveryLatitude,
                job.DeliveryLongitude, defaultCountry),
            EarliestSlotUtc: etaUtc ?? now,
            LatestSlotUtc: etaUtc ?? now.AddDays(2),
            AtlOptions: atlOptions,
            DefaultAtlOptionId: defaultAtlOptionId);
    }
    
    public Task<string?> GetJobNumberAsync(int jobId, CancellationToken ct) =>
        db.TucJobs
            .AsNoTracking()
            .Where(j => j.UcjbId == jobId)
            .Select(j => j.UcjbNumber)
            .FirstOrDefaultAsync(ct);

    private static string FirstNonBlank(string? preferred, string? fallback) =>
        !string.IsNullOrWhiteSpace(preferred) ? preferred.Trim()
        : !string.IsNullOrWhiteSpace(fallback) ? fallback.Trim()
        : string.Empty;

    private static AddressUpdateDto BuildAddressDto(
        int jobId,
        string? line2, string? line3, string? line4, string? line5, string? line6, string? line7,
        string? line8, decimal? latitude, decimal? longitude, string defaultCountry)
    {
        var country = ResolveStoredCountry(jobId, line8, defaultCountry);
        var isUs = country == "US";
        return new AddressUpdateDto
        {
            Line1 = CombineStreet(line3, line4),
            Line2 = line2,
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

    // Resolves the tenant zone once per request — the slot loop converts up to
    // sixteen wall-clock times and FindSystemTimeZoneById is not free.
    private static TimeZoneInfo? ResolveTimeZone(string? timeZoneCode)
    {
        if (string.IsNullOrWhiteSpace(timeZoneCode))
        {
            return null;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneCode);
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

    // Despatch stores wall-clock time with no offset. Null when the zone is
    // unresolvable, or when the local time doesn't exist because it falls in the
    // DST spring-forward gap — ConvertTimeToUtc throws on those, and one unusable
    // run must not take the whole timeslot list down with it.
    private static DateTime? LocalToUtc(DateTime local, TimeZoneInfo? timeZone)
    {
        if (timeZone is null)
        {
            return null;
        }

        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (!timeZone.IsInvalidTime(unspecified))
        {
            return TimeZoneInfo.ConvertTimeToUtc(unspecified, timeZone);
        }

        Log.Warning("Local time {Local} does not exist in {TimeZone} — skipping that window",
            unspecified, timeZone.Id);
        return null;

    }

    private static DateTime? TenantLocalToUtc(DateTime? local, string? timeZoneCode) =>
        local is { } value ? LocalToUtc(value, ResolveTimeZone(timeZoneCode)) : null;

    public async Task<IReadOnlyList<BookingTimeSlot>> GetTimeslotsAsync(int jobId,
        DateTime? localDate, CancellationToken ct)
    {
        var job = await db.TucJobs
            .AsNoTracking()
            .Where(j => j.UcjbId == jobId && j.UcjbClient != null)
            .Select(j => new
            {
                j.UcjbClient!.UcclId,
                // ucjbSpeed is an FK to tucJobType.ucjtID, but no navigation is
                // mapped here: legacy rows can point at a retired speed, and a
                // correlated subquery yields null for those instead of dropping
                // the job from the result.
                WindowMinutes = db.TucJobTypes
                    .Where(t => t.UcjtId == j.UcjbSpeed)
                    .Select(t => t.Minutes)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        if (job is null)
        {
            Log.Warning("GetTimeslots: tucJob {JobId} not found or has no client", jobId);
            return [];
        }

        var timeZone = ResolveTimeZone(despatchOptions.Value.TimeZone);
        if (timeZone is null)
        {
            return [];
        }

        var runs = await LoadClientRunsAsync(job.UcclId, ct);

        var nowUtc = time.GetUtcNow().UtcDateTime;
        var today = TenantToday(nowUtc, timeZone);
        // `?date=` means "the next windows from this date", so an anchor in the
        // past would offer windows that have already been run.
        var anchor = localDate is { } supplied && supplied.Date > today ? supplied.Date : today;
        var window = TimeSpan.FromMinutes(
            job.WindowMinutes is > 0 ? job.WindowMinutes.Value : DefaultWindowMinutes);

        return runs.Count == 0
            ? await BuildFallbackSlotsAsync(job.UcclId, anchor, today, nowUtc, timeZone, window, ct)
            : await BuildRunSlotsAsync(runs, job.UcclId, anchor, today, nowUtc, timeZone, window, ct);
    }

    // Runs are per-client: tucClient.EconomyRun1..8, mirroring
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

    // Walks forward from the anchor, flattening (business day × run) in
    // chronological order until there are TargetSlotCount windows. The two cases
    // the single-day version handled specially fall out for free: a non-business
    // anchor is skipped before the walk starts, and a day whose runs have all been
    // filtered out simply contributes nothing.
    private async Task<IReadOnlyList<BookingTimeSlot>> BuildRunSlotsAsync(
        IReadOnlyList<TimeOnly> runs, int clientId, DateTime anchor, DateTime today,
        DateTime nowUtc, TimeZoneInfo? timeZone, TimeSpan window, CancellationToken ct)
    {
        // Ceiling division: how many days this many runs need to fill the list,
        // plus one for an anchor that is a holiday or already part-spent.
        await PrimeBusinessDayChainAsync(clientId, anchor,
            Math.Min(MaxDaysWalked, (TargetSlotCount + runs.Count - 1) / runs.Count + 1), ct);

        var date = anchor;
        var honourCurrentTime = true;
        if (!await calendar.IsBusinessDayAsync(date, clientId, ct))
        {
            date = await NextBusinessDayAsync(clientId, date, ct);
            honourCurrentTime = false;
        }

        var slots = new List<BookingTimeSlot>(TargetSlotCount);
        for (var day = 0; day < MaxDaysWalked && slots.Count < TargetSlotCount; day++)
        {
            foreach (var run in runs)
            {
                if (slots.Count == TargetSlotCount)
                {
                    break;
                }

                AddSlot(slots, date, run, today, nowUtc, timeZone, window, honourCurrentTime);
            }

            if (slots.Count >= TargetSlotCount)
            {
                continue;
            }

            date = await NextBusinessDayAsync(clientId, date, ct);
            honourCurrentTime = false;
        }

        WarnIfShort(slots.Count, clientId);
        return slots;
    }

    // Mirrors the speed-38 else-branch of
    // NET_stpBaggageJobBooking_OnHoldInsertJobAndChildren: clients without runs
    // fall back to the global BaggageCutOff/BaggageRebook pair.
    private async Task<IReadOnlyList<BookingTimeSlot>> BuildFallbackSlotsAsync(
        int clientId, DateTime anchor, DateTime today, DateTime nowUtc, TimeZoneInfo? timeZone,
        TimeSpan window, CancellationToken ct)
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

        // One window a day on this path, so the walk needs a day per slot.
        await PrimeBusinessDayChainAsync(clientId, anchor,
            Math.Min(MaxDaysWalked, TargetSlotCount + 1), ct);

        var date = anchor;
        if (!await calendar.IsBusinessDayAsync(date, clientId, ct) || setting.CutOff is { } cutOff
            && LocalToUtc(date.Add(cutOff.TimeOfDay), timeZone) is { } cutOffUtc
            && nowUtc > cutOffUtc)
        {
            date = await NextBusinessDayAsync(clientId, date, ct);
        }

        var run = TimeOnly.FromDateTime(rebook);
        var slots = new List<BookingTimeSlot>(TargetSlotCount);
        for (var day = 0; day < MaxDaysWalked && slots.Count < TargetSlotCount; day++)
        {
            // honourCurrentTime is false throughout: the cutoff above is the only
            // "is today still bookable" test on this path, same as the stored proc.
            // Comparing the rebook time against the clock as well would drop today
            // for a passenger arriving after it, which the proc doesn't do.
            AddSlot(slots, date, run, today, nowUtc, timeZone, window, honourCurrentTime: false);

            if (slots.Count < TargetSlotCount)
            {
                date = await NextBusinessDayAsync(clientId, date, ct);
            }
        }

        WarnIfShort(slots.Count, clientId);
        return slots;
    }

    private sealed record BaggageFallback(DateTime? CutOff, DateTime? Rebook);

    private static void AddSlot(List<BookingTimeSlot> slots, DateTime date, TimeOnly run,
        DateTime today, DateTime nowUtc, TimeZoneInfo? timeZone, TimeSpan window,
        bool honourCurrentTime)
    {
        var localStart = date.Add(run.ToTimeSpan());
        if (LocalToUtc(localStart, timeZone) is not { } runUtc)
        {
            return;
        }

        if (honourCurrentTime && runUtc < nowUtc)
        {
            return;
        }

        slots.Add(new BookingTimeSlot(
            Id: Guid.NewGuid(),
            RunUtc: runUtc,
            DayLabel: FormatDayLabel(date, today),
            // The end is derived in local time, not from runUtc, so a window
            // straddling a DST change still reads as the promise the passenger was
            // given rather than shifting by an hour.
            Label: FormatWindowLabel(localStart, localStart.Add(window)),
            FirstAvailable: slots.Count == 0));
    }

    // The answer is identical for every passenger of this client on this date, and
    // holidays don't move intraday — without this a client with one run a day costs
    // one UTL_AddBusinessDays round trip per window offered.
    private async Task<DateTime> NextBusinessDayAsync(int clientId, DateTime date,
        CancellationToken ct)
    {
        var key = BusinessDayKey(clientId, date);

        if (cache.TryGetValue(key, out DateTime cached))
        {
            return cached;
        }

        var next = await calendar.AddBusinessDaysAsync(1, date, clientId, ct);
        cache.Set(key, next, ReferenceDataTtl);
        return next;
    }

    private static string BusinessDayKey(int clientId, DateTime date) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{NextBusinessDayCacheKeyPrefix}{clientId}:{date:yyyy-MM-dd}");

    // The walks below advance one business day at a time, and each hop that misses
    // the cache is its own round trip — a client with one run a day pays eight of
    // them to render one page. Resolve the whole chain in a single call up front
    // and seed the keys the walk reads, so the loops themselves stay unchanged and
    // simply hit cache. Anything the walk needs beyond `count` still falls through
    // to a live hop, so this only ever changes the round-trip count.
    private async Task PrimeBusinessDayChainAsync(int clientId, DateTime anchor, int count,
        CancellationToken ct)
    {
        // The chain is seeded in one pass under one TTL, so a hit on its first link
        // means the rest is there too.
        if (cache.TryGetValue(BusinessDayKey(clientId, anchor), out DateTime _))
        {
            return;
        }

        var previous = anchor.Date;
        foreach (var day in await calendar.NextBusinessDaysAsync(count, previous, clientId, ct))
        {
            cache.Set(BusinessDayKey(clientId, previous), day, ReferenceDataTtl);
            previous = day;
        }
    }

    private static void WarnIfShort(int count, int clientId)
    {
        if (count < TargetSlotCount)
        {
            Log.Warning(
                "GetTimeslots: only {Count} of {Target} windows resolved for client {ClientId}",
                count, TargetSlotCount, clientId);
        }
    }

    private static DateTime TenantToday(DateTime nowUtc, TimeZoneInfo? timeZone) =>
        timeZone is null ? nowUtc.Date : TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone).Date;

    // Without the date the passenger can't tell whether an option is today or next
    // week; without Today/Tomorrow they have to read a date to work out the
    // obvious cases.
    private static string FormatDayLabel(DateTime date, DateTime today)
    {
        var prefix = date == today
            ? "Today, "
            : date == today.AddDays(1)
                ? "Tomorrow, "
                : string.Empty;

        return string.Create(CultureInfo.InvariantCulture, $"{prefix}{date:ddd d MMM}");
    }

    private static string FormatWindowLabel(DateTime start, DateTime end) =>
        string.Create(CultureInfo.InvariantCulture, $"{start:h:mm tt} – {end:h:mm tt}");

    public async Task ConfirmAsync(ConfirmBookingInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        var leaveId = input.AtlOptionId;
        var deliverByLocal = UtcToTenantLocal(input.DeliveryTimeUtc, despatchOptions.Value.TimeZone);

        // Write back using the canonical Despatch DeliveryAddressLine convention
        // (see BuildAddressDto). The pax form captures a single combined street, so
        // it goes in L4 (street name) with L3 (number) cleared — CombineStreet on
        // read reproduces it. L2 (building) is the "Extra delivery information"
        // field, so it round-trips; L1 (company) is deliberately left untouched
        // because the passenger is never shown it. For US, L5=city; otherwise
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
                    .SetProperty(j => j.DeliveryAddressLine2, input.Address.Line2)
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