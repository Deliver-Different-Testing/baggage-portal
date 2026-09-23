using System.ComponentModel.DataAnnotations;

namespace BaggageDelivery.Api.DTOs.Admin;

public sealed record MintBookingLinkRequest(
    [Required, Range(1, int.MaxValue)] int JobId,
    string? PassengerName,
    string? AirlineLabel,
    string? Reference,
    string? Phone,
    string? Email,
    [Required, RegularExpression("^(sms|email|both)$")] string Channel);

public sealed record MintBookingLinkResponse(
    string EncryptedId,
    string ConfirmUrl,
    string? TrackUrl);
