namespace BaggageDelivery.Core.Services;

public sealed class PaxServiceNotAllowedException(string message) : Exception(message);
