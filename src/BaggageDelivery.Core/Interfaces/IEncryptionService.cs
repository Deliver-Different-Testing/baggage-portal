namespace BaggageDelivery.Core.Interfaces;

public interface IEncryptionService
{
    // URL token used in passenger-facing links — encrypts the courier
    // JobId. Tenant identity comes from the deployment (matches inboundagent
    // and trackingpage); the BagDelBooking shadow row is upserted lazily on
    // first hit.
    string EncryptId(int id);

    int? DecryptId(string? encryptedId);
}
