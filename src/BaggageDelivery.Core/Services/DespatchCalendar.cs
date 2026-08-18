using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BaggageDelivery.Core.Services;

// Composes the scaffolded UTL_* scalar functions (BaggageDeliveryContext.Functions)
// into EF queries. They're anchored on the client row because the functions take
// the client's SiteID — the same row that supplies it, so this stays one round
// trip. Read-only, so no exposure to the legacy QUOTED_IDENTIFIER landmine.
internal sealed class DespatchCalendar(BaggageDeliveryContext db) : IDespatchCalendar
{
    private const string JobEntryType = "Local";

    // Exposed so a translation test can assert these compose into SQL instead of
    // throwing NotSupportedException — the unit tests otherwise only see the fake.
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

    // One row per requested offset, UNION ALL-ed into a single statement.
    // UTL_AddBusinessDays chains — the k-th business day after a date is the same
    // whether you take one k-step or k single steps — so the whole walk resolves
    // in one round trip instead of one per hop. Ordering by the date rather than
    // by the offset keeps the projection a bare scalar, which is what lets EF
    // apply a set operation to it at all.
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

        // UNION ALL makes no ordering promise, and the walk reads the days in
        // sequence. The chain is strictly ascending, so sorting by date restores it.
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

        // Same degradation as AddBusinessDaysAsync: a missing tail must not shorten
        // the chain, or the walk silently offers fewer windows.
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
