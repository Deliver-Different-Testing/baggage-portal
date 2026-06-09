using BaggageDelivery.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BaggageDelivery.Core.Models;

// Slim DbContext over the Despatch (DeliverDifferent) DB - only the tables this
// service writes to. Other Despatch tables are reached via Despatch WebAPICore.
public class BaggageDeliveryContext(DbContextOptions<BaggageDeliveryContext> options) : DbContext(options)
{
    public DbSet<BagDelBooking> Bookings => Set<BagDelBooking>();
    public DbSet<BagDelNotificationLog> NotificationLogs => Set<BagDelNotificationLog>();
    public DbSet<BagDelConfirmationOutbox> ConfirmationOutbox => Set<BagDelConfirmationOutbox>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BagDelBooking>(e =>
        {
            e.HasIndex(b => b.JobId).HasDatabaseName("IX_BagDelBooking_JobId");
            e.HasIndex(b => b.TenantId).HasDatabaseName("IX_BagDelBooking_TenantId");
        });

        modelBuilder.Entity<BagDelNotificationLog>(e =>
        {
            e.HasIndex(n => n.Status).HasDatabaseName("IX_BagDelNotificationLog_Status");
        });

        modelBuilder.Entity<BagDelConfirmationOutbox>(e =>
        {
            e.HasIndex(o => o.Status).HasDatabaseName("IX_BagDelConfirmationOutbox_Status");
        });
    }
}
