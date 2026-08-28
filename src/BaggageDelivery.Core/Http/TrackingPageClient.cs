using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Interfaces;
using Microsoft.Extensions.Options;
using Serilog;

namespace BaggageDelivery.Core.Http;

public sealed class TrackingPageClient(
    HttpClient httpClient,
    IOptions<TrackingPageUrlsOptions> urlOptions)
    : ITrackingPageClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public async Task<TrackingDto?> GetJobAsync(int jobId, CancellationToken ct)
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

            if (response.IsSuccessStatusCode && parsed is { Success: true, Job: { } job })
            {
                return MapToTrackingDto(job);
            }

            if (response.StatusCode != HttpStatusCode.BadRequest && response.StatusCode != HttpStatusCode.NotFound)
            {
                Log.Warning(
                    "TrackingPage GetJob unexpected response for JobId={JobId}: {StatusCode}",
                    jobId, response.StatusCode);
            }

            return null;
        }
        catch (HttpRequestException ex)
        {
            Log.Warning(ex, "TrackingPage transport failure for JobId={JobId}", jobId);
            return null;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            Log.Warning("TrackingPage request timed out for JobId={JobId}", jobId);
            return null;
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

    private sealed record JobResponseWire(bool Success, JobWire? Job);

    private sealed record JobWire(
        int Id,
        int? StatusId,
        DateTime? DeliverByTime,
        string? TimeZoneCode,
        string? DeliverByTimeZoneCode);
}
