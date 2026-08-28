namespace BaggageDelivery.Core.Services;

public sealed class PaxAlreadyConfirmedException(int jobId)
    : Exception($"tucJob {jobId} has already been confirmed by the passenger");
