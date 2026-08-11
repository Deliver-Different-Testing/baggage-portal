using BaggageDelivery.Api.DTOs.Admin;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.Notifications;
using BaggageDelivery.Core.Security;
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
    IOptions<BookingLinkOptions> linkOptions) : ControllerBase
{
    private const string DefaultPassenger = "Unknown Passenger";
    private const string DefaultAirline = "Deliver DFRNT";
    
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

        var passengerName = body.PassengerName ?? DefaultPassenger;
        var airline = body.AirlineLabel ?? DefaultAirline;
        var reference = body.Reference ?? body.JobId.ToString();

        if (body.Channel is "sms" or "both" && !string.IsNullOrWhiteSpace(body.Phone))
        {
            await notifications.SendBookingLinkAsync(
                body.JobId, body.Phone!,
                new BookingNotificationContext(
                    NotificationChannel.Sms, passengerName, airline, reference, confirmUrl),
                ct);
        }

        if (body.Channel is "email" or "both" && !string.IsNullOrWhiteSpace(body.Email))
        {
            await notifications.SendBookingLinkAsync(
                body.JobId, body.Email!,
                new BookingNotificationContext(
                    NotificationChannel.Email, passengerName, airline, reference, confirmUrl),
                ct);
        }

        Log.Information(
            "Minted booking link: JobId={JobId} Channel={Channel}",
            body.JobId, body.Channel);

        return Ok(new MintBookingLinkResponse(token, confirmUrl, trackUrl));
    }
}
