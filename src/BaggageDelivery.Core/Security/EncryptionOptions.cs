using Microsoft.Extensions.Options;

namespace BaggageDelivery.Core.Security;

public sealed class EncryptionOptions
{
    public const string SectionName = "Encryption";

    public string Key { get; set; } = string.Empty;

    public string IV { get; set; } = string.Empty;
}

internal sealed class EncryptionOptionsValidator : IValidateOptions<EncryptionOptions>
{
    public ValidateOptionsResult Validate(string? name, EncryptionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Key))
        {
            return ValidateOptionsResult.Fail(
                "Encryption:Key (or BaggageDeliveryEncryptionKey env var) must be a base64 32-byte AES-256 key.");
        }

        if (string.IsNullOrWhiteSpace(options.IV))
        {
            return ValidateOptionsResult.Fail(
                "Encryption:IV (or BaggageDeliveryEncryptionIV env var) must be a base64 16-byte IV.");
        }

        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(options.Key);
        }
        catch (FormatException)
        {
            return ValidateOptionsResult.Fail("Encryption:Key is not valid base64.");
        }

        byte[] ivBytes;
        try
        {
            ivBytes = Convert.FromBase64String(options.IV);
        }
        catch (FormatException)
        {
            return ValidateOptionsResult.Fail("Encryption:IV is not valid base64.");
        }

        if (keyBytes.Length != 32)
        {
            return ValidateOptionsResult.Fail(
                $"Encryption:Key must decode to 32 bytes for AES-256 (got {keyBytes.Length}).");
        }

        if (ivBytes.Length != 16)
        {
            return ValidateOptionsResult.Fail(
                $"Encryption:IV must decode to 16 bytes for AES (got {ivBytes.Length}).");
        }

        return ValidateOptionsResult.Success;
    }
}
