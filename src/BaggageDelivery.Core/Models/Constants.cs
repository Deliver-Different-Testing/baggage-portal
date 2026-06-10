 namespace BaggageDelivery.Core.Models;

 public static class NotificationStatus
{
    public const string Pending = "Pending";
    public const string Sent = "Sent";
    public const string Failed = "Failed";
}

public static class NotificationChannel
{
    public const string Sms = "sms";
    public const string Email = "email";
}
