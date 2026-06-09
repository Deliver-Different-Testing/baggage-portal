using System.Globalization;
using System.Text.Json;
using BaggageDelivery.Api.Auth;
using BaggageDelivery.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BaggageDelivery.Api.Controllers.Pax;

[ApiController]
[Route("api/v1/pax/tracking")]
public sealed class PaxTrackingController(
    IPaxTrackingService tracking,
    ILogger<PaxTrackingController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [HttpGet("")]
    [Authorize(AuthenticationSchemes = MagicLinkSchemeOptions.SchemeName)]
    public async Task<IActionResult> GetTimeline(CancellationToken ct)
    {
        var (jobId, tenantId) = CurrentClaims();
        var dto = await tracking.GetTimelineAsync(jobId, tenantId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("stream")]
    [Authorize(AuthenticationSchemes = MagicLinkSchemeOptions.SchemeName)]
    public async Task Stream(CancellationToken ct)
    {
        var (jobId, tenantId) = CurrentClaims();

        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        string? lastSerialised = null;

        try
        {
            // 5-minute idle limit; client reconnects via EventSource.
            for (var i = 0; i < 30 && !ct.IsCancellationRequested; i++)
            {
                var dto = await tracking.GetTimelineAsync(jobId, tenantId, ct);
                if (dto is not null)
                {
                    var serialised = JsonSerializer.Serialize(dto, JsonOptions);
                    if (serialised != lastSerialised)
                    {
                        await Response.WriteAsync($"data: {serialised}\n\n", ct);
                        await Response.Body.FlushAsync(ct);
                        lastSerialised = serialised;
                    }
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), ct);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Tracking stream failed for JobId={JobId}", jobId);
        }
    }

    private (int JobId, int TenantId) CurrentClaims()
    {
        var jobId = int.Parse(User.FindFirst(MagicLinkClaims.JobId)?.Value
            ?? throw new InvalidOperationException("JobId claim missing"),
            CultureInfo.InvariantCulture);
        var tenantId = int.Parse(User.FindFirst(MagicLinkClaims.TenantId)?.Value
            ?? throw new InvalidOperationException("TenantId claim missing"),
            CultureInfo.InvariantCulture);
        return (jobId, tenantId);
    }
}
