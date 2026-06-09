using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace BaggageDelivery.Api.Auth;

public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddAppAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSecret = Environment.GetEnvironmentVariable("JWTSecretKey")
                        ?? configuration["Auth:JwtSecretKey"]
                        ?? throw new InvalidOperationException("JWTSecretKey env var or Auth:JwtSecretKey config is required");
        var issuer = Environment.GetEnvironmentVariable("Issuer") ?? configuration["Auth:Issuer"] ?? "DespatchSC";
        var audience = Environment.GetEnvironmentVariable("Audience") ?? configuration["Auth:Audience"] ?? "DespatchSC";
        var cookieDomain = Environment.GetEnvironmentVariable("Domain");

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

        // Mirrors inboundagent: XSRF cookie shared across the deliverdifferent
        // app family. SPA reads XSRF-TOKEN and sends it as X-XSRF-TOKEN.
        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = "XSRF-TOKEN";
            options.Cookie.Domain = cookieDomain;
            options.HeaderName = "X-XSRF-TOKEN";
        });

        return services;
    }
}
