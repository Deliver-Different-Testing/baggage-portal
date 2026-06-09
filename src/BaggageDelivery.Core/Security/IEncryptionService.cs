namespace BaggageDelivery.Core.Security;

public interface IEncryptionService
{
    string EncryptId(int id);

    int? DecryptId(string? encryptedId);
}
