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
    IDespatchCalendar calendar,
    ISuburbResolver suburbs) : IPaxBookingService
{
    private const string AtlOptionsCacheKey = "pax:atl-options";
    private const string EcoRunsCacheKeyPrefix = "pax:eco-runs:";
    private const string BaggageFallbackCacheKey = "pax:baggage-fallback";
    private const string NextBusinessDayCacheKeyPrefix = "pax:next-business-day:";
    private static readonly TimeSpan ReferenceDataTtl = TimeSpan.FromMinutes(30);

    private const int TargetSlotCount = 8;
    private const int MaxDaysWalked = 14;
    private const int DefaultWindowMinutes = 180;

    private const string BookingCreatedComment =
        "Baggage delivery booking created by passenger via self-service link";
    private const string DeliveryAddressFieldName = "ucjbToAddr";
    private const int MaxJourneyCommentLength = 500;

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
                j.DeliveryAddressLine1,
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
                ClientSmsName = j.UcjbClient != null ? j.UcjbClient.Smsname : null,
                ClientRefa = j.UcjbClientRefa,
                JobClientCode = j.UcjbClientCode,
                ClientCode = j.UcjbClient != null ? j.UcjbClient.UcclCode : null,
                JobNumber = j.UcjbNumber,
                CourierId = j.UcjbCourierId,
                Status = j.UcjbStatus,
                AtlOptionId = j.DeliverToLeaveId,
                AccessNotes = j.UcjbToSpecial,
                WindowMinutes = db.TucJobTypes
                    .Where(t => t.UcjtId == j.UcjbSpeed)
                    .Select(t => t.Minutes)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        if (job is null)
        {
            Log.Warning("GetSummary: tucJob {JobId} not found", jobId);
            return null;
        }
        
        var airlineCode = ExtractAirlineFromWorldTracerRef(job.ClientRefa)
            ?? (string.IsNullOrWhiteSpace(job.JobClientCode) ? job.ClientCode : job.JobClientCode);
        
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

        var timeZone = ResolveTimeZone(despatchOptions.Value.TimeZone);
        var confirmation = await BuildConfirmationAsync(
            jobId, job.DeliverByTime, etaUtc, job.AtlOptionId, job.AccessNotes,
            job.WindowMinutes, now, timeZone, ct);

        return new BookingSummary(
            JobId: jobId,
            JobNumber: (job.JobNumber ?? string.Empty).Trim(),
            FileReference: (job.ClientRefa ?? string.Empty).Trim(),
            AirlineLabel: AirlineLabelOf(job.ClientName),
            AirlineSmsName: AirlineSmsNameOf(job.ClientSmsName, job.ClientName),
            SupportPhone: (despatchOptions.Value.SupportPhone).Trim(),
            AirlineCode: string.IsNullOrWhiteSpace(airlineCode) ? null : airlineCode.Trim(),
            PassengerName: job.DeliverToContact ?? string.Empty,
            PassengerPhone: job.DeliverToPhone,
            PassengerEmail: job.ProofOfDeliveryEmail,
            DeliveryAddress: BuildAddressDto(jobId, job.DeliveryAddressLine1,
                job.DeliveryAddressLine2, job.DeliveryAddressLine3, job.DeliveryAddressLine4,
                job.DeliveryAddressLine5, job.DeliveryAddressLine6, job.DeliveryAddressLine7,
                job.DeliveryAddressLine8, job.DeliveryLatitude, job.DeliveryLongitude,
                defaultCountry),
            EarliestSlotUtc: etaUtc ?? now,
            LatestSlotUtc: etaUtc ?? now.AddDays(2),
            AtlOptions: atlOptions,
            DefaultAtlOptionId: defaultAtlOptionId,
            TrackingAvailable: job.CourierId > 0
                && job.Status >= (int)JobStatus.Dispatched
                && job.Status != (int)JobStatus.Void,
            Confirmation: confirmation);
    }

    private async Task<BookingConfirmation?> BuildConfirmationAsync(
        int jobId, DateTime? deliverByLocal, DateTime? deliverByUtc, int? atlOptionId,
        string? accessNotes, int? windowMinutes, DateTime nowUtc, TimeZoneInfo? timeZone,
        CancellationToken ct)
    {
        var confirmedAt = await FindConfirmedAtAsync(jobId, ct);
        if (confirmedAt is not { } stamp)
        {
            return null;
        }

        var window = TimeSpan.FromMinutes(
            windowMinutes is > 0 ? windowMinutes.Value : DefaultWindowMinutes);

        var dayLabel = string.Empty;
        var windowLabel = string.Empty;
        if (deliverByLocal is not { } localStart)
        {
            return new BookingConfirmation(
                ConfirmedAtUtc: stamp,
                DeliveryTimeUtc: deliverByUtc,
                DayLabel: dayLabel,
                WindowLabel: windowLabel,
                AtlOptionId: atlOptionId,
                AccessNotes: accessNotes);
        }

        dayLabel = FormatDayLabel(localStart.Date, TenantToday(nowUtc, timeZone));
        windowLabel = FormatWindowLabel(localStart, localStart.Add(window));

        return new BookingConfirmation(
            ConfirmedAtUtc: stamp,
            DeliveryTimeUtc: deliverByUtc,
            DayLabel: dayLabel,
            WindowLabel: windowLabel,
            AtlOptionId: atlOptionId,
            AccessNotes: accessNotes);
    }

    private Task<DateTime?> FindConfirmedAtAsync(int jobId, CancellationToken ct) =>
        db.JobDeliveryJourneys
            .AsNoTracking()
            .Where(j => j.JobId == jobId
                && j.ChangeType == nameof(DeliveryJourneyChangeType.BaggageDeliveryBooking))
            .OrderByDescending(j => j.UpdatedAt)
            .Select(j => (DateTime?)j.UpdatedAt)
            .FirstOrDefaultAsync(ct);
    
    public async Task<BookingNotificationDetails?> GetNotificationDetailsAsync(int jobId, CancellationToken ct)
    {
        var job = await db.TucJobs
            .AsNoTracking()
            .Where(j => j.UcjbId == jobId)
            .Select(j => new
            {
                ClientRefa = j.UcjbClientRefa,
                ClientName = j.UcjbClient != null ? j.UcjbClient.UcclName : null,
                ClientSmsName = j.UcjbClient != null ? j.UcjbClient.Smsname : null
            })
            .FirstOrDefaultAsync(ct);

        return job is null
            ? null
            : new BookingNotificationDetails(
                (job.ClientRefa ?? string.Empty).Trim(),
                AirlineLabelOf(job.ClientName),
                AirlineSmsNameOf(job.ClientSmsName, job.ClientName));
    }

    private static string AirlineLabelOf(string? clientName) =>
        string.IsNullOrWhiteSpace(clientName) ? BookingNotificationDetails.UnknownAirline : clientName;

    private static string AirlineSmsNameOf(string? clientSmsName, string? clientName) =>
        string.IsNullOrWhiteSpace(clientSmsName) ? AirlineLabelOf(clientName) : clientSmsName.Trim();

    private static AddressUpdateDto BuildAddressDto(
        int jobId,
        string? line1, string? line2, string? line3, string? line4, string? line5, string? line6,
        string? line7, string? line8, decimal? latitude, decimal? longitude, string defaultCountry)
    {
        return new AddressUpdateDto
        {
            Line1 = line1,
            Line2 = line2,
            Line3 = line3,
            Line4 = line4 ?? string.Empty,
            Line5 = line5 ?? string.Empty,
            Line6 = line6 ?? string.Empty,
            Line7 = line7,
            Country = ResolveStoredCountry(jobId, line8, defaultCountry),
            Latitude = latitude,
            Longitude = longitude
        };
    }

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
        var anchor = localDate is { } supplied && supplied.Date > today ? supplied.Date : today;
        var window = TimeSpan.FromMinutes(
            job.WindowMinutes is > 0 ? job.WindowMinutes.Value : DefaultWindowMinutes);

        return runs.Count == 0
            ? await BuildFallbackSlotsAsync(job.UcclId, anchor, today, nowUtc, timeZone, window, ct)
            : await BuildRunSlotsAsync(runs, job.UcclId, anchor, today, nowUtc, timeZone, window, ct);
    }

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
        IReadOnlyList<TimeOnly> runs, int clientId, DateTime anchor, DateTime today,
        DateTime nowUtc, TimeZoneInfo? timeZone, TimeSpan window, CancellationToken ct)
    {
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
            Label: FormatWindowLabel(localStart, localStart.Add(window)),
            FirstAvailable: slots.Count == 0));
    }
    
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
    
    private async Task PrimeBusinessDayChainAsync(int clientId, DateTime anchor, int count,
        CancellationToken ct)
    {
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

        if (await FindConfirmedAtAsync(input.JobId, ct) is not null)
        {
            Log.Information("Pax confirmation refused, already confirmed: JobId={JobId}", input.JobId);
            throw new PaxAlreadyConfirmedException(input.JobId);
        }

        var leaveId = input.AtlOptionId;
        var startLocal = UtcToTenantLocal(input.DeliveryTimeUtc, despatchOptions.Value.TimeZone);
        var startDateLocal = startLocal?.Date;

        if (!CountryCodes.TryToIso2(input.Address.Country, out var country))
        {
            throw new PaxAddressValidationException(
                "We couldn't recognise the country on your delivery address. "
                + "Please check it, or use the address search to select your address.");
        }

        var suburbId = CountryCodes.IsUnitedStates(country)
            ? null
            : await suburbs.ResolveAsync(input.Address.Line5, input.Address.Line7, ct);

        var before = await db.TucJobs
            .Where(j => j.UcjbId == input.JobId)
            .Select(j => new DeliveryAddressSnapshot(
                j.DeliveryAddressLine1, j.DeliveryAddressLine2, j.DeliveryAddressLine3,
                j.DeliveryAddressLine4, j.DeliveryAddressLine5, j.DeliveryAddressLine6,
                j.DeliveryAddressLine7, j.DeliveryAddressLine8, j.UcjbToAddr))
            .FirstOrDefaultAsync(ct);

        var newAddress = DespatchAddressComposer.Compose(input.Address, country);

        var rows = await db.TucJobs
            .Where(j => j.UcjbId == input.JobId)
            .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.DeliverToContact, input.PassengerName)
                    .SetProperty(j => j.DeliverToPhone, input.PassengerPhone)
                    .SetProperty(j => j.ProofOfDeliveryEmail, input.PassengerEmail)
                    .SetProperty(j => j.DeliverToLeaveId, leaveId)
                    .SetProperty(j => j.UcjbToSpecial, input.AccessNotes)
                    .SetProperty(j => j.DeliveryAddressLine1, input.Address.Line1)
                    .SetProperty(j => j.DeliveryAddressLine2, input.Address.Line2)
                    .SetProperty(j => j.DeliveryAddressLine3, input.Address.Line3)
                    .SetProperty(j => j.DeliveryAddressLine4, input.Address.Line4)
                    .SetProperty(j => j.DeliveryAddressLine5, input.Address.Line5)
                    .SetProperty(j => j.DeliveryAddressLine6, input.Address.Line6)
                    .SetProperty(j => j.DeliveryAddressLine7, input.Address.Line7)
                    .SetProperty(j => j.DeliveryAddressLine8, country)
                    .SetProperty(j => j.UcjbToAddr, newAddress)
                    .SetProperty(j => j.UcjbTo, j => suburbId ?? j.UcjbTo)
                    .SetProperty(j => j.DeliveryLatitude, input.Address.Latitude)
                    .SetProperty(j => j.DeliveryLongitude, input.Address.Longitude)
                    .SetProperty(j => j.DeliverByTime, startLocal)
                    .SetProperty(j => j.UcjbDate, j => startDateLocal ?? j.UcjbDate)
                    .SetProperty(j => j.UcjbTime, j => startLocal ?? j.UcjbTime)
                    .SetProperty(j => j.UcjbStatus, (int)JobStatus.New),
                ct);

        if (rows == 0)
        {
            throw new InvalidOperationException(
                $"tucJob {input.JobId} not found — pax confirmation not persisted");
        }
        
        try
        {
            var updatedAt = time.GetUtcNow().UtcDateTime;

            var journey = new JobDeliveryJourney
            {
                JobId = input.JobId,
                ChangeType = nameof(DeliveryJourneyChangeType.BaggageDeliveryBooking),
                UpdatedAt = updatedAt,
                UpdatedByType = nameof(DeliveryJourneyUpdatedByType.System),
                Comments = BookingCreatedComment
            };

            if (before is not null && HasAddressChanged(before, input.Address, country))
            {
                var oldAddress = DespatchAddressComposer.Compose(
                    before.Line1, before.Line2, before.Line3, before.Line4,
                    before.Line5, before.Line6, before.Line7, before.Line8);

                journey.FieldName = DeliveryAddressFieldName;
                journey.OldValue = string.IsNullOrWhiteSpace(oldAddress) ? before.Flat : oldAddress;
                journey.NewValue = newAddress;
                journey.Comments = Truncate(
                    $"{BookingCreatedComment}. Delivery address changed by the passenger on "
                    + $"{FormatAuditStamp(updatedAt)}.",
                    MaxJourneyCommentLength);
            }

            await db.JobDeliveryJourneys.AddAsync(journey, ct);
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

    private sealed record DeliveryAddressSnapshot(
        string? Line1, string? Line2, string? Line3, string? Line4,
        string? Line5, string? Line6, string? Line7, string? Line8, string? Flat);

    private static bool HasAddressChanged(
        DeliveryAddressSnapshot before, AddressUpdateDto after, string country) =>
        !SameAddressLine(before.Line1, after.Line1)
        || !SameAddressLine(before.Line2, after.Line2)
        || !SameAddressLine(before.Line3, after.Line3)
        || !SameAddressLine(before.Line4, after.Line4)
        || !SameAddressLine(before.Line5, after.Line5)
        || !SameAddressLine(before.Line6, after.Line6)
        || !SameAddressLine(before.Line7, after.Line7)
        || !SameAddressLine(before.Line8, country);

    private static bool SameAddressLine(string? left, string? right) =>
        string.Equals(
            (left ?? string.Empty).Trim(),
            (right ?? string.Empty).Trim(),
            StringComparison.OrdinalIgnoreCase);

    private string FormatAuditStamp(DateTime utc)
    {
        var local = UtcToTenantLocal(utc, despatchOptions.Value.TimeZone);

        return local is null
            ? string.Create(CultureInfo.InvariantCulture, $"{utc:d MMM yyyy 'at' h:mm tt} UTC")
            : string.Create(CultureInfo.InvariantCulture, $"{local.Value:d MMM yyyy 'at' h:mm tt}");
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

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