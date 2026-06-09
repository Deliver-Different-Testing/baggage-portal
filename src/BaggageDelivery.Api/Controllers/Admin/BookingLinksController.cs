using BaggageDelivery.Api.DTOs.Admin;
using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.Models.Entities;
using BaggageDelivery.Core.Security;
using BaggageDelivery.Core.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace BaggageDelivery.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/booking-links")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class BookingLinksController(
    BaggageDeliveryContext db,
    IEncryptionService encryption,
    IBookingLinkDispatchService dispatch,
    IOptions<BookingLinkOptions> linkOptions,
    TimeProvider time,
    ILogger<BookingLinksController> logger) : ControllerBase
{
    [HttpPost("")]
    [EnableRateLimiting("admin-mint")]
    public async Task<ActionResult<MintBookingLinkResponse>> Mint(
        [FromBody] MintBookingLinkRequest body,
        CancellationToken ct)
    {
        var publicBase = linkOptions.Value.PublicBaseUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(publicBase))
        {
            return Problem("BookingLinks:PublicBaseUrl is not configured");
        }

        var now = time.GetUtcNow().UtcDateTime;
        var booking = new BagDelBooking
        {
            JobId = body.JobId,
            TenantId = body.TenantId,
            CreatedAtUtc = now
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync(ct);

        var encryptedId = encryption.EncryptId(booking.Id);
        var confirmUrl = $"{publicBase}/c/{encryptedId}";
        var trackUrl = $"{publicBase}/t/{encryptedId}";

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

        logger.LogInformation(
            "Minted booking link: BookingId={BookingId} JobId={JobId} TenantId={TenantId} Channel={Channel}",
            booking.Id, body.JobId, body.TenantId, body.Channel);

        return Ok(new MintBookingLinkResponse(booking.Id, encryptedId, confirmUrl, trackUrl));
    }
}
