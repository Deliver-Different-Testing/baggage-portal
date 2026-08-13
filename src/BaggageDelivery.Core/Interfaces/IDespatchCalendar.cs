namespace BaggageDelivery.Core.Interfaces;

// Business-day arithmetic delegated to Despatch's own UTL_* functions so
// holidays resolve exactly as they do for the rest of the stack. Behind an
// interface because those functions don't exist on the SQLite test database.
public interface IDespatchCalendar
{
    Task<bool> IsBusinessDayAsync(DateTime localDate, int clientId, CancellationToken ct);

    Task<DateTime> AddBusinessDaysAsync(int days, DateTime localDate, int clientId,
        CancellationToken ct);
}
