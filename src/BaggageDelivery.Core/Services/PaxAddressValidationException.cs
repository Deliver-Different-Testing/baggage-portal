namespace BaggageDelivery.Core.Services;

// Raised when a pax-submitted delivery address can't be accepted as-is and the
// passenger has to correct it. Carries a message written for the passenger, not
// for the log — the controller surfaces it as a 400 ValidationProblem.
public sealed class PaxAddressValidationException(string message) : Exception(message);
