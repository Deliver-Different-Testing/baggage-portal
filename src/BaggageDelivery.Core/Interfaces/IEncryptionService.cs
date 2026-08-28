namespace BaggageDelivery.Core.Interfaces;

public interface IEncryptionService
{
    string EncryptId(int id);

    int? DecryptId(string? encryptedId);
}
