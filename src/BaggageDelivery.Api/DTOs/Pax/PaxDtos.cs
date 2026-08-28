using System.ComponentModel.DataAnnotations;

namespace BaggageDelivery.Api.DTOs.Pax;

public sealed record BookingSummaryDto(
    int JobId,
    string FileReference,
    string AirlineLabel,
    string? AirlineCode,
    string SupportPhone,
    string PassengerName,
    string? PassengerPhone,
    string? PassengerEmail,
    AddressDto DeliveryAddress,
    DateTime EarliestSlotUtc,
    DateTime LatestSlotUtc,
    AtlOptionDto[] AtlOptions,
    int? DefaultAtlOptionId);

public sealed record AtlOptionDto(int Id, string Name);

public sealed record AddressDto(
    [Required, MaxLength(200)] string Line1,
    [MaxLength(200)] string? Line2,
    [MaxLength(100)] string? Suburb,
    [Required, MaxLength(100)] string City,
    [MaxLength(20)] string? PostCode,
    [Required, MaxLength(100)] string Country,
    decimal? Latitude,
    decimal? Longitude);

public sealed record TimeSlotDto(
    Guid Id, DateTime RunUtc, string DayLabel, string Label, bool FirstAvailable);

public sealed record ConfirmBookingRequest(
    [Required] AddressDto Address,
    [Required] DateTime? DeliveryTimeUtc,
    int? AtlOptionId,
    [MaxLength(120)] string? AccessNotes,
    [Required, MaxLength(100)] string PassengerName,
    [MaxLength(40)] string? PassengerPhone,
    [MaxLength(100), EmailAddress] string? PassengerEmail);

public sealed record ConfirmBookingResponse(string Status, DateTime ReleasedAtUtc);
