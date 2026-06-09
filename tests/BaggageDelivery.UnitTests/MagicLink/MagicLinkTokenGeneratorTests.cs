using BaggageDelivery.Core.MagicLink;
using Xunit;

namespace BaggageDelivery.UnitTests.MagicLink;

public class MagicLinkTokenGeneratorTests
{
    private readonly MagicLinkTokenGenerator _generator = new();

    [Fact]
    public void GenerateRawToken_returns_43_char_url_safe_string()
    {
        var token = _generator.GenerateRawToken();

        Assert.Equal(43, token.Length);
        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
    }

    [Fact]
    public void Two_tokens_are_different()
    {
        var a = _generator.GenerateRawToken();
        var b = _generator.GenerateRawToken();
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void HashToken_is_deterministic_and_32_bytes()
    {
        var hashA = _generator.HashToken("abc");
        var hashB = _generator.HashToken("abc");
        var hashC = _generator.HashToken("def");

        Assert.Equal(hashA, hashB);
        Assert.NotEqual(hashA, hashC);
        Assert.Equal(32, hashA.Length);
    }
}
