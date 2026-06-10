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
    IDespatchApiClient despatch,
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

    public async Task ConfirmAsync(ConfirmBookingInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        var deliveryUpdate = new DeliveryUpdateRequest
        {
            Address = input.Address,
            TimeSlotStartUtc = input.TimeSlotStartUtc,
            TimeSlotEndUtc = input.TimeSlotEndUtc,
            AtlOption = input.AtlOption,
            AccessNotes = input.AccessNotes,
            PassengerName = input.PassengerName,
            PassengerPhone = input.PassengerPhone,
            PassengerEmail = input.PassengerEmail
        };

        var updateOk = await despatch.UpdateJobDeliveryAsync(input.JobId, deliveryUpdate, ct);
        if (!updateOk)
        {
            throw new InvalidOperationException(
                $"api Jobs/{input.JobId}/delivery returned non-success — pax confirmation not persisted");
        }

        var releaseOk = await despatch.ReleaseBaggageJobAsync(
            new BookingReleaseRequest { JobID = input.JobId }, ct);
        if (!releaseOk)
        {
            throw new InvalidOperationException(
                $"api Baggage/release returned non-success for JobId={input.JobId} — pax confirmation not persisted");
        }

        Log.Information("Pax confirmation released: JobId={JobId}", input.JobId);
    }
}
