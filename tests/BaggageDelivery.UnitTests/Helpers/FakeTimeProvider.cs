namespace BaggageDelivery.UnitTests.Helpers;

internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTime _now;
    public FakeTimeProvider(DateTime initial) => _now = DateTime.SpecifyKind(initial, DateTimeKind.Utc);
    public override DateTimeOffset GetUtcNow() => new(_now, TimeSpan.Zero);
    public void Advance(TimeSpan delta) => _now = _now.Add(delta);
}
