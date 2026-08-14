using System.ComponentModel.DataAnnotations;

namespace BaggageDelivery.Api.DTOs.Pax;

public sealed record BookingSummaryDto(
    int JobId,
    string Reference,
    string AirlineLabel,
    string? AirlineCode,
    string SupportPhone,
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

// DayLabel is the date line ("Tomorrow, Fri 15 Aug"), Label the window
// ("9:00 AM – 12:00 PM"). Both are rendered in the tenant's timezone by
// PaxBookingService — see BookingTimeSlot.
public sealed record TimeSlotDto(
    Guid Id, DateTime RunUtc, string DayLabel, string Label, bool FirstAvailable);

// DeliveryTimeUtc is nullable so [Required] can actually reject a missing value.
// On a non-nullable DateTime the attribute is a no-op — an absent JSON property
// binds to default(DateTime), and 0001-01-01 is outside the range of the
// `datetime` column it lands in (tucJob.DeliverByTime), so the write blew up in
// SQL Server instead of being refused at the edge.
public sealed record ConfirmBookingRequest(
    [Required] AddressDto Address,
    [Required] DateTime? DeliveryTimeUtc,
    int? AtlOptionId,
    // These limits are the width of the tucJob columns ConfirmAsync writes them
    // to, not RFC maximums — anything longer is a SqlException, not a truncation.
    // ConfirmBookingRequestLengthTests pins them to the scaffolded model.
    [MaxLength(120)] string? AccessNotes,
    [Required, MaxLength(100)] string PassengerName,
    [MaxLength(40)] string? PassengerPhone,
    [MaxLength(100), EmailAddress] string? PassengerEmail);

public sealed record ConfirmBookingResponse(string Status, DateTime ReleasedAtUtc);
