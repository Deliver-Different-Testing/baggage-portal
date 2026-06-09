using Microsoft.AspNetCore.Authentication;

namespace BaggageDelivery.Api.Auth;

public class MagicLinkSchemeOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "MagicLink";

    public string CookieName { get; set; } = "bagdel.sess";

    public int CookieTtlMinutes { get; set; } = 15;

    public string TokenQueryParam { get; set; } = "token";
}
