namespace BaggageDelivery.Core.Notifications;

public sealed class SnsOptions
{
    public const string SectionName = "Sns";

    // Optional alphanumeric sender ID shown on the recipient's handset where
    // the destination country supports it (NZ does; US does not). When empty
    // AWS uses its long-code pool.
    public string? SenderId { get; set; }

    // "Transactional" gives higher delivery priority and is the correct class
    // for one-shot magic links. "Promotional" is cheaper but lower priority.
    public string SmsType { get; set; } = "Transactional";
}

public sealed class SesOptions
{
    public const string SectionName = "Ses";

    public string FromAddress { get; set; } = "noreply@deliverdifferent.com";
    public string FromName { get; set; } = "Urgent Baggage Delivery";

    // Optional SES configuration set for event publishing (bounces/complaints
    // to SNS). Leave empty until we wire the feedback loop.
    public string? ConfigurationSetName { get; set; }
}
