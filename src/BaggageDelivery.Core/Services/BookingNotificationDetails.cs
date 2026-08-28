namespace BaggageDelivery.Core.Services;

public sealed record BookingNotificationDetails(
    string FileReference,
    string AirlineLabel)
{
    public const string UnknownAirline = "Your Airline";
}
