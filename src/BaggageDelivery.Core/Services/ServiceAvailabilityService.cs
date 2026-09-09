using BaggageDelivery.Core.Http;
using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

namespace BaggageDelivery.Core.Services;

internal sealed class ServiceAvailabilityService(
    BaggageDeliveryContext db,
    IAvailableServicesQuery availableServices,
    IOptions<AllowedServiceOptions> allowedOptions,
    IOptions<DespatchOptions> despatchOptions) : IServiceAvailabilityService
{
    private const int ScheduleIdMultiplier = 1000;

    public async Task<IReadOnlyList<CandidateService>> GetAllowedServicesAsync(
        int jobId, AddressUpdateDto address, DateTime asOfUtc, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(address);

        var job = await db.TucJobs
            .AsNoTracking()
            .Where(j => j.UcjbId == jobId && j.UcjbClientId != null)
            .Select(j => new JobContext(
                j.UcjbClientId!.Value,
                j.UcjbSize,
                db.TucSuburbs.Where(s => s.UcsuId == j.UcjbFrom).Select(s => s.UcsuName)
                    .FirstOrDefault(),
                db.TucSuburbs.Where(s => s.UcsuId == j.UcjbFrom).Select(s => s.PostCode)
                    .FirstOrDefault(),
                j.UcjbClient!.EconomyActive,
                j.UcjbClient.EconomyRuns))
            .FirstOrDefaultAsync(ct);

        if (job is null)
        {
            Log.Warning("Service availability: tucJob {JobId} not found or has no client", jobId);
            return [];
        }

        var request = new AvailableServicesRequest(
            ClientId: job.ClientId,
            SizeName: await SizeNameAsync(job.SizeId, ct),
            FromSuburb: job.FromSuburbName,
            FromPostCode: ParsePostCode(job.FromPostCode),
            ToSuburb: address.Line5,
            ToPostCode: ParsePostCode(address.Line7),
            ToLatitude: address.Latitude,
            ToLongitude: address.Longitude,
            AsOfLocal: ToTenantLocal(asOfUtc));

        var rows = await availableServices.ExecuteAsync(request, ct);

        var candidates = rows
            .Where(r => r.JobTypeID is not null)
            .Select(ToCandidate)
            .ToList();

        var flags = new ClientServiceFlags(job.EconomyActive, job.EconomyRuns);
        return AllowedServicePolicy.Filter(candidates, allowedOptions.Value, flags);
    }

    private CandidateService ToCandidate(BagDel_stpAvailableServicesResult row)
    {
        var id = row.JobTypeID!.Value;
        var scheduleId = id >= ScheduleIdMultiplier ? id / ScheduleIdMultiplier : (int?)null;

        return new CandidateService(
            JobTypeId: id,
            ScheduleId: scheduleId,
            Name: row.Name ?? string.Empty,
            SystemName: row.Speed,
            Description: row.Description,
            Availability: row.Availability ?? AvailabilityVerdict.Unavailable,
            BookDateUtc: row.BookDate is { } local ? ToUtc(local) : null,
            DurationMinutes: row.Duration,
            AvailabilityColour: row.AvailabilityColour);
    }

    // @Size is a vehicle-type name the proc matches with LIKE, not the numeric size id
    private async Task<string?> SizeNameAsync(int? sizeId, CancellationToken ct)
    {
        if (sizeId is not { } id)
        {
            return null;
        }

        var key = id.ToString();
        return await db.TblJobSizeNames
            .AsNoTracking()
            .Where(s => s.SizeId == key)
            .Select(s => s.Name)
            .FirstOrDefaultAsync(ct);
    }

    private static int? ParsePostCode(string? value) =>
        int.TryParse(value, out var postCode) && postCode > 0 ? postCode : null;

    private TimeZoneInfo? TenantTimeZone()
    {
        var id = despatchOptions.Value.TimeZone;
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            Log.Warning(ex, "Service availability: unknown tenant time zone {TimeZone}", id);
            return null;
        }
    }

    private DateTime ToTenantLocal(DateTime utc) =>
        TenantTimeZone() is { } tz
            ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz)
            : utc;

    private DateTime? ToUtc(DateTime local)
    {
        if (TenantTimeZone() is not { } tz)
        {
            return DateTime.SpecifyKind(local, DateTimeKind.Utc);
        }

        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return tz.IsInvalidTime(unspecified)
            ? null
            : TimeZoneInfo.ConvertTimeToUtc(unspecified, tz);
    }

    private sealed record JobContext(
        int ClientId,
        int? SizeId,
        string? FromSuburbName,
        string? FromPostCode,
        bool EconomyActive,
        bool EconomyRuns);
}
