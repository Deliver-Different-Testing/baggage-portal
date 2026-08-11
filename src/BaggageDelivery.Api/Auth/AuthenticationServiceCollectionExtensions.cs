using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace BaggageDelivery.Api.Auth;

public static class AuthenticationServiceCollectionExtensions
{
    public static void AddAppAuthentication(this IServiceCollection services, IConfiguration configuration,
        bool isDevelopment = false)
    {
        var jwtSecret = Environment.GetEnvironmentVariable("JWTSecretKey")
                        ?? configuration["Auth:JwtSecretKey"]
                        ?? throw new InvalidOperationException("JWTSecretKey env var or Auth:JwtSecretKey config is required");
        var issuer = Environment.GetEnvironmentVariable("Issuer") ?? configuration["Auth:Issuer"] ?? "DespatchSC";
        var audience = Environment.GetEnvironmentVariable("Audience") ?? configuration["Auth:Audience"] ?? "DespatchSC";

        // Bearer (SC-JWT) is the only registered scheme. Customer-facing pax
        // routes are [AllowAnonymous] and authenticate by holding the encrypted
        // BagDelBooking ID in the URL path — same model as inboundagent.
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o =>
            {
                o.RequireHttpsMetadata = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2)
                };
            });

        services.AddAuthorizationBuilder();

        // The cookie half of the token keeps its framework defaults: host-only,
        // HttpOnly, SameSite=Strict. It is deliberately NOT named XSRF-TOKEN and
        // NOT scoped to the shared Domain — that name is already taken by
        // inboundagent on the same parent domain, and the SPA must never see this
        // value. GET /api/v1/antiforgery/token issues the readable companion
        // cookie carrying the request token (see AntiforgeryController).
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-XSRF-TOKEN";
            options.Cookie.SecurePolicy =
                isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });
    }
}
