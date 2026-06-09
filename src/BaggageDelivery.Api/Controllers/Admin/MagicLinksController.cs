using BaggageDelivery.Api.DTOs.Admin;
using BaggageDelivery.Core.MagicLink;
using BaggageDelivery.Core.Models.Entities;
using BaggageDelivery.Core.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BaggageDelivery.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/magic-links")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class MagicLinksController(
    IMagicLinkService magicLink,
    IMagicLinkDispatchService dispatch,
    ILogger<MagicLinksController> logger) : ControllerBase
{
    [HttpPost("")]
    [EnableRateLimiting("admin-mint")]
    public async Task<ActionResult<MintMagicLinkResponse>> Mint([FromBody] MintMagicLinkRequest body, CancellationToken ct)
    {
        if (body.Scope is not (MagicLinkScope.Confirm or MagicLinkScope.Track))
        {
            return BadRequest(new { reason = "invalid_scope" });
        }

        var mint = await magicLink.MintAsync(
            new MagicLinkMintRequest(body.JobId, body.TenantId, body.Scope, IssuedByService: User.Identity?.Name ?? "Unknown"),
            ct);

        var passengerName = body.PassengerName ?? "there";
        var airline = body.AirlineLabel ?? "Urgent";
        var reference = body.Reference ?? body.JobId.ToString();

        if (body.Channel is "sms" or "both" && !string.IsNullOrWhiteSpace(body.Phone))
        {
            await dispatch.EnqueueAsync(new EnqueueNotificationRequest(
                mint.TokenId, NotificationChannel.Sms, body.Phone!, passengerName, airline, reference, mint.Url), ct);
        }

        if (body.Channel is "email" or "both" && !string.IsNullOrWhiteSpace(body.Email))
        {
            await dispatch.EnqueueAsync(new EnqueueNotificationRequest(
                mint.TokenId, NotificationChannel.Email, body.Email!, passengerName, airline, reference, mint.Url), ct);
        }

        logger.LogInformation(
            "Minted magic link: JobId={JobId} TenantId={TenantId} Scope={Scope} TokenId={TokenId} Channel={Channel}",
            body.JobId, body.TenantId, body.Scope, mint.TokenId, body.Channel);

        return Ok(new MintMagicLinkResponse(mint.RawToken, mint.Url, mint.ExpiresAtUtc, mint.TokenId));
    }

    [HttpPost("{token}/revoke")]
    public async Task<IActionResult> Revoke(string token, CancellationToken ct)
    {
        await magicLink.RevokeAsync(token, ct);
        return NoContent();
    }
}
