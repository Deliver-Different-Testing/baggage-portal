using BaggageDelivery.Core.Models.Entities;

namespace BaggageDelivery.Core.MagicLink;

public interface IMagicLinkService
{
    Task<MagicLinkMintResult> MintAsync(MagicLinkMintRequest request, CancellationToken ct);

    Task<MagicLinkVerifyResult> VerifyAsync(string rawToken, string scope, CancellationToken ct);

    Task RevokeAsync(string rawToken, CancellationToken ct);

    Task MarkUsedAsync(int tokenId, CancellationToken ct);
}

public sealed record MagicLinkMintRequest(
    int JobId,
    int TenantId,
    string Scope,
    string IssuedByService);

public sealed record MagicLinkMintResult(
    string RawToken,
    string Url,
    DateTime ExpiresAtUtc,
    int TokenId);

public sealed record MagicLinkVerifyResult(
    VerifyOutcome Outcome,
    BagDelMagicLinkToken? Token);

public enum VerifyOutcome
{
    Accepted,
    Expired,
    Revoked,
    AlreadyUsed,
    ScopeMismatch,
    Unknown
}
