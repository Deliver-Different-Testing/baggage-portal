using System.Globalization;
using System.Security.Cryptography;
using BaggageDelivery.Core.Interfaces;
using Microsoft.Extensions.Options;
using Serilog;

namespace BaggageDelivery.Core.Security;

// Mirror of inboundagent EncryptionService: AES-256-CBC with a deterministic IV
// (same input → same output). Keeps the URL shape stable across notifications,
// matching the InboundAgent customer-link model.
public sealed class EncryptionService : IEncryptionService
{
    private readonly byte[] _key;
    private readonly byte[] _iv;

    public EncryptionService(IOptions<EncryptionOptions> options)
    {
        var opts = options.Value;
        _key = Convert.FromBase64String(opts.Key);
        _iv = Convert.FromBase64String(opts.IV);
    }

    public string EncryptId(int id) => EncryptString(id.ToString(CultureInfo.InvariantCulture));

    public int? DecryptId(string? encryptedId)
    {
        var plaintext = DecryptToString(encryptedId);
        if (plaintext is null)
        {
            return null;
        }

        return int.TryParse(plaintext, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            ? id
            : null;
    }

    public string EncryptToken(int tenantId, int jobId)
    {
        var plain = string.Create(CultureInfo.InvariantCulture, $"{tenantId}:{jobId}");
        return EncryptString(plain);
    }

    public BookingToken? DecryptToken(string? encryptedToken)
    {
        var plaintext = DecryptToString(encryptedToken);
        if (plaintext is null)
        {
            return null;
        }

        var sep = plaintext.IndexOf(':');
        if (sep <= 0 || sep == plaintext.Length - 1)
        {
            return null;
        }

        var tenantSpan = plaintext.AsSpan(0, sep);
        var jobSpan = plaintext.AsSpan(sep + 1);

        if (!int.TryParse(tenantSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tenantId) ||
            !int.TryParse(jobSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out var jobId))
        {
            return null;
        }

        return new BookingToken(tenantId, jobId);
    }

    private string EncryptString(string plain)
    {
        try
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            using var msEncrypt = new MemoryStream();
            using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
            using (var swEncrypt = new StreamWriter(csEncrypt))
            {
                swEncrypt.Write(plain);
            }

            return Convert.ToBase64String(msEncrypt.ToArray())
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error encrypting token");
            throw new InvalidOperationException("Failed to encrypt token", ex);
        }
    }

    private string? DecryptToString(string? encrypted)
    {
        if (string.IsNullOrEmpty(encrypted))
        {
            return null;
        }

        try
        {
            var base64 = encrypted.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }

            var bytes = Convert.FromBase64String(base64);

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            using var msDecrypt = new MemoryStream(bytes);
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using var srDecrypt = new StreamReader(csDecrypt);

            return srDecrypt.ReadToEnd();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to decrypt token: {EncryptedToken}", encrypted);
            return null;
        }
    }
}
