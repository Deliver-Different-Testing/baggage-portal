using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Security;
using Microsoft.Extensions.Options;
using Serilog;

namespace BaggageDelivery.Core.Http;

public sealed class DespatchApiClient(
    HttpClient httpClient,
    IOptions<DespatchUrlsOptions> urlOptions,
    IOptions<DespatchOptions> despatchOptions) : IDespatchApiClient
{
    private const string TokenName = "BaggageDelivery";

    public async Task<bool> ReleaseBaggageJobAsync(BookingReleaseRequest request, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/Baggage/release", request, ct);

        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        Log.Warning("api Baggage/release failed for {JobId}: {StatusCode} {Body}",
            request.JobID, response.StatusCode, body);
        return false;
    }

    public async Task<bool> CancelBaggageJobAsync(BookingCancelRequest request, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/Baggage/cancel", request, ct);

        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        Log.Warning("api Baggage/cancel failed for {JobId}: {StatusCode} {Body}",
            request.JobID, response.StatusCode, body);
        return false;
    }

    public async Task<bool> SendOnHoldAsync(SendOnHoldBookingRequest request, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/Baggage/send-on-hold", request, ct);

        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        Log.Warning("api Baggage/send-on-hold failed for {JobId}: {StatusCode} {Body}",
            request.JobID, response.StatusCode, body);
        return false;
    }

    public async Task<bool> UpdateJobDeliveryAsync(int jobId, DeliveryUpdateRequest request, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Patch, $"api/Jobs/{jobId}/delivery", request, ct);

        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        Log.Warning("api Jobs/{JobId}/delivery PATCH failed: {StatusCode} {Body}",
            jobId, response.StatusCode, body);
        return false;
    }

    public async Task<bool> UpdateJobStatusAsync(int jobId, JobStatusUpdateRequest request, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Patch, $"api/Jobs/{jobId}/status", request, ct);

        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        Log.Warning("api Jobs/{JobId}/status PATCH failed: {StatusCode} {Body}",
            jobId, response.StatusCode, body);
        return false;
    }

    private async Task<HttpResponseMessage> SendAsync<T>(
        HttpMethod method, string path, T? body, CancellationToken ct)
    {
        var baseUrl = urlOptions.Value.ApiBaseUrl
            ?? throw new InvalidOperationException(
                "WebAPIUrl is not configured. Set the 'WebAPIUrl' env var (or DespatchUrls:ApiBaseUrl).");

        var opts = despatchOptions.Value;
        if (opts.TenantId <= 0 || string.IsNullOrWhiteSpace(opts.Connection) || string.IsNullOrWhiteSpace(opts.TimeZone))
        {
            throw new InvalidOperationException(
                "Despatch identity is not configured. Set Despatch__TenantId / Despatch__Connection / Despatch__TimeZone env vars.");
        }

        var token = AuthenticationExtensions.CreateApiToken(
            name: TokenName,
            tenantId: opts.TenantId,
            connection: opts.Connection,
            timeZone: opts.TimeZone,
            clientId: null,
            contactId: 0);

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
