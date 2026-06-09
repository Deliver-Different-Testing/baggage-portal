using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BaggageDelivery.Core.Models.Entities;

[Table("BagDelNotificationLog")]
public class BagDelNotificationLog
{
    [Key]
    public int Id { get; set; }

    public required int TokenId { get; set; }

    [MaxLength(10)]
    public required string Channel { get; set; }

    [MaxLength(200)]
    public required string Recipient { get; set; }

    [MaxLength(20)]
    public required string Status { get; set; }

    [MaxLength(100)]
    public string? ProviderMsgId { get; set; }

    public int AttemptCount { get; set; }

    public required DateTime NextAttemptUtc { get; set; }

    public string? LastError { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class NotificationChannel
{
    public const string Sms = "sms";
    public const string Email = "email";
}

public static class NotificationStatus
{
    public const string Pending = "Pending";
    public const string Sent = "Sent";
    public const string Failed = "Failed";
}
