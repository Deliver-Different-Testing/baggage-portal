namespace BaggageDelivery.Core.MagicLink;

public interface IMagicLinkTokenGenerator
{
    string GenerateRawToken();

    byte[] HashToken(string rawToken);
}
