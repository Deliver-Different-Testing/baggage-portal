using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BaggageDelivery.UnitTests.Services;

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

        var sql = calendar.NextBusinessDaysQuery(3, new DateTime(2026, 6, 13), clientId: 77)
            .ToQueryString();

        Assert.Contains("UTL_AddBusinessDays", sql);
        Assert.Contains("SiteID", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
    }
}
