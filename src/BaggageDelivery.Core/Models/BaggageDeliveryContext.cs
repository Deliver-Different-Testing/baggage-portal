using BaggageDelivery.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BaggageDelivery.Core.Models;

// Slim DbContext over the Despatch (DeliverDifferent) DB - only the tables this
// service writes to. Other Despatch tables are reached via Despatch WebAPICore.
public class BaggageDeliveryContext(DbContextOptions<BaggageDeliveryContext> options) : DbContext(options)
{
    public DbSet<BagDelMagicLinkToken> MagicLinkTokens => Set<BagDelMagicLinkToken>();
    public DbSet<BagDelBookingConfirmation> BookingConfirmations => Set<BagDelBookingConfirmation>();
    public DbSet<BagDelNotificationLog> NotificationLogs => Set<BagDelNotificationLog>();
    public DbSet<BagDelConfirmationOutbox> ConfirmationOutbox => Set<BagDelConfirmationOutbox>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BagDelMagicLinkToken>(e =>
        {
            e.HasIndex(t => t.TokenHash).HasDatabaseName("IX_BagDelMagicLinkToken_TokenHash");
            e.HasIndex(t => t.JobId).HasDatabaseName("IX_BagDelMagicLinkToken_JobId");
        });

        modelBuilder.Entity<BagDelBookingConfirmation>(e =>
        {
            e.HasIndex(c => c.JobId).HasDatabaseName("IX_BagDelBookingConfirmation_JobId");
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
