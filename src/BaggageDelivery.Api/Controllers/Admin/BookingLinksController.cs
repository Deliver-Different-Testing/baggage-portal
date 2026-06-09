using BaggageDelivery.Api.DTOs.Admin;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.Security;
using BaggageDelivery.Core.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

namespace BaggageDelivery.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/booking-links")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class BookingLinksController(
    BaggageDeliveryContext db,
    IEncryptionService encryption,
    IBookingLinkDispatchService dispatch,
    IOptions<BookingLinkOptions> linkOptions,
    TimeProvider time) : ControllerBase
{
    [HttpPost("")]
    [EnableRateLimiting("admin-mint")]
    public async Task<ActionResult<MintBookingLinkResponse>> Mint(
        [FromBody] MintBookingLinkRequest body,
        CancellationToken ct)
    {
        var publicBase = linkOptions.Value.PublicBaseUrl.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(publicBase))
        {
            return Problem("BookingLinks:PublicBaseUrl is not configured");
        }

        // The URL encrypts (TenantId, JobId), matching inboundagent's model: the
        // URL identifies the courier job, the BagDelBooking shadow row is owned by
        // us. Idempotent: re-minting the same (TenantId, JobId) reuses the existing
        // booking — same shadow row, same URL.
        var booking = await db.BagDelBookings
            .AsTracking()
            .FirstOrDefaultAsync(b => b.TenantId == body.TenantId && b.JobId == body.JobId, ct);

        if (booking is null)
        {
            booking = new BagDelBooking
            {
                JobId = body.JobId,
                TenantId = body.TenantId,
                CreatedAtUtc = time.GetUtcNow().UtcDateTime
            };
            db.BagDelBookings.Add(booking);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                db.Entry(booking).State = EntityState.Detached;
                booking = await db.BagDelBookings
                    .AsTracking()
                    .FirstAsync(b => b.TenantId == body.TenantId && b.JobId == body.JobId, ct);
            }
        }

        var token = encryption.EncryptToken(body.TenantId, body.JobId);
        var confirmUrl = $"{publicBase}/c/{token}";
        var trackUrl = $"{publicBase}/t/{token}";

        var passengerName = body.PassengerName ?? "there";
        var airline = body.AirlineLabel ?? "Urgent";
        var reference = body.Reference ?? body.JobId.ToString();

        if (body.Channel is "sms" or "both" && !string.IsNullOrWhiteSpace(body.Phone))
        {
            await dispatch.EnqueueAsync(new EnqueueNotificationRequest(
                booking.Id, NotificationChannel.Sms, body.Phone!, passengerName, airline, reference, confirmUrl), ct);
        }

        if (body.Channel is "email" or "both" && !string.IsNullOrWhiteSpace(body.Email))
        {
            await dispatch.EnqueueAsync(new EnqueueNotificationRequest(
                booking.Id, NotificationChannel.Email, body.Email!, passengerName, airline, reference, confirmUrl), ct);
        }

        Log.Information(
            "Minted booking link: BookingId={BookingId} JobId={JobId} TenantId={TenantId} Channel={Channel}",
            booking.Id, body.JobId, body.TenantId, body.Channel);

        return Ok(new MintBookingLinkResponse(booking.Id, token, confirmUrl, trackUrl));
    }
}
