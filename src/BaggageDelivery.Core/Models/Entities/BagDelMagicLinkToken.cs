using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BaggageDelivery.Core.Models.Entities;

[Table("BagDelMagicLinkToken")]
public class BagDelMagicLinkToken
{
    [Key]
    public int Id { get; set; }

    [MaxLength(32)]
    public required byte[] TokenHash { get; set; }

    public required int JobId { get; set; }

    public required int TenantId { get; set; }

    [MaxLength(20)]
    public required string Scope { get; set; }

    public required DateTime IssuedAtUtc { get; set; }

    public required DateTime ExpiresAtUtc { get; set; }

    public DateTime? UsedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    [MaxLength(50)]
    public required string IssuedByService { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class MagicLinkScope
{
    public const string Confirm = "Confirm";
    public const string Track = "Track";
}
