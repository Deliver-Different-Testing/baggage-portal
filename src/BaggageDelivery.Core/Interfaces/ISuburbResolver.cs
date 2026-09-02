namespace BaggageDelivery.Core.Interfaces;

public interface ISuburbResolver
{
    Task<int?> ResolveAsync(string? suburbName, string? postCode, CancellationToken ct);
}
