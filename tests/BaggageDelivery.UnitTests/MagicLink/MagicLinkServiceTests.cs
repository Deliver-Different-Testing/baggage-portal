using BaggageDelivery.Core.MagicLink;
using BaggageDelivery.Core.Models.Entities;
using BaggageDelivery.UnitTests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BaggageDelivery.UnitTests.MagicLink;

public class MagicLinkServiceTests
{
    private static MagicLinkService BuildService(FakeTimeProvider time, out IMagicLinkTokenGenerator gen,
        out BaggageDelivery.Core.Models.BaggageDeliveryContext db)
    {
        db = InMemoryDb.NewContext();
        gen = new MagicLinkTokenGenerator();
        var opts = Options.Create(new MagicLinkOptions
        {
            ConfirmTtlDays = 7,
            TrackTtlDays = 7,
            TrackHardCapDays = 30,
            PublicBaseUrl = "https://baggage.example",
            IssuerName = "BaggageDelivery"
        });
        return new MagicLinkService(db, gen, opts, NullLogger<MagicLinkService>.Instance, time);
    }

    [Fact]
    public async Task Mint_then_verify_returns_Accepted()
    {
        var time = new FakeTimeProvider(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var svc = BuildService(time, out _, out _);

        var mint = await svc.MintAsync(
            new MagicLinkMintRequest(JobId: 100, TenantId: 1, Scope: MagicLinkScope.Confirm, IssuedByService: "test"),
            CancellationToken.None);

        var verify = await svc.VerifyAsync(mint.RawToken, MagicLinkScope.Confirm, CancellationToken.None);

        Assert.Equal(VerifyOutcome.Accepted, verify.Outcome);
        Assert.NotNull(verify.Token);
        Assert.Equal(100, verify.Token!.JobId);
        Assert.StartsWith("https://baggage.example/c/", mint.Url);
    }

    [Fact]
    public async Task Verify_after_expiry_returns_Expired()
    {
        var time = new FakeTimeProvider(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var svc = BuildService(time, out _, out _);

        var mint = await svc.MintAsync(
            new MagicLinkMintRequest(1, 1, MagicLinkScope.Confirm, "test"), CancellationToken.None);

        time.Advance(TimeSpan.FromDays(8));

        var verify = await svc.VerifyAsync(mint.RawToken, MagicLinkScope.Confirm, CancellationToken.None);

        Assert.Equal(VerifyOutcome.Expired, verify.Outcome);
    }

    [Fact]
    public async Task Verify_with_wrong_scope_returns_ScopeMismatch()
    {
        var time = new FakeTimeProvider(DateTime.UtcNow);
        var svc = BuildService(time, out _, out _);

        var mint = await svc.MintAsync(
            new MagicLinkMintRequest(1, 1, MagicLinkScope.Confirm, "test"), CancellationToken.None);

        var verify = await svc.VerifyAsync(mint.RawToken, MagicLinkScope.Track, CancellationToken.None);

        Assert.Equal(VerifyOutcome.ScopeMismatch, verify.Outcome);
    }

    [Fact]
    public async Task Confirm_scope_token_used_twice_returns_AlreadyUsed()
    {
        var time = new FakeTimeProvider(DateTime.UtcNow);
        var svc = BuildService(time, out _, out _);

        var mint = await svc.MintAsync(
            new MagicLinkMintRequest(1, 1, MagicLinkScope.Confirm, "test"), CancellationToken.None);

        await svc.MarkUsedAsync(mint.TokenId, CancellationToken.None);

        var verify = await svc.VerifyAsync(mint.RawToken, MagicLinkScope.Confirm, CancellationToken.None);

        Assert.Equal(VerifyOutcome.AlreadyUsed, verify.Outcome);
    }

    [Fact]
    public async Task Revoke_then_verify_returns_Revoked()
    {
        var time = new FakeTimeProvider(DateTime.UtcNow);
        var svc = BuildService(time, out _, out _);

        var mint = await svc.MintAsync(
            new MagicLinkMintRequest(1, 1, MagicLinkScope.Confirm, "test"), CancellationToken.None);

        await svc.RevokeAsync(mint.RawToken, CancellationToken.None);

        var verify = await svc.VerifyAsync(mint.RawToken, MagicLinkScope.Confirm, CancellationToken.None);

        Assert.Equal(VerifyOutcome.Revoked, verify.Outcome);
    }

    [Fact]
    public async Task Unknown_token_returns_Unknown()
    {
        var time = new FakeTimeProvider(DateTime.UtcNow);
        var svc = BuildService(time, out _, out _);

        var verify = await svc.VerifyAsync("not-a-real-token", MagicLinkScope.Confirm, CancellationToken.None);

        Assert.Equal(VerifyOutcome.Unknown, verify.Outcome);
    }

    [Fact]
    public async Task Track_scope_verify_slides_expiry()
    {
        var time = new FakeTimeProvider(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var svc = BuildService(time, out _, out var db);

        var mint = await svc.MintAsync(
            new MagicLinkMintRequest(1, 1, MagicLinkScope.Track, "test"), CancellationToken.None);

        time.Advance(TimeSpan.FromDays(5));

        var verify = await svc.VerifyAsync(mint.RawToken, MagicLinkScope.Track, CancellationToken.None);

        Assert.Equal(VerifyOutcome.Accepted, verify.Outcome);
        var token = db.MagicLinkTokens.AsQueryable().First();
        var expectedSlide = time.GetUtcNow().UtcDateTime.AddDays(7);
        Assert.True(token.ExpiresAtUtc >= expectedSlide.AddSeconds(-1));
    }
}

internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTime _now;
    public FakeTimeProvider(DateTime initial) => _now = DateTime.SpecifyKind(initial, DateTimeKind.Utc);
    public override DateTimeOffset GetUtcNow() => new(_now, TimeSpan.Zero);
    public void Advance(TimeSpan delta) => _now = _now.Add(delta);
}
