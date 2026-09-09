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
    int BookingLeadTimeMinutes,
    BookingConfirmationDto? Confirmation);

public sealed record BookingConfirmationDto(
    DateTime ConfirmedAtUtc,
    DateTime? DeliveryTimeUtc,
    string DayLabel,
    string WindowLabel,
    int? AtlOptionId,
    string? AccessNotes,
    DateTime? EditableUntilUtc,
    bool CanEdit);

public sealed record AtlOptionDto(int Id, string Name);

public sealed record AddressDto(
    [MaxLength(255)] string? Line1,
    [MaxLength(255)] string? Line2,
    [Required, MaxLength(255)] string? Line3,
    [Required, MaxLength(255)] string Line4,
    [Required, MaxLength(255)] string Line5,
    [Required, MaxLength(255)] string Line6,
    [MaxLength(255)] string? Line7,
    [Required, MaxLength(100)] string Country,
    decimal? Latitude,
    decimal? Longitude);

public sealed record TimeSlotDto(
    Guid Id, DateTime RunUtc, string DayLabel, string Label, bool FirstAvailable);

public sealed record AvailableServiceDto(
    int JobTypeId,
    int? ScheduleId,
    string Name,
    string? Description,
    DateTime? BookDateUtc,
    int? DurationMinutes,
    bool IsScheduled);

public sealed record AvailableServicesResponse(
    AvailableServiceDto[] Services,
    bool NoServiceAvailable);

public sealed record AddressServicesRequest(
    [Required] AddressDto Address);

public sealed record AddressHelpRequest(
    [Required] AddressDto Address,
    [Required, MaxLength(100)] string PassengerName,
    [MaxLength(40)] string? PassengerPhone,
    [MaxLength(100), EmailAddress] string? PassengerEmail);

public sealed record AddressHelpResponse(bool Requested);

public sealed record ConfirmBookingRequest(
    [Required] AddressDto Address,
    [Required] DateTime? DeliveryTimeUtc,
    int? ServiceJobTypeId,
    int? AtlOptionId,
    [MaxLength(120)] string? AccessNotes,
    [Required, MaxLength(100)] string PassengerName,
    [MaxLength(40)] string? PassengerPhone,
    [MaxLength(100), EmailAddress] string? PassengerEmail);

public sealed record AmendBookingRequest(
    [Required] DateTime? DeliveryTimeUtc,
    int? AtlOptionId,
    [MaxLength(120)] string? AccessNotes,
    [Required, MaxLength(100)] string PassengerName,
    [MaxLength(40)] string? PassengerPhone,
    [MaxLength(100), EmailAddress] string? PassengerEmail);

public sealed record ConfirmBookingResponse(string Status, DateTime ReleasedAtUtc, string? TrackingUrl);
