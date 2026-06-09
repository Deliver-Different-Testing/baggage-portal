using BaggageDelivery.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BaggageDelivery.UnitTests.Helpers;

internal static class InMemoryDb
{
    // SQLite in-memory rather than the EF InMemory provider because the latter
    // does not support ExecuteUpdateAsync, which MagicLinkService uses for
    // single-statement mutations.
    //
    // BagDelConfirmationOutbox.RowVersion / BagDelNotificationLog.RowVersion are
    // mapped IsRowVersion (SQL Server stamps them). SQLite has no equivalent, so
    // we subclass the context to mark those properties as ValueGeneratedNever
    // (drops the store-generation expectation) and use an interceptor to stamp a
    // fresh byte[] on every insert/update — simulating SQL Server's behaviour.
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

            modelBuilder.Entity<BagDelConfirmationOutbox>()
                .Property(o => o.RowVersion)
                .ValueGeneratedNever();

            modelBuilder.Entity<BagDelNotificationLog>()
                .Property(n => n.RowVersion)
                .ValueGeneratedNever();
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
