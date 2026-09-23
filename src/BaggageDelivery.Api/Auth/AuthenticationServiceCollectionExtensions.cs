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

        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-XSRF-TOKEN";
            options.Cookie.SecurePolicy =
                isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });
    }
}
