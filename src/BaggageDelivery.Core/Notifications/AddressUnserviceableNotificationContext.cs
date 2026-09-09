namespace BaggageDelivery.Core.Notifications;

public sealed record AddressUnserviceableNotificationContext(
    string Channel,
    string AirlineName,
    string JobNumber,
    string? FileReference,
    string PassengerName,
    string? PassengerPhone,
    string? PassengerEmail,
    string CurrentAddress,
    string RequestedAddress);
