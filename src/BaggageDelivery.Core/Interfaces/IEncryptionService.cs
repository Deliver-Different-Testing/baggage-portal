namespace BaggageDelivery.Core.Interfaces;

public interface IEncryptionService
{
    string EncryptId(int id);

    int? DecryptId(string? encryptedId);

    // Token used in passenger-facing URLs. Encrypts a composite (tenantId, jobId)
    // so the magic-link is keyed on the courier job — the same model inboundagent
    // uses — and the BagDelBooking shadow row can be upserted lazily on first hit.
    string EncryptToken(int tenantId, int jobId);

    BookingToken? DecryptToken(string? encryptedToken);
}

public readonly record struct BookingToken(int TenantId, int JobId);
