namespace BaggageDelivery.UnitTests.Helpers;

internal sealed class FakeTimeProvider(DateTime initial) : TimeProvider
{
    private readonly DateTime _now = DateTime.SpecifyKind(initial, DateTimeKind.Utc);
    public override DateTimeOffset GetUtcNow() => new(_now, TimeSpan.Zero);
}
