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
