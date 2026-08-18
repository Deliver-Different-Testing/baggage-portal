namespace BaggageDelivery.Core.Interfaces;

// Business-day arithmetic delegated to Despatch's own UTL_* functions so
// holidays resolve exactly as they do for the rest of the stack. Behind an
// interface because those functions don't exist on the SQLite test database.
public interface IDespatchCalendar
{
    Task<bool> IsBusinessDayAsync(DateTime localDate, int clientId, CancellationToken ct);

    Task<DateTime> AddBusinessDaysAsync(int days, DateTime localDate, int clientId,
        CancellationToken ct);

    // The next `count` business days after localDate, ascending. Equivalent to
    // calling AddBusinessDaysAsync(1, ...) repeatedly, but in one round trip —
    // the timeslot walk needs up to fourteen hops and paying for each separately
    // is the slowest part of a passenger's first page load.
    Task<IReadOnlyList<DateTime>> NextBusinessDaysAsync(int count, DateTime localDate,
        int clientId, CancellationToken ct);
}
