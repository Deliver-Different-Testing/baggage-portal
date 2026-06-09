namespace BaggageDelivery.Core.Models;

// RowVersion (rowversion / timestamp) for optimistic concurrency on the
// outbox tables. SQL Server stamps a new value on every UPDATE, so two
// drainers racing on the same Pending row will produce a
// DbUpdateConcurrencyException on the second SaveChangesAsync — handled in
// the drain services by skipping the row this iteration.
//
// Lives outside the EF Power Tools scaffold so a regen doesn't strip it.
public partial class BagDelConfirmationOutbox
{
    public byte[] RowVersion { get; set; } = [];
}

public partial class BagDelNotificationLog
{
    public byte[] RowVersion { get; set; } = [];
}
