using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BaggageDelivery.Core.Models.Entities;

[Table("BagDelConfirmationOutbox")]
public class BagDelConfirmationOutbox
{
    [Key]
    public int Id { get; set; }

    public required int ConfirmationId { get; set; }

    public required int JobId { get; set; }

    public required int TenantId { get; set; }

    [MaxLength(20)]
    public required string Status { get; set; }

    public int AttemptCount { get; set; }

    public required DateTime NextAttemptUtc { get; set; }

    public string? LastError { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class OutboxStatus
{
    public const string Pending = "Pending";
    public const string Done = "Done";
    public const string Failed = "Failed";
}
