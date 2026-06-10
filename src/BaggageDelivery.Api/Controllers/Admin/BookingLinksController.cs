using BaggageDelivery.Api.DTOs.Admin;
using BaggageDelivery.Core.Enums;
using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
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
    IBookingLinkDispatchService dispatch,
    IDespatchApiClient despatch_,
    IOptions<BookingLinkOptions> linkOptions) : ControllerBase
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

        // No shadow row to upsert — tucJob is the canonical record, JobId
        // is what the URL encrypts. Idempotent by construction: minting
        // for the same JobId twice produces the same URL.
        var token = encryption.EncryptId(body.JobId);
        var confirmUrl = $"{publicBase}/c/{token}";
        var trackUrl = $"{publicBase}/t/{token}";

        // Flip the courier job to JobStatus.New via the api repo — tucJob
        // is read-only from BaggageDelivery's DB user, all writes route
        // through the SC-JWT-authed api endpoint which also stamps the
        // JobDeliveryJourney audit row. Best-effort: any failure is logged
        // and swallowed so the passenger link still gets minted.
        var statusOk = await despatch_.UpdateJobStatusAsync(
            body.JobId,
            new JobStatusUpdateRequest
            {
                Status = (int)JobStatus.New,
                Comment = "Pax confirmation link sent"
            },
            ct);

        if (!statusOk)
        {
            Log.Warning(
                "Mint: api UpdateJobStatus({JobId}, New) returned false (link still minted)",
                body.JobId);
        }

        var passengerName = body.PassengerName ?? "there";
        var airline = body.AirlineLabel ?? "Urgent";
        var reference = body.Reference ?? body.JobId.ToString();

        if (body.Channel is "sms" or "both" && !string.IsNullOrWhiteSpace(body.Phone))
        {
            await dispatch.EnqueueAsync(new EnqueueNotificationRequest(
                body.JobId, NotificationChannel.Sms, body.Phone!, passengerName, airline, reference, confirmUrl), ct);
        }

        if (body.Channel is "email" or "both" && !string.IsNullOrWhiteSpace(body.Email))
        {
            await dispatch.EnqueueAsync(new EnqueueNotificationRequest(
                body.JobId, NotificationChannel.Email, body.Email!, passengerName, airline, reference, confirmUrl), ct);
        }

        Log.Information(
            "Minted booking link: JobId={JobId} Channel={Channel}",
            body.JobId, body.Channel);

        return Ok(new MintBookingLinkResponse(token, confirmUrl, trackUrl));
    }
}
