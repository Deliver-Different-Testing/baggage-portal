namespace BaggageDelivery.Core.Services;

public sealed record BookingNotificationDetails(
    string FileReference,
    string AirlineLabel,
    string AirlineSmsName)
{
    public const string UnknownAirline = "Your Airline";
}
