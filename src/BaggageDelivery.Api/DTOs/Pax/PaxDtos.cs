using System.ComponentModel.DataAnnotations;

namespace BaggageDelivery.Api.DTOs.Pax;

public sealed record BookingSummaryDto(
    int JobId,
    string JobNumber,
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
    int? DefaultAtlOptionId,
    bool TrackingAvailable,
    string? TrackingUrl,
    BookingConfirmationDto? Confirmation);

public sealed record BookingConfirmationDto(
    DateTime ConfirmedAtUtc,
    DateTime? DeliveryTimeUtc,
    string DayLabel,
    string WindowLabel,
    int? AtlOptionId,
    string? AccessNotes);

public sealed record AtlOptionDto(int Id, string Name);

public sealed record AddressDto(
    [MaxLength(255)] string? Line1,
    [MaxLength(255)] string? Line2,
    [MaxLength(255)] string? Line3,
    [Required, MaxLength(255)] string Line4,
    [Required, MaxLength(255)] string Line5,
    [Required, MaxLength(255)] string Line6,
    [MaxLength(255)] string? Line7,
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

public sealed record ConfirmBookingResponse(string Status, DateTime ReleasedAtUtc, string? TrackingUrl);
