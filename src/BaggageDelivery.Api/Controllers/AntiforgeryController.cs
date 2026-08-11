using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BaggageDelivery.Api.Controllers;

[ApiController]
[Route("api/v1/antiforgery")]
[AllowAnonymous]
public sealed class AntiforgeryController(IAntiforgery antiforgery) : ControllerBase
{
    // Anonymous by design — the pax flow has no session to bind to, and the
    // request token is inert without the paired HttpOnly cookie token.
    [HttpGet("token")]
    public IActionResult Token()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);

        // The framework's own cookie stays HttpOnly; this readable companion carries
        // the request token so the SPA can echo it back as X-XSRF-TOKEN.
        Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            SameSite = SameSiteMode.Strict,
            Secure = Request.IsHttps
        });

        return NoContent();
    }
}
