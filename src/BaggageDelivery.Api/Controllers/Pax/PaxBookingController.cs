using System.Globalization;
using BaggageDelivery.Api.Auth;
using BaggageDelivery.Api.DTOs.Pax;
using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.MagicLink;
using BaggageDelivery.Core.Models.Entities;
using BaggageDelivery.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BaggageDelivery.Api.Controllers.Pax;

[ApiController]
[Route("api/v1/pax")]
public sealed class PaxBookingController(
    IMagicLinkService magicLink,
    IPaxBookingService paxBooking,
    IDataProtectionProvider dpProvider,
    ILogger<PaxBookingController> logger) : ControllerBase
{
    [HttpPost("session")]
    [AllowAnonymous]
    [EnableRateLimiting("pax-session")]
    public async Task<ActionResult<SessionResponse>> Session([FromBody] SessionRequest body, CancellationToken ct)
    {
        var verify = await magicLink.VerifyAsync(body.Token, MagicLinkScope.Confirm, ct);
        var outcome = verify.Outcome;

        // Track-scope tokens are also valid for session creation.
        if (outcome != VerifyOutcome.Accepted)
        {
            var trackTry = await magicLink.VerifyAsync(body.Token, MagicLinkScope.Track, ct);
            if (trackTry.Outcome == VerifyOutcome.Accepted)
            {
                verify = trackTry;
                outcome = trackTry.Outcome;
            }
        }

        var truncatedHash = TruncateHash(body.Token);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();
        logger.LogInformation(
            "MagicLinkVerify Outcome={Outcome} TokenHashPrefix={Prefix} IP={Ip} UserAgent={UserAgent}",
            outcome, truncatedHash, ip, ua);

        if (outcome != VerifyOutcome.Accepted || verify.Token is null)
        {
            return outcome switch
            {
                VerifyOutcome.Expired => StatusCode(StatusCodes.Status410Gone, new { reason = "expired" }),
                VerifyOutcome.Revoked => StatusCode(StatusCodes.Status410Gone, new { reason = "revoked" }),
                VerifyOutcome.AlreadyUsed => Conflict(new { reason = "already_used" }),
                _ => Unauthorized()
            };
        }

        var cookieExpiry = DateTime.UtcNow.AddMinutes(15);
        var payload = MagicLinkAuthenticationHandler.BuildCookiePayload(
            verify.Token.Id, verify.Token.JobId, verify.Token.TenantId, verify.Token.Scope, cookieExpiry);
        var protectedCookie = MagicLinkAuthenticationHandler.ProtectCookiePayload(dpProvider, payload);

        Response.Cookies.Append("bagdel.sess", protectedCookie, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = cookieExpiry,
            Path = "/api/v1/pax"
        });

        var summary = await paxBooking.GetSummaryAsync(verify.Token.JobId, verify.Token.TenantId, ct);
        if (summary is null) return NotFound();

        return Ok(new SessionResponse(MapSummary(summary), cookieExpiry));
    }

    [HttpGet("booking")]
    [Authorize(AuthenticationSchemes = MagicLinkSchemeOptions.SchemeName)]
    public async Task<ActionResult<BookingSummaryDto>> GetBooking(CancellationToken ct)
    {
        var (jobId, tenantId, _, _) = CurrentClaims();
        var summary = await paxBooking.GetSummaryAsync(jobId, tenantId, ct);
        return summary is null ? NotFound() : Ok(MapSummary(summary));
    }

    [HttpGet("booking/timeslots")]
    [Authorize(AuthenticationSchemes = MagicLinkSchemeOptions.SchemeName)]
    public ActionResult<TimeSlotDto[]> GetTimeslots([FromQuery] DateTime? date)
    {
        // v1: server generates slot windows in UTC. Real implementation should pull
        // available runs from Despatch once GET api/Jobs/{id}/slots ships.
        var anchor = (date ?? DateTime.UtcNow).Date;
        var slots = new[]
        {
            new TimeSlotDto(Guid.NewGuid(), anchor.AddHours(9), anchor.AddHours(12), "9:00 AM - 12:00 PM", FirstAvailable: true),
            new TimeSlotDto(Guid.NewGuid(), anchor.AddHours(14), anchor.AddHours(17), "2:00 PM - 5:00 PM", FirstAvailable: false),
            new TimeSlotDto(Guid.NewGuid(), anchor.AddDays(1).AddHours(9), anchor.AddDays(1).AddHours(12), "Tomorrow 9 AM - 12 PM", FirstAvailable: false),
            new TimeSlotDto(Guid.NewGuid(), anchor.AddDays(1).AddHours(14), anchor.AddDays(1).AddHours(17), "Tomorrow 2 PM - 5 PM", FirstAvailable: false)
        };
        return Ok(slots);
    }

    [HttpPost("booking/confirm")]
    [Authorize(Policy = MagicLinkPolicies.PaxConfirm)]
    public async Task<ActionResult<ConfirmBookingResponse>> Confirm([FromBody] ConfirmBookingRequest body, CancellationToken ct)
    {
        var (jobId, tenantId, _, tokenId) = CurrentClaims();
        try
        {
            await paxBooking.ConfirmAsync(new ConfirmBookingInput(
                JobId: jobId,
                TenantId: tenantId,
                TokenId: tokenId,
                Address: new AddressUpdateDto
                {
                    Line1 = body.Address.Line1,
                    Line2 = body.Address.Line2,
                    Suburb = body.Address.Suburb,
                    City = body.Address.City,
                    PostCode = body.Address.PostCode,
                    Country = body.Address.Country,
                    Latitude = body.Address.Latitude,
                    Longitude = body.Address.Longitude
                },
                TimeSlotStartUtc: body.TimeSlotStartUtc,
                TimeSlotEndUtc: body.TimeSlotEndUtc,
                AtlOption: body.AtlOption,
                AccessNotes: body.AccessNotes,
                PhoneOverride: body.PhoneOverride), ct);

            return Ok(new ConfirmBookingResponse("Released", DateTime.UtcNow));
        }
        catch (ConfirmationAlreadyExistsException)
        {
            return Conflict(new { reason = "already_confirmed" });
        }
    }

    private (int JobId, int TenantId, string Scope, int TokenId) CurrentClaims()
    {
        var jobId = int.Parse(User.FindFirst(MagicLinkClaims.JobId)?.Value
            ?? throw new InvalidOperationException("JobId claim missing"),
            CultureInfo.InvariantCulture);
        var tenantId = int.Parse(User.FindFirst(MagicLinkClaims.TenantId)?.Value
            ?? throw new InvalidOperationException("TenantId claim missing"),
            CultureInfo.InvariantCulture);
        var scope = User.FindFirst(MagicLinkClaims.Scope)?.Value
            ?? throw new InvalidOperationException("Scope claim missing");
        var tokenId = int.Parse(User.FindFirst(MagicLinkClaims.TokenId)?.Value
            ?? throw new InvalidOperationException("TokenId claim missing"),
            CultureInfo.InvariantCulture);
        return (jobId, tenantId, scope, tokenId);
    }

    private static BookingSummaryDto MapSummary(BookingSummary s) => new(
        JobId: s.JobId,
        Reference: s.Reference,
        AirlineLabel: s.AirlineLabel,
        PassengerName: s.PassengerName,
        PassengerPhone: s.PassengerPhone,
        PassengerEmail: s.PassengerEmail,
        DeliveryAddress: new AddressDto(
            s.DeliveryAddress.Line1,
            s.DeliveryAddress.Line2,
            s.DeliveryAddress.Suburb,
            s.DeliveryAddress.City,
            s.DeliveryAddress.PostCode,
            s.DeliveryAddress.Country,
            s.DeliveryAddress.Latitude,
            s.DeliveryAddress.Longitude),
        EarliestSlotUtc: s.EarliestSlotUtc,
        LatestSlotUtc: s.LatestSlotUtc);

    private static string TruncateHash(string raw)
    {
        // Match the SHA-256 prefix length the audit logger uses elsewhere — 16 hex
        // chars is enough to correlate without leaking enough to brute-force.
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes.AsSpan(0, 8)).ToLowerInvariant();
    }
}
