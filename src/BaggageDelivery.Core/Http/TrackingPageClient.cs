using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using BaggageDelivery.Core.Enums;
using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Interfaces;
using Microsoft.Extensions.Options;
using Serilog;

namespace BaggageDelivery.Core.Http;

// HTTP client for the trackingpage app. No auth — trackingpage's API
// endpoints are anonymous; the encrypted-ID URL is what gates customer use
// from the public web. BaggageDelivery calls server-to-server so we hit the
// plain by-id endpoint directly.
//
// REQUIRES: trackingpage exposes `GET /api/Job/byId/{jobId}` returning the
// existing JobResponse shape. The underlying JobService.Get(int jobId,
// Guid messageId) overload already exists — only the controller route is
// missing from JobController.cs.
public sealed class TrackingPageClient(HttpClient httpClient, IOptions<TrackingPageUrlsOptions> urlOptions)
    : ITrackingPageClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public async Task<TrackingDto?> GetJobAsync(int jobId, CancellationToken ct)
    {
        var (status, body) = await FetchAsync(jobId, ct);

        if (status == JobExistenceResult.Exists && body?.Job is { } job)
        {
            return MapToTrackingDto(job);
        }

        return null;
    }

    public async Task<JobExistenceResult> CheckJobExistsAsync(int jobId, CancellationToken ct)
    {
        var (status, _) = await FetchAsync(jobId, ct);
        return status;
    }

    private async Task<(JobExistenceResult Status, JobResponseWire? Body)> FetchAsync(
        int jobId, CancellationToken ct)
    {
        var baseUrl = urlOptions.Value.BaseUrl
            ?? throw new InvalidOperationException(
                "TrackingPage base URL is not configured. " +
                "Set the 'TrackingPageUrl' env var (or TrackingPageUrls:BaseUrl).");

        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUrl, $"api/Job/byId/{jobId}"));

        try
        {
            using var response = await httpClient.SendAsync(request, ct);
            var content = await response.Content.ReadAsStringAsync(ct);
            var parsed = TryDeserialize(content);

            // trackingpage maps job-not-found to 400 BadRequest with
            // Success=false in the body (see API/Controllers/BaseController.cs).
            // 200 + Success=true is the only exists signal.
            if (response.IsSuccessStatusCode && parsed?.Success == true)
            {
                return (JobExistenceResult.Exists, parsed);
            }

            if (response.StatusCode == HttpStatusCode.BadRequest && parsed?.Success == false)
            {
                return (JobExistenceResult.NotFound, parsed);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return (JobExistenceResult.NotFound, null);
            }

            Log.Warning(
                "TrackingPage GetJob unexpected response for JobId={JobId}: {StatusCode} Success={Success}",
                jobId, response.StatusCode, parsed?.Success);
            return (JobExistenceResult.Unknown, null);
        }
        catch (HttpRequestException ex)
        {
            Log.Warning(ex, "TrackingPage transport failure for JobId={JobId}", jobId);
            return (JobExistenceResult.Unknown, null);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            Log.Warning("TrackingPage request timed out for JobId={JobId}", jobId);
            return (JobExistenceResult.Unknown, null);
        }
    }

    private static JobResponseWire? TryDeserialize(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<JobResponseWire>(content, JsonOptions);
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "TrackingPage response failed to deserialize: {Content}", content);
            return null;
        }
    }

    // Trackingpage exposes the live job state (status id, ETA delivery
    // time, courier id) but not an event timeline. Events stays empty
    // until trackingpage gains a /timeline endpoint backed by TucEvent.
    // CurrentStatus is the raw StatusId as a string — a future revision
    // can fan-out /api/JobTypeStatus (cached) for a human-readable label.
    // CourierFirstName / VehicleLabel are null because the response only
    // carries CourierId; a join through tucCourier would have to come
    // from trackingpage too.
    private static TrackingDto MapToTrackingDto(JobWire job)
    {
        var eta = TenantLocalToUtc(job.DeliverByTime, job.DeliverByTimeZoneCode ?? job.TimeZoneCode);

        return new TrackingDto
        {
            JobId = job.Id,
            CurrentStatus = job.StatusId?.ToString() ?? "Unknown",
            Events = [],
            EtaWindowStartUtc = eta,
            EtaWindowEndUtc = eta,
            CourierFirstName = null,
            VehicleLabel = null
        };
    }

    // trackingpage emits DeliverByTime in the tenant's local clock (no Kind
    // information) plus a separate IANA-ish TimeZoneCode. Convert to UTC
    // when both are present; degrade to null on any failure so we don't
    // surface a bogus instant.
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
            Log.Warning("TrackingPage timezone {TimeZoneCode} not found on this host", timeZoneCode);
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            Log.Warning("TrackingPage timezone {TimeZoneCode} is invalid", timeZoneCode);
            return null;
        }
    }

    // Wire models — subset of trackingpage's JobResponse / JobFullDto.
    // Names match the JSON the controller serialises (no JsonPropertyName
    // overrides needed since PascalCase is the default).
    private sealed record JobResponseWire(bool Success, JobWire? Job);

    private sealed record JobWire(
        int Id,
        int? StatusId,
        DateTime? DeliverByTime,
        string? TimeZoneCode,
        string? DeliverByTimeZoneCode);
}
