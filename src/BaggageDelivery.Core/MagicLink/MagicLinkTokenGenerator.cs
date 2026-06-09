using System.Security.Cryptography;
using System.Text;

namespace BaggageDelivery.Core.MagicLink;

internal sealed class MagicLinkTokenGenerator : IMagicLinkTokenGenerator
{
    private const int RawByteLength = 32;

    public string GenerateRawToken()
    {
        Span<byte> buffer = stackalloc byte[RawByteLength];
        RandomNumberGenerator.Fill(buffer);
        return ToUrlSafeBase64(buffer);
    }

    public byte[] HashToken(string rawToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(rawToken);
        return SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
    }

    private static string ToUrlSafeBase64(ReadOnlySpan<byte> bytes)
    {
        var b64 = Convert.ToBase64String(bytes);
        // RFC 4648 §5 — URL-safe variant, padding stripped.
        return b64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
