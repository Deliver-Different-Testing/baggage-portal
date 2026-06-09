using System.Text.Json;
using BaggageDelivery.Core.Security;
using BaggageDelivery.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BaggageDelivery.Api.Controllers.Pax;

[ApiController]
[Route("api/v1/pax/{id}/tracking")]
[AllowAnonymous]
public sealed class PaxTrackingController(
    IEncryptionService encryption,
    IPaxTrackingService tracking,
    ILogger<PaxTrackingController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [HttpGet("")]
    public async Task<IActionResult> GetTimeline(string id, CancellationToken ct)
    {
        var bookingId = encryption.DecryptId(id);
        if (bookingId is null) return NotFound();

        var dto = await tracking.GetTimelineAsync(bookingId.Value, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("stream")]
    public async Task Stream(string id, CancellationToken ct)
    {
        var bookingId = encryption.DecryptId(id);
        if (bookingId is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        string? lastSerialised = null;

        try
        {
            // 5-minute idle limit; client reconnects via EventSource.
            for (var i = 0; i < 30 && !ct.IsCancellationRequested; i++)
            {
                var dto = await tracking.GetTimelineAsync(bookingId.Value, ct);
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
            logger.LogWarning(ex, "Tracking stream failed for BookingId={BookingId}", bookingId);
        }
    }
}
