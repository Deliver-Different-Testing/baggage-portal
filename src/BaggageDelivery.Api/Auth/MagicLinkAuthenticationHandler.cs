using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using BaggageDelivery.Core.MagicLink;
using BaggageDelivery.Core.Models.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace BaggageDelivery.Api.Auth;

// Resolves a passenger session in two stages:
//   1. /api/v1/pax/session POSTs the raw token; we verify, emit a DataProtection-
//      protected cookie scoped to one JobId, and the same handler reads that cookie
//      on subsequent calls.
//   2. Routes carrying [Authorize(AuthenticationSchemes = "MagicLink")] read the
//      cookie (or, for legacy direct-link hits, the ?token= query param).
public sealed class MagicLinkAuthenticationHandler(
    IOptionsMonitor<MagicLinkSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IMagicLinkService magicLink,
    IDataProtectionProvider dpProvider) : AuthenticationHandler<MagicLinkSchemeOptions>(options, logger, encoder)
{
    private const string DataProtectionPurpose = "BaggageDelivery.MagicLinkCookie.v1";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var protector = dpProvider.CreateProtector(DataProtectionPurpose);

        if (Request.Cookies.TryGetValue(Options.CookieName, out var protectedCookie)
            && !string.IsNullOrWhiteSpace(protectedCookie))
        {
            try
            {
                var unprotected = protector.Unprotect(protectedCookie);
                var ticket = ParseCookie(unprotected);
                if (ticket is not null)
                {
                    return AuthenticateResult.Success(ticket);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "MagicLink cookie failed to unprotect — treating as anonymous");
            }
        }

        if (Request.Query.TryGetValue(Options.TokenQueryParam, out var rawToken) &&
            !string.IsNullOrWhiteSpace(rawToken))
        {
            var scope = Request.Path.StartsWithSegments("/api/v1/pax/tracking")
                ? MagicLinkScope.Track
                : MagicLinkScope.Confirm;

            var verify = await magicLink.VerifyAsync(rawToken!, scope, Context.RequestAborted);
            if (verify.Outcome == VerifyOutcome.Accepted && verify.Token is not null)
            {
                return AuthenticateResult.Success(BuildTicket(verify.Token));
            }

            Logger.LogInformation(
                "MagicLink verify failed — Outcome={Outcome} Scope={Scope}",
                verify.Outcome, scope);
        }

        return AuthenticateResult.NoResult();
    }

    private AuthenticationTicket BuildTicket(Core.Models.Entities.BagDelMagicLinkToken token)
    {
        var claims = new[]
        {
            new Claim(MagicLinkClaims.JobId, token.JobId.ToString(CultureInfo.InvariantCulture)),
            new Claim(MagicLinkClaims.TenantId, token.TenantId.ToString(CultureInfo.InvariantCulture)),
            new Claim(MagicLinkClaims.Scope, token.Scope),
            new Claim(MagicLinkClaims.TokenId, token.Id.ToString(CultureInfo.InvariantCulture))
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return new AuthenticationTicket(principal, Scheme.Name);
    }

    private AuthenticationTicket? ParseCookie(string payload)
    {
        // Cookie payload format: "<tokenId>|<jobId>|<tenantId>|<scope>|<expUtcTicks>"
        var parts = payload.Split('|');
        if (parts.Length != 5) return null;
        if (!long.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var expTicks))
        {
            return null;
        }

        if (DateTime.UtcNow.Ticks > expTicks)
        {
            return null;
        }

        var claims = new[]
        {
            new Claim(MagicLinkClaims.TokenId, parts[0]),
            new Claim(MagicLinkClaims.JobId, parts[1]),
            new Claim(MagicLinkClaims.TenantId, parts[2]),
            new Claim(MagicLinkClaims.Scope, parts[3])
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        return new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
    }

    public static string BuildCookiePayload(int tokenId, int jobId, int tenantId, string scope, DateTime expiresAtUtc)
    {
        return string.Create(CultureInfo.InvariantCulture,
            $"{tokenId}|{jobId}|{tenantId}|{scope}|{expiresAtUtc.Ticks}");
    }

    public static string ProtectCookiePayload(IDataProtectionProvider provider, string payload)
    {
        return provider.CreateProtector(DataProtectionPurpose).Protect(payload);
    }
}
