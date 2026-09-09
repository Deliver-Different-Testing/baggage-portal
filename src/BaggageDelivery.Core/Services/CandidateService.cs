namespace BaggageDelivery.Core.Services;

public sealed record CandidateService(
    int JobTypeId,
    int? ScheduleId,
    string Name,
    string? SystemName,
    string? Description,
    string Availability,
    DateTime? BookDateUtc,
    int? DurationMinutes,
    string? AvailabilityColour = null)
{
    public bool IsScheduled => ScheduleId is not null;

    // Underlying speed behind a composite scheduleId*1000+speedId key
    public int SpeedId => IsScheduled ? JobTypeId % 1000 : JobTypeId;
}
