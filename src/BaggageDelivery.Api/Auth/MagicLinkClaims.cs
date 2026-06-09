namespace BaggageDelivery.Api.Auth;

public static class MagicLinkClaims
{
    public const string JobId = "bagdel.jobId";
    public const string TenantId = "bagdel.tenantId";
    public const string Scope = "bagdel.scope";
    public const string TokenId = "bagdel.tokenId";
}

public static class MagicLinkPolicies
{
    public const string PaxConfirm = "PaxConfirm";
    public const string PaxTrack = "PaxTrack";
}
