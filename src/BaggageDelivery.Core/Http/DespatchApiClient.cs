using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BaggageDelivery.Core.Enums;
using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Security;
using Microsoft.Extensions.Options;
using Serilog;

namespace BaggageDelivery.Core.Http;

public sealed class DespatchApiClient(HttpClient httpClient, IOptions<DespatchUrlsOptions> urlOptions)
    : IDespatchApiClient
{
    private const string TokenName = "BaggageDelivery";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<bool> ReleaseBaggageJobAsync(int tenantId, string connection, string timeZone,
        int? clientId, int contactId, BookingReleaseRequest request, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/Baggage/release",
            tenantId, connection, timeZone, clientId, contactId, request, ct);

        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        Log.Warning("api Baggage/release failed for {JobId}: {StatusCode} {Body}",
            request.JobID, response.StatusCode, body);
        return false;
    }

    public async Task<bool> CancelBaggageJobAsync(int tenantId, string connection, string timeZone,
        int? clientId, int contactId, BookingCancelRequest request, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/Baggage/cancel",
            tenantId, connection, timeZone, clientId, contactId, request, ct);

        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        Log.Warning("api Baggage/cancel failed for {JobId}: {StatusCode} {Body}",
            request.JobID, response.StatusCode, body);
        return false;
    }

    public async Task<bool> SendOnHoldAsync(int tenantId, string connection, string timeZone,
        int? clientId, int contactId, SendOnHoldBookingRequest request, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/Baggage/send-on-hold",
            tenantId, connection, timeZone, clientId, contactId, request, ct);

        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        Log.Warning("api Baggage/send-on-hold failed for {JobId}: {StatusCode} {Body}",
            request.JobID, response.StatusCode, body);
        return false;
    }

    public async Task<bool> UpdateJobDeliveryAsync(int tenantId, string connection, string timeZone,
        int? clientId, int contactId, int jobId, DeliveryUpdateRequest request, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Patch, $"api/Jobs/{jobId}/delivery",
            tenantId, connection, timeZone, clientId, contactId, request, ct);

        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        Log.Warning("api Jobs/{JobId}/delivery PATCH failed: {StatusCode} {Body}",
            jobId, response.StatusCode, body);
        return false;
    }

    public async Task<TrackingDto?> GetJobTrackingAsync(int tenantId, string connection, string timeZone,
        int? clientId, int contactId, int jobId, CancellationToken ct)
    {
        using var response = await SendAsync<object?>(HttpMethod.Get, $"api/Jobs/{jobId}/tracking",
            tenantId, connection, timeZone, clientId, contactId, body: null, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(ct);

        if (response.IsSuccessStatusCode)
        {
            return JsonSerializer.Deserialize<TrackingDto>(body, JsonOptions);
        }

        Log.Warning("api Jobs/{JobId}/tracking failed: {StatusCode} {Body}",
            jobId, response.StatusCode, body);
        return null;
    }

    // Reconciliation only cares whether the job exists; we reuse the tracking
    // endpoint and map: 200 → Exists, 404 → NotFound, anything else → Unknown.
    // Unknown is deliberately distinct from NotFound so transient 5xx /
    // circuit-breaker trips never flip a real booking to orphaned.
    public async Task<JobExistenceResult> CheckJobExistsAsync(int tenantId, string connection, string timeZone,
        int? clientId, int contactId, int jobId, CancellationToken ct)
    {
        try
        {
            using var response = await SendAsync<object?>(HttpMethod.Get, $"api/Jobs/{jobId}/tracking",
                tenantId, connection, timeZone, clientId, contactId, body: null, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return JobExistenceResult.NotFound;
            }

            if (response.IsSuccessStatusCode)
            {
                return JobExistenceResult.Exists;
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            Log.Warning("CheckJobExists: api Jobs/{JobId}/tracking returned {StatusCode} {Body}",
                jobId, response.StatusCode, body);
            return JobExistenceResult.Unknown;
        }
        catch (HttpRequestException ex)
        {
            Log.Warning(ex, "CheckJobExists: transport failure for JobId={JobId}", jobId);
            return JobExistenceResult.Unknown;
        }
    }

    private async Task<HttpResponseMessage> SendAsync<T>(HttpMethod method, string path,
        int tenantId, string connection, string timeZone, int? clientId, int contactId, T? body, CancellationToken ct)
    {
        var baseUrl = urlOptions.Value.ApiBaseUrl
            ?? throw new InvalidOperationException(
                "WebAPIUrl is not configured. Set the 'WebAPIUrl' env var (or DespatchUrls:ApiBaseUrl).");

        var token = AuthenticationExtensions.CreateApiToken(
            name: TokenName,
            tenantId: tenantId,
            connection: connection,
            timeZone: timeZone,
            clientId: clientId,
            contactId: contactId);

        var requestToken = new JwtSecurityTokenHandler().WriteToken(token);

        var request = new HttpRequestMessage(method, new Uri(baseUrl, path));
        request.Headers.Add("Authorization", $"Bearer {requestToken}");

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await httpClient.SendAsync(request, ct);
    }
}
