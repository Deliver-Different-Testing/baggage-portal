namespace BaggageDelivery.Core.Services;

public sealed record AmendBookingInput(
    int JobId,
    DateTime DeliveryTimeUtc,
    int? AtlOptionId,
    string? AccessNotes,
    string PassengerName,
    string? PassengerPhone,
    string? PassengerEmail);
