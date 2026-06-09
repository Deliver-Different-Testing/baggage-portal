using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BaggageDelivery.Core.MagicLink;

internal sealed class MagicLinkService(
    BaggageDeliveryContext db,
    IMagicLinkTokenGenerator generator,
    IOptions<MagicLinkOptions> options,
    ILogger<MagicLinkService> logger,
    TimeProvider time) : IMagicLinkService
{
    public async Task<MagicLinkMintResult> MintAsync(MagicLinkMintRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var raw = generator.GenerateRawToken();
        var hash = generator.HashToken(raw);
        var now = time.GetUtcNow().UtcDateTime;
        var ttlDays = request.Scope == MagicLinkScope.Confirm
            ? options.Value.ConfirmTtlDays
            : options.Value.TrackTtlDays;

        var entity = new BagDelMagicLinkToken
        {
            TokenHash = hash,
            JobId = request.JobId,
            TenantId = request.TenantId,
            Scope = request.Scope,
            IssuedAtUtc = now,
            ExpiresAtUtc = now.AddDays(ttlDays),
            IssuedByService = request.IssuedByService,
            CreatedAtUtc = now
        };
        db.MagicLinkTokens.Add(entity);
        await db.SaveChangesAsync(ct);

        var prefix = request.Scope == MagicLinkScope.Confirm ? "c" : "t";
        var url = $"{options.Value.PublicBaseUrl.TrimEnd('/')}/{prefix}/{raw}";

        logger.LogInformation(
            "MagicLink minted JobId={JobId} TenantId={TenantId} Scope={Scope} TokenId={TokenId} ExpiresAtUtc={ExpiresAtUtc}",
            request.JobId, request.TenantId, request.Scope, entity.Id, entity.ExpiresAtUtc);

        return new MagicLinkMintResult(raw, url, entity.ExpiresAtUtc, entity.Id);
    }

    public async Task<MagicLinkVerifyResult> VerifyAsync(string rawToken, string scope, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return new MagicLinkVerifyResult(VerifyOutcome.Unknown, null);
        }

        var hash = generator.HashToken(rawToken);
        // AsNoTracking so that ExecuteUpdate calls from other methods on the same
        // scoped DbContext are not masked by a stale tracked entity.
        var token = await db.MagicLinkTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (token is null)
        {
            return new MagicLinkVerifyResult(VerifyOutcome.Unknown, null);
        }

        if (token.RevokedAtUtc is not null)
        {
            return new MagicLinkVerifyResult(VerifyOutcome.Revoked, token);
        }

        var now = time.GetUtcNow().UtcDateTime;
        if (token.ExpiresAtUtc <= now)
        {
            return new MagicLinkVerifyResult(VerifyOutcome.Expired, token);
        }

        if (token.Scope != scope)
        {
            return new MagicLinkVerifyResult(VerifyOutcome.ScopeMismatch, token);
        }

        if (token.Scope == MagicLinkScope.Confirm && token.UsedAtUtc is not null)
        {
            return new MagicLinkVerifyResult(VerifyOutcome.AlreadyUsed, token);
        }

        // Slide expiry for tracking scope tokens (up to hard cap).
        if (token.Scope == MagicLinkScope.Track)
        {
            var hardCap = token.IssuedAtUtc.AddDays(options.Value.TrackHardCapDays);
            var slide = now.AddDays(options.Value.TrackTtlDays);
            var newExpiry = slide > hardCap ? hardCap : slide;
            if (newExpiry > token.ExpiresAtUtc)
            {
                await db.MagicLinkTokens
                    .Where(t => t.Id == token.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiresAtUtc, newExpiry), ct);
                token.ExpiresAtUtc = newExpiry;
            }
        }

        return new MagicLinkVerifyResult(VerifyOutcome.Accepted, token);
    }

    public async Task RevokeAsync(string rawToken, CancellationToken ct)
    {
        var hash = generator.HashToken(rawToken);
        var now = time.GetUtcNow().UtcDateTime;
        await db.MagicLinkTokens
            .Where(t => t.TokenHash == hash && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), ct);
    }

    public async Task MarkUsedAsync(int tokenId, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        await db.MagicLinkTokens
            .Where(t => t.Id == tokenId && t.UsedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAtUtc, now), ct);
    }
}
