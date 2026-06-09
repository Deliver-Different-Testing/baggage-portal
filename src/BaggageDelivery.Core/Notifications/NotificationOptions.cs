namespace BaggageDelivery.Core.Notifications;

public sealed class TwilioOptions
{
    public const string SectionName = "Twilio";

    public string AccountSid { get; set; } = "";
    public string AuthToken { get; set; } = "";
    public string FromNumber { get; set; } = "";
}

public sealed class SendGridOptions
{
    public const string SectionName = "SendGrid";

    public string ApiKey { get; set; } = "";
    public string FromAddress { get; set; } = "noreply@deliverdifferent.com";
    public string FromName { get; set; } = "Urgent Baggage Delivery";
}
