namespace BaggageDelivery.Core.Interfaces;

public interface IDespatchCalendar
{
    Task<bool> IsBusinessDayAsync(DateTime localDate, int clientId, CancellationToken ct);

    Task<DateTime> AddBusinessDaysAsync(int days, DateTime localDate, int clientId,
        CancellationToken ct);

    Task<IReadOnlyList<DateTime>> NextBusinessDaysAsync(int count, DateTime localDate,
        int clientId, CancellationToken ct);
}
