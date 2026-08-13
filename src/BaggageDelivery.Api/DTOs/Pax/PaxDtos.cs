using System.ComponentModel.DataAnnotations;

namespace BaggageDelivery.Api.DTOs.Pax;

public sealed record BookingSummaryDto(
    int JobId,
    string Reference,
    string AirlineLabel,
    string? AirlineCode,
    string PassengerName,
    string? PassengerPhone,
    string? PassengerEmail,
    AddressDto DeliveryAddress,
    DateTime EarliestSlotUtc,
    DateTime LatestSlotUtc,
    AtlOptionDto[] AtlOptions);

public sealed record AtlOptionDto(int Id, string Name);

// Asymmetric by design: responses always carry an ISO-3166-1 alpha-2 Country,
// but requests must tolerate both the legacy free text we used to emit (still
// served from the 30-minute service-worker cache) and whatever spelling the
// passenger types into the Country field. CountryCodes is the single enforcement
// point; a length rule here would reject "New Zealand" before it can be resolved.
public sealed record AddressDto(
    [Required, MaxLength(200)] string Line1,
    [MaxLength(200)] string? Line2,
    [MaxLength(100)] string? Suburb,
    [Required, MaxLength(100)] string City,
    [MaxLength(20)] string? PostCode,
    [Required, MaxLength(100)] string Country,
    decimal? Latitude,
    decimal? Longitude);

public sealed record TimeSlotDto(Guid Id, DateTime RunUtc, string Label, bool FirstAvailable);

public sealed record ConfirmBookingRequest(
    [Required] AddressDto Address,
    [Required] DateTime DeliveryTimeUtc,
    int? AtlOptionId,
    [MaxLength(500)] string? AccessNotes,
    [Required, MaxLength(100)] string PassengerName,
    [MaxLength(40)] string? PassengerPhone,
    [MaxLength(254), EmailAddress] string? PassengerEmail);

public sealed record ConfirmBookingResponse(string Status, DateTime ReleasedAtUtc);
