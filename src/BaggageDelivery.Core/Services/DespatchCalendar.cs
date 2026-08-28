using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BaggageDelivery.Core.Services;

internal sealed class DespatchCalendar(BaggageDeliveryContext db) : IDespatchCalendar
{
    private const string JobEntryType = "Local";

    internal IQueryable<bool?> IsBusinessDayQuery(DateTime localDate, int clientId)
    {
        var date = localDate.Date;
        return db.TucClients
            .AsNoTracking()
            .Where(c => c.UcclId == clientId)
            .Select(c => BaggageDeliveryContext.UTL_IsBusinessDay(date, c.SiteId, JobEntryType));
    }

    internal IQueryable<DateTime?> AddBusinessDaysQuery(int days, DateTime localDate, int clientId)
    {
        var date = localDate.Date;
        return db.TucClients
            .AsNoTracking()
            .Where(c => c.UcclId == clientId)
            .Select(c => BaggageDeliveryContext.UTL_AddBusinessDays(days, date, c.SiteId, JobEntryType));
    }

    internal IQueryable<DateTime?> NextBusinessDaysQuery(int count, DateTime localDate,
        int clientId)
    {
        var date = localDate.Date;

        IQueryable<DateTime?>? query = null;
        for (var offset = 1; offset <= count; offset++)
        {
            var n = offset;
            var hop = db.TucClients
                .AsNoTracking()
                .Where(c => c.UcclId == clientId)
                .Select(c => BaggageDeliveryContext.UTL_AddBusinessDays(n, date, c.SiteId,
                    JobEntryType));

            query = query is null ? hop : query.Concat(hop);
        }

        return query!.OrderBy(d => d);
    }

    public async Task<IReadOnlyList<DateTime>> NextBusinessDaysAsync(int count, DateTime localDate,
        int clientId, CancellationToken ct)
    {
        if (count <= 0)
        {
            return [];
        }

        var date = localDate.Date;
        var rows = await NextBusinessDaysQuery(count, date, clientId).ToListAsync(ct);

        var days = rows.Where(d => d is not null).Select(d => d!.Value.Date).ToList();
        if (days.Count == count)
        {
            return days;
        }

        Log.Warning(
            "UTL_AddBusinessDays returned {Returned} of {Requested} days for client {ClientId} "
            + "from {Date} — falling back to calendar days for the rest",
            days.Count, count, clientId, date);

        var previous = days.Count > 0 ? days[^1] : date;
        while (days.Count < count)
        {
            previous = previous.AddDays(1);
            days.Add(previous);
        }

        return days;
    }

    public async Task<bool> IsBusinessDayAsync(DateTime localDate, int clientId,
        CancellationToken ct)
    {
        var date = localDate.Date;
        var result = await IsBusinessDayQuery(date, clientId).FirstOrDefaultAsync(ct);

        if (result is null)
        {
            Log.Warning(
                "UTL_IsBusinessDay returned no value for client {ClientId} on {Date} — treating as a business day",
                clientId, date);
        }

        return result ?? true;
    }

    public async Task<DateTime> AddBusinessDaysAsync(int days, DateTime localDate, int clientId,
        CancellationToken ct)
    {
        var date = localDate.Date;
        var result = await AddBusinessDaysQuery(days, date, clientId).FirstOrDefaultAsync(ct);

        if (result is null)
        {
            Log.Warning(
                "UTL_AddBusinessDays returned no value for client {ClientId} on {Date} — falling back to calendar days",
                clientId, date);
        }

        return (result ?? date.AddDays(days)).Date;
    }
}
