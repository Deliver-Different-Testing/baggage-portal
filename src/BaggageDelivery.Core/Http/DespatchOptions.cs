using BaggageDelivery.Core.Enums;

namespace BaggageDelivery.Core.Http;

public sealed class DespatchOptions
{
    public const string SectionName = "Despatch";

    public string TimeZone { get; set; } = string.Empty;

    public string[] Countries { get; set; } = ["NZ"];

    public string SupportPhone { get; set; } = string.Empty;

    public string NotificationReplyToEmail { get; set; } = "baggage@urgent.co.nz";

    public LeaveNotHomeOption[] ExcludedAtlOptions { get; set; } =
    [
        LeaveNotHomeOption.LetterBox,
        LeaveNotHomeOption.Other
    ];

    public LeaveNotHomeOption DefaultAtlOption { get; set; } = LeaveNotHomeOption.FrontDoor;

    public int BookingLeadTimeMinutes { get; set; } = 30;
}