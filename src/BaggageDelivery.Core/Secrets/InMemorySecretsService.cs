using System.Collections.Concurrent;

namespace BaggageDelivery.Core.Secrets;

// Dev fallback. Backed by a process-local dictionary so we never accidentally
// commit credentials to disk. Production uses AwsSecretsService.
internal sealed class InMemorySecretsService : ISecretsService
{
    private readonly ConcurrentDictionary<string, string> _store = new(StringComparer.OrdinalIgnoreCase);

    public Task<string?> GetAsync(string name, CancellationToken ct)
    {
        return Task.FromResult(_store.TryGetValue(name, out var v) ? v : null);
    }

    public Task PutAsync(string name, string value, CancellationToken ct)
    {
        _store[name] = value;
        return Task.CompletedTask;
    }
}
