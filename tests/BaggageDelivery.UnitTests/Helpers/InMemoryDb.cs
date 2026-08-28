using BaggageDelivery.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BaggageDelivery.UnitTests.Helpers;

internal static class InMemoryDb
{
    public static BaggageDeliveryContext NewContext()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        connection.CreateFunction("getdate", () => DateTime.UtcNow);
        connection.CreateFunction("getutcdate", () => DateTime.UtcNow);

        var options = new DbContextOptionsBuilder<BaggageDeliveryContext>()
            .UseSqlite(connection)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;

        var ctx = new TestBaggageDeliveryContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private sealed class TestBaggageDeliveryContext(DbContextOptions<BaggageDeliveryContext> options)
        : BaggageDeliveryContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var ix in entityType.GetIndexes().Where(i => i.GetFilter() is not null).ToList())
                {
                    entityType.RemoveIndex(ix.Properties);
                }
            }
        }
    }
}
