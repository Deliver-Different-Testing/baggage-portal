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

    public string EncryptId(int id)
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
                swEncrypt.Write(id.ToString(CultureInfo.InvariantCulture));
            }

            return Convert.ToBase64String(msEncrypt.ToArray())
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error encrypting ID: {Id}", id);
            throw new InvalidOperationException("Failed to encrypt ID", ex);
        }
    }

    public int? DecryptId(string? encryptedId)
    {
        if (string.IsNullOrEmpty(encryptedId))
        {
            return null;
        }

        try
        {
            var base64 = encryptedId.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }

            var encrypted = Convert.FromBase64String(base64);

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            using var msDecrypt = new MemoryStream(encrypted);
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using var srDecrypt = new StreamReader(csDecrypt);

            var decryptedString = srDecrypt.ReadToEnd();
            return int.TryParse(decryptedString, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                ? id
                : null;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to decrypt ID: {EncryptedId}", encryptedId);
            return null;
        }
    }
}
