using System.ComponentModel.DataAnnotations;

namespace BaggageDelivery.Api.DTOs.Admin;

public sealed record MintMagicLinkRequest(
    [Required, Range(1, int.MaxValue)] int JobId,
    [Required, Range(1, int.MaxValue)] int TenantId,
    [Required] string Scope,
    string? PassengerName,
    string? AirlineLabel,
    string? Reference,
    string? Phone,
    string? Email,
    [Required, RegularExpression("^(sms|email|both)$")] string Channel);

public sealed record MintMagicLinkResponse(
    string Token,
    string Url,
    DateTime ExpiresAtUtc,
    int TokenId);
