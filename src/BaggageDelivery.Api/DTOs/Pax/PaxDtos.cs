using System.ComponentModel.DataAnnotations;

namespace BaggageDelivery.Api.DTOs.Pax;

public sealed record BookingSummaryDto(
    int JobId,
    string Reference,
    string AirlineLabel,
    string PassengerName,
    string? PassengerPhone,
    string? PassengerEmail,
    AddressDto DeliveryAddress,
    DateTime EarliestSlotUtc,
    DateTime LatestSlotUtc);

public sealed record AddressDto(
    [Required, MaxLength(200)] string Line1,
    [MaxLength(200)] string? Line2,
    [MaxLength(100)] string? Suburb,
    [Required, MaxLength(100)] string City,
    [MaxLength(20)] string? PostCode,
    [Required, StringLength(2, MinimumLength = 2)] string Country,
    decimal? Latitude,
    decimal? Longitude);

public sealed record TimeSlotDto(Guid Id, DateTime StartUtc, DateTime EndUtc, string Label, bool FirstAvailable);

public sealed record ConfirmBookingRequest(
    [Required] AddressDto Address,
    [Required] DateTime TimeSlotStartUtc,
    [Required] DateTime TimeSlotEndUtc,
    [Required, MaxLength(30)] string AtlOption,
    [MaxLength(500)] string? AccessNotes,
    [MaxLength(40)] string? PhoneOverride);

public sealed record ConfirmBookingResponse(string Status, DateTime ReleasedAtUtc);
