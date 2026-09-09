using BaggageDelivery.Core.Http.Models;

namespace BaggageDelivery.Core.Services;

public sealed record AddressContactRequestInput(
    int JobId,
    AddressUpdateDto RequestedAddress,
    string PassengerName,
    string? PassengerPhone,
    string? PassengerEmail);
