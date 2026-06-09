using BaggageDelivery.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BaggageDelivery.UnitTests.Helpers;

internal static class InMemoryDb
{
    // SQLite in-memory rather than the EF InMemory provider because the latter
    // does not support ExecuteUpdateAsync, which MagicLinkService uses for
    // single-statement mutations.
    public static BaggageDeliveryContext NewContext()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<BaggageDeliveryContext>()
            .UseSqlite(connection)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;

        var ctx = new BaggageDeliveryContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }
}
