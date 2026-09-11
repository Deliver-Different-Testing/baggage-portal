using BaggageDelivery.Core.Enums;
using BaggageDelivery.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BaggageDelivery.UnitTests.Helpers;

internal static class InMemoryDb
{
    private const int IdentityOffset = 100;

    public static BaggageDeliveryContext NewContext()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        connection.CreateFunction("getdate", () => DateTime.UtcNow);
        connection.CreateFunction("getutcdate", () => DateTime.UtcNow);
        connection.CreateFunction("sysutcdatetime", () => DateTime.UtcNow);

        var options = new DbContextOptionsBuilder<BaggageDeliveryContext>()
            .UseSqlite(connection)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;

        var ctx = new TestBaggageDeliveryContext(options);
        ctx.Database.EnsureCreated();
        SeedNoteTypes(ctx);
        return ctx;
    }

    public static int NoteTypeId(NoteType type) => (int)type + IdentityOffset;

    private static void SeedNoteTypes(BaggageDeliveryContext ctx)
    {
        ctx.TucNoteTypes.AddRange(Enum.GetValues<NoteType>().Select(t => new TucNoteType
        {
            NoteTypeId = NoteTypeId(t),
            NoteTypeName = NoteTypeNames.For(t),
            IsPublic = t is NoteType.PickupNotes or NoteType.DeliveryNotes,
            IsActive = true
        }));

        ctx.SaveChanges();
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

            // views are not created by EnsureCreated, so back them with a seedable table
            modelBuilder.Entity<TblJobSizeName>(entity =>
            {
                entity.HasKey(e => e.SizeId);
                entity.ToTable("tblJobSizeName");
            });
        }
    }
}
