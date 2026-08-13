using BaggageDelivery.Core.Http.Models;

namespace BaggageDelivery.Core.Services;

public sealed record ConfirmBookingInput(
    int JobId,
    AddressUpdateDto Address,
    DateTime DeliveryTimeUtc,
    int? AtlOptionId,
    string? AccessNotes,
    string PassengerName,
    string? PassengerPhone,
    string? PassengerEmail);
