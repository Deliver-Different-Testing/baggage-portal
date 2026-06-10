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

            modelBuilder.Ignore<TucJob>();
            modelBuilder.Ignore<JobDeliveryJourney>();
            modelBuilder.Ignore<TblJobLeaveNotHome>();
        }
    }
}
