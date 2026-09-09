namespace BaggageDelivery.Core.Services;

public sealed class PaxAmendWindowClosedException(int jobId)
    : Exception($"Job {jobId} can no longer be changed by the passenger")
{
    public int JobId { get; } = jobId;
}
