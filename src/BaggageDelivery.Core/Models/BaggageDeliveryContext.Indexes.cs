using Microsoft.EntityFrameworkCore;

namespace BaggageDelivery.Core.Models;

// Index/concurrency tweaks that the EF Power Tools scaffold doesn't emit.
// Lives in a separate partial so a regen of BaggageDeliveryContext.cs
// preserves these changes.
public partial class BaggageDeliveryContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // Drain queries: WHERE Status='Pending' AND NextAttemptUtc <= now
        //                ORDER BY NextAttemptUtc.
        // Filtered indexes are safe here — BagDelConfirmationOutbox and
        // BagDelNotificationLog are new tables (no legacy QI-OFF procs touch
        // them), so the global "no filtered indexes on legacy tables" rule
        // does not apply.
        modelBuilder.Entity<BagDelConfirmationOutbox>()
            .HasIndex(o => o.NextAttemptUtc, "IX_BagDelConfirmationOutbox_Pending")
            .HasFilter("[Status] = 'Pending'");

        modelBuilder.Entity<BagDelNotificationLog>()
            .HasIndex(n => n.NextAttemptUtc, "IX_BagDelNotificationLog_Pending")
            .HasFilter("[Status] = 'Pending'");
    }
}
