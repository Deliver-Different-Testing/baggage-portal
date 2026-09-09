namespace BaggageDelivery.Core.Services;

public sealed class PaxNotConfirmedException(int jobId)
    : Exception($"Job {jobId} has no passenger confirmation to change")
{
    public int JobId { get; } = jobId;
}
