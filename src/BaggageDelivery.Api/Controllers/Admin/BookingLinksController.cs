using BaggageDelivery.Api.DTOs.Admin;
using BaggageDelivery.Core.Interfaces; 
using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.Notifications;
using BaggageDelivery.Core.Security;
using BaggageDelivery.Core.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Serilog;

namespace BaggageDelivery.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/booking-links")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class BookingLinksController(
    IEncryptionService encryption,
    INotificationService notifications,
    IPaxBookingService bookings,
    IOptions<BookingLinkOptions> linkOptions) : ControllerBase
{
    private const string DefaultPassenger = "Unknown Passenger";
    
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
        
        var token = encryption.EncryptId(body.JobId);
        var confirmUrl = $"{publicBase}/c/{token}";
        var trackUrl = $"{publicBase}/t/{token}";

        var details = await bookings.GetNotificationDetailsAsync(body.JobId, ct);

        var passengerName = body.PassengerName ?? DefaultPassenger;
        var airline = !string.IsNullOrWhiteSpace(body.AirlineLabel)
            ? body.AirlineLabel
            : details?.AirlineLabel ?? BookingNotificationDetails.UnknownAirline;
        var fileReference = details?.FileReference ?? string.Empty;

        if (body.Channel is "sms" or "both" && !string.IsNullOrWhiteSpace(body.Phone))
        {
            await notifications.SendBookingLinkAsync(
                body.JobId, body.Phone!,
                new BookingNotificationContext(
                    NotificationChannel.Sms, passengerName, airline, fileReference, confirmUrl),
                ct);
        }

        if (body.Channel is "email" or "both" && !string.IsNullOrWhiteSpace(body.Email))
        {
            await notifications.SendBookingLinkAsync(
                body.JobId, body.Email!,
                new BookingNotificationContext(
                    NotificationChannel.Email, passengerName, airline, fileReference, confirmUrl),
                ct);
        }

        Log.Information(
            "Minted booking link: JobId={JobId} Channel={Channel}",
            body.JobId, body.Channel);

        return Ok(new MintBookingLinkResponse(token, confirmUrl, trackUrl));
    }
}
