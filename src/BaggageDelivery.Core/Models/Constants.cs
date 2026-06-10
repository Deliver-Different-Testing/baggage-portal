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

public static class AtlOption
{
    public const string None = "None";
    public const string FrontDoor = "FrontDoor";
    public const string BackDoor = "BackDoor";
    public const string Garage = "Garage";
    public const string Reception = "Reception";
    public const string Neighbour = "Neighbour";
    public const string Other = "Other";
}
