using System.Text.Json;
using BaggageDelivery.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace BaggageDelivery.Api.Controllers.Pax;

[ApiController]
[Route("api/v1/pax/{id}/tracking")]
[AllowAnonymous]
public sealed class PaxTrackingController(
    IEncryptionService encryption,
    IPaxTrackingService tracking) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [HttpGet("")]
    public async Task<IActionResult> GetTimeline(string id, CancellationToken ct)
    {
        var jobId = encryption.DecryptId(id);
        if (jobId is null)
        {
            return NotFound();
        }

        var dto = await tracking.GetTimelineAsync(jobId.Value, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("stream")]
    public async Task Stream(string id, CancellationToken ct)
    {
        var jobId = encryption.DecryptId(id);
        if (jobId is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        string? lastSerialised = null;

        try
        {
            for (var i = 0; i < 30 && !ct.IsCancellationRequested; i++)
            {
                var dto = await tracking.GetTimelineAsync(jobId.Value, ct);
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
            Log.Warning(ex, "Tracking stream failed for JobId={JobId}", jobId.Value);
        }
    }
}
