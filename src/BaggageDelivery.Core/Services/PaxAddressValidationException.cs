namespace BaggageDelivery.Core.Services;

public sealed class PaxAddressValidationException(string message) : Exception(message);
