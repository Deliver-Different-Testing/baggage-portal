using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BaggageDelivery.UnitTests.Services;

// The pax service tests use a fake calendar, so nothing else proves the
// scaffolded UTL_* DbFunctions actually translate. These build the real queries
// against the SqlServer provider and read the generated SQL — no connection is
// opened, so an untranslatable expression fails here instead of in production.
public class DespatchCalendarTests
{
    private static BaggageDeliveryContext SqlServerContext() =>
        new(new DbContextOptionsBuilder<BaggageDeliveryContext>()
            .UseSqlServer("Server=unused;Database=unused;Trusted_Connection=True;")
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options);

    [Fact]
    public void IsBusinessDay_translates_to_a_scalar_function_call()
    {
        using var db = SqlServerContext();
        var calendar = new DespatchCalendar(db);

        var sql = calendar.IsBusinessDayQuery(new DateTime(2026, 6, 13), clientId: 77).ToQueryString();

        Assert.Contains("UTL_IsBusinessDay", sql);
        // The SiteID argument must come off the client row, not be hardcoded.
        Assert.Contains("SiteID", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddBusinessDays_translates_to_a_scalar_function_call()
    {
        using var db = SqlServerContext();
        var calendar = new DespatchCalendar(db);

        var sql = calendar.AddBusinessDaysQuery(1, new DateTime(2026, 6, 13), clientId: 77).ToQueryString();

        Assert.Contains("UTL_AddBusinessDays", sql);
        Assert.Contains("SiteID", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NextBusinessDays_translates_to_a_single_batched_query()
    {
        using var db = SqlServerContext();
        var calendar = new DespatchCalendar(db);

        // One IQueryable for the whole walk, so this is one round trip by
        // construction — what needs proving is that it translates at all.
        var sql = calendar.NextBusinessDaysQuery(3, new DateTime(2026, 6, 13), clientId: 77)
            .ToQueryString();

        Assert.Contains("UTL_AddBusinessDays", sql);
        Assert.Contains("SiteID", sql, StringComparison.OrdinalIgnoreCase);
        // The offsets arrive as an unordered set, so the walk order has to be
        // restored in SQL rather than assumed.
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
    }
}
