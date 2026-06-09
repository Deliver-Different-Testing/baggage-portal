using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace BaggageDelivery.Core.Security;

// Mirror of the SC-JWT minting logic from IntegrationManager - same claim
// shape, same encrypted "SC" payload, so Despatch WebAPICore accepts both
// services' tokens identically.
public static class AuthenticationExtensions
{
    private const int MinimumKeyLengthBytes = 32;

    public static JwtSecurityToken CreateApiToken(string name, int tenantId, string connection, string timeZone,
        int? clientId = null, int? contactId = null)
    {
        try
        {
            var jwtSecretKey = Environment.GetEnvironmentVariable("JWTSecretKey")
                ?? throw new InvalidOperationException(
                    "JWTSecretKey environment variable is not set. Cannot create secure tokens.");

            var keyBytes = Encoding.UTF8.GetBytes(jwtSecretKey);
            if (keyBytes.Length < MinimumKeyLengthBytes)
            {
                throw new InvalidOperationException(
                    $"JWTSecretKey must be at least {MinimumKeyLengthBytes} bytes (256 bits) for secure token signing.");
            }

            var symmetricSecurityKey = new SymmetricSecurityKey(keyBytes);

            var sensitiveClaims = new Dictionary<string, string>
            {
                ["TenantId"] = tenantId.ToString(),
                ["Connection"] = connection,
                ["TimeZone"] = timeZone
            };

            if (clientId.HasValue)
            {
                sensitiveClaims["ClientId"] = clientId.Value.ToString();
                sensitiveClaims["SubAccounts"] = string.Empty;
            }

            sensitiveClaims["ContactId"] = (contactId ?? 0).ToString();

            var sensitiveClaimsJson = JsonSerializer.Serialize(sensitiveClaims);
            var encryptedClaims = EncryptClaims(sensitiveClaimsJson,
                Environment.GetEnvironmentVariable("ClaimsKey")
                ?? throw new InvalidOperationException("ClaimsKey env var is required"));

            var claims = new Claim[]
            {
                new(ClaimTypes.Name, name),
                new("SC", encryptedClaims)
            };

            return new JwtSecurityToken(
                issuer: Environment.GetEnvironmentVariable("Issuer"),
                audience: Environment.GetEnvironmentVariable("Audience"),
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: new SigningCredentials(symmetricSecurityKey, SecurityAlgorithms.HmacSha256)
            );
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to create SC-JWT for Despatch call: {Message}",
                e.InnerException?.Message ?? e.Message);
            throw;
        }
    }

    private static string EncryptClaims(string claims, string key)
    {
        using var aesAlg = Aes.Create();
        aesAlg.Key = Convert.FromBase64String(key);
        aesAlg.GenerateIV();
        var iv = aesAlg.IV;

        using var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
        using var msEncrypt = new MemoryStream();
        msEncrypt.Write(iv, 0, iv.Length);
        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
        using (var swEncrypt = new StreamWriter(csEncrypt))
        {
            swEncrypt.Write(claims);
        }

        return Convert.ToBase64String(msEncrypt.ToArray());
    }
}
