using BaggageDelivery.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BaggageDelivery.UnitTests.Helpers;

internal static class InMemoryDb
{
    // SQLite in-memory rather than the EF InMemory provider because the latter
    // does not support ExecuteUpdateAsync, which our drain services use for
    // single-statement mutations.
    //
    // BagDelNotificationLog.RowVersion is mapped IsRowVersion (SQL Server
    // stamps it). SQLite has no equivalent, so we subclass the context to
    // mark the property as ValueGeneratedNever and use an interceptor to
    // stamp a fresh byte[] on every insert/update.
    //
    // The Despatch DB tables we scaffolded into the context for reads
    // (tucJob, JobDeliveryJourney, tblUndeliverableLocation) are .Ignore()'d
    // in the test context — they're SELECT-only via api/trackingpage in
    // production and unit tests have no business creating their schema in
    // SQLite (the real tucJob schema has triggers, foreign keys, and types
    // SQLite can't model anyway).
    public static BaggageDeliveryContext NewContext()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<BaggageDeliveryContext>()
            .UseSqlite(connection)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .AddInterceptors(new RowVersionStampInterceptor())
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

            modelBuilder.Entity<BagDelNotificationLog>()
                .Property(n => n.RowVersion)
                .ValueGeneratedNever();

            modelBuilder.Ignore<TucJob>();
            modelBuilder.Ignore<JobDeliveryJourney>();
            modelBuilder.Ignore<TblUndeliverableLocation>();
        }
    }

    private sealed class RowVersionStampInterceptor : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData, InterceptionResult<int> result)
        {
            Stamp(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            Stamp(eventData.Context);
            return base.SavingChangesAsync(eventData, result, ct);
        }

        private static void Stamp(DbContext? ctx)
        {
            if (ctx is null) return;
            foreach (var entry in ctx.ChangeTracker.Entries())
            {
                if (entry.State is not EntityState.Added and not EntityState.Modified)
                {
                    continue;
                }

                StampIfPresent(entry, "RowVersion");
            }
        }

        private static void StampIfPresent(EntityEntry entry, string propertyName)
        {
            var prop = entry.Metadata.FindProperty(propertyName);
            if (prop?.ClrType != typeof(byte[]))
            {
                return;
            }
            entry.Property(propertyName).CurrentValue = Guid.NewGuid().ToByteArray();
        }
    }
}
