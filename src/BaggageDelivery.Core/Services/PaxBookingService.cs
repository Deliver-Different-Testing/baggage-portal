using System.Globalization;
using BaggageDelivery.Core.Enums;
using BaggageDelivery.Core.Http;
using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

namespace BaggageDelivery.Core.Services;

internal sealed class PaxBookingService(
    BaggageDeliveryContext db,
    IOptions<DespatchOptions> despatchOptions,
    TimeProvider time) : IPaxBookingService
{
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
                ClientName = j.UcjbClient != null ? j.UcjbClient.UcclName : null
            })
            .FirstOrDefaultAsync(ct);

        if (job is null)
        {
            Log.Warning("GetSummary: tucJob {JobId} not found", jobId);
            return null;
        }

        var atlOptions = await db.TblJobLeaveNotHomes
            .AsNoTracking()
            .Where(l => l.AllowLeave)
            .OrderBy(l => l.Sequence).ThenBy(l => l.Name)
            .Select(l => new AtlOptionDto(l.LeaveNotHomeId, l.Name))
            .ToListAsync(ct);

        var now = time.GetUtcNow().UtcDateTime;
        var etaUtc = TenantLocalToUtc(job.DeliverByTime, despatchOptions.Value.TimeZone);

        return new BookingSummary(
            JobId: jobId,
            Reference: jobId.ToString(),
            AirlineLabel: string.IsNullOrWhiteSpace(job.ClientName) ? "Your Airline" : job.ClientName,
            PassengerName: job.DeliverToContact ?? string.Empty,
            PassengerPhone: job.DeliverToPhone,
            PassengerEmail: job.ProofOfDeliveryEmail,
            DeliveryAddress: new AddressUpdateDto
            {
                Line1 = string.Empty,
                City = string.Empty,
                Country = despatchOptions.Value.Countries is { Length: > 0 } cs ? cs[0] : "NZ"
            },
            EarliestSlotUtc: etaUtc ?? now,
            LatestSlotUtc: etaUtc ?? now.AddDays(2),
            AtlOptions: atlOptions);
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
        var setting = await db.TblEcoSettings
            .AsNoTracking()
            .OrderBy(s => s.SettingId)
            .Select(s => new
            {
                s.EconomyRun1,
                s.EconomyRun2,
                s.EconomyRun3,
                s.EconomyRun4,
                s.EconomyRun5
            })
            .FirstOrDefaultAsync(ct);

        if (setting is null)
        {
            Log.Warning("GetTimeslots: tblEcoSetting has no rows — returning empty slot list");
            return [];
        }

        var runs = new[]
        {
            setting.EconomyRun1,
            setting.EconomyRun2,
            setting.EconomyRun3,
            setting.EconomyRun4,
            setting.EconomyRun5
        };

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

        var leaveId = int.TryParse(input.AtlOption, out var parsed) ? parsed : (int?)null;
        var deliverByLocal = UtcToTenantLocal(input.TimeSlotEndUtc, despatchOptions.Value.TimeZone);
        
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
                    .SetProperty(j => j.DeliveryAddressLine3, input.Address.Suburb)
                    .SetProperty(j => j.DeliveryAddressLine4, input.Address.City)
                    .SetProperty(j => j.DeliveryAddressLine5, input.Address.PostCode)
                    .SetProperty(j => j.DeliveryAddressLine6, input.Address.Country)
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