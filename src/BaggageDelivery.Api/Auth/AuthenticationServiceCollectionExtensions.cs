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

        services.AddAuthentication(o =>
            {
                o.DefaultScheme = "AutoSelect";
                o.DefaultChallengeScheme = "AutoSelect";
            })
            .AddPolicyScheme("AutoSelect", "Magic-link or Bearer", o =>
            {
                o.ForwardDefaultSelector = ctx =>
                {
                    // Bearer (SC-JWT) for admin and internal routes; MagicLink for /api/v1/pax/*.
                    if (ctx.Request.Path.StartsWithSegments("/api/v1/pax"))
                    {
                        return MagicLinkSchemeOptions.SchemeName;
                    }
                    return JwtBearerDefaults.AuthenticationScheme;
                };
            })
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
            })
            .AddScheme<MagicLinkSchemeOptions, MagicLinkAuthenticationHandler>(
                MagicLinkSchemeOptions.SchemeName,
                _ => { });

        services.AddAuthorization(o =>
        {
            o.AddPolicy(MagicLinkPolicies.PaxConfirm, p =>
                p.AddAuthenticationSchemes(MagicLinkSchemeOptions.SchemeName)
                    .RequireAuthenticatedUser()
                    .RequireClaim(MagicLinkClaims.Scope, "Confirm"));

            o.AddPolicy(MagicLinkPolicies.PaxTrack, p =>
                p.AddAuthenticationSchemes(MagicLinkSchemeOptions.SchemeName)
                    .RequireAuthenticatedUser()
                    .RequireClaim(MagicLinkClaims.Scope, "Track"));
        });

        return services;
    }
}
