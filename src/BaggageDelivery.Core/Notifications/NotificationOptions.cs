namespace BaggageDelivery.Core.Notifications;

public sealed class TwilioOptions
{
    public const string SectionName = "Twilio";

    public string AccountSid { get; set; } = "";
    public string AuthToken { get; set; } = "";
    public string FromNumber { get; set; } = "";
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
