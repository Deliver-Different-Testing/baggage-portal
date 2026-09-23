using System.Security.Cryptography;
using BaggageDelivery.Core.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace BaggageDelivery.UnitTests.Security;

public class EncryptionServiceTests
{
    private static EncryptionService BuildService()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var iv = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var opts = Options.Create(new EncryptionOptions { Key = key, IV = iv });
        return new EncryptionService(opts);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(int.MaxValue)]
    [InlineData(123456789)]
    public void Round_trip_returns_original_id(int id)
    {
        var svc = BuildService();
        var encrypted = svc.EncryptId(id);
        Assert.Equal(id, svc.DecryptId(encrypted));
    }

    [Fact]
    public void Encrypt_is_deterministic_for_same_input()
    {
        var svc = BuildService();
        var a = svc.EncryptId(42);
        var b = svc.EncryptId(42);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Encrypted_output_is_url_safe()
    {
        var svc = BuildService();
        var encrypted = svc.EncryptId(42);
        Assert.DoesNotContain('+', encrypted);
        Assert.DoesNotContain('/', encrypted);
        Assert.DoesNotContain('=', encrypted);
    }

    [Fact]
    public void Decrypt_returns_null_for_empty_input()
    {
        var svc = BuildService();
        Assert.Null(svc.DecryptId(null));
        Assert.Null(svc.DecryptId(""));
    }

    [Fact]
    public void Decrypt_returns_null_for_malformed_input()
    {
        var svc = BuildService();
        Assert.Null(svc.DecryptId("not-valid-base64!!"));
        Assert.Null(svc.DecryptId("abcdef"));
    }

    [Fact]
    public void Decrypt_returns_null_when_key_does_not_match()
    {
        var svc1 = BuildService();
        var svc2 = BuildService();
        var encrypted = svc1.EncryptId(42);
        Assert.Null(svc2.DecryptId(encrypted));
    }
}

public class EncryptionOptionsValidatorTests
{
    private static EncryptionOptionsValidator NewValidator() => new();

    [Fact]
    public void Fails_when_key_empty()
    {
        var result = NewValidator().Validate(null, new EncryptionOptions { Key = "", IV = "AAAAAAAAAAAAAAAAAAAAAA==" });
        Assert.True(result.Failed);
    }

    [Fact]
    public void Fails_when_key_wrong_length()
    {
        var shortKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var iv = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var result = NewValidator().Validate(null, new EncryptionOptions { Key = shortKey, IV = iv });
        Assert.True(result.Failed);
    }

    [Fact]
    public void Fails_when_iv_wrong_length()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var shortIv = Convert.ToBase64String(RandomNumberGenerator.GetBytes(8));
        var result = NewValidator().Validate(null, new EncryptionOptions { Key = key, IV = shortIv });
        Assert.True(result.Failed);
    }

    [Fact]
    public void Succeeds_for_valid_key_and_iv()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var iv = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var result = NewValidator().Validate(null, new EncryptionOptions { Key = key, IV = iv });
        Assert.True(result.Succeeded);
    }
}
