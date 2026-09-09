namespace BaggageDelivery.Core.Services;

public sealed record AvailableServicesRequest(
    int ClientId,
    string? SizeName,
    string? FromSuburb,
    int? FromPostCode,
    string? ToSuburb,
    int? ToPostCode,
    decimal? ToLatitude,
    decimal? ToLongitude,
    DateTime AsOfLocal);
