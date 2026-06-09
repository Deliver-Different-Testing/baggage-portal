namespace BaggageDelivery.Core.Interfaces;

public interface ISecretsService
{
    Task<string?> GetAsync(string name, CancellationToken ct);

    Task PutAsync(string name, string value, CancellationToken ct);
}
