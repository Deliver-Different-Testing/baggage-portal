using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace BaggageDelivery.Core.Secrets;

internal sealed class AwsSecretsService(IAmazonSecretsManager client) : ISecretsService
{
    public async Task<string?> GetAsync(string name, CancellationToken ct)
    {
        try
        {
            var response = await client.GetSecretValueAsync(new GetSecretValueRequest { SecretId = name }, ct);
            return response.SecretString;
        }
        catch (ResourceNotFoundException)
        {
            return null;
        }
    }

    public async Task PutAsync(string name, string value, CancellationToken ct)
    {
        try
        {
            await client.PutSecretValueAsync(new PutSecretValueRequest
            {
                SecretId = name,
                SecretString = value
            }, ct);
        }
        catch (ResourceNotFoundException)
        {
            await client.CreateSecretAsync(new CreateSecretRequest
            {
                Name = name,
                SecretString = value
            }, ct);
        }
    }
}
