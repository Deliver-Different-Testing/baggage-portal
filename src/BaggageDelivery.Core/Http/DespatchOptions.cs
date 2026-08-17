using BaggageDelivery.Core.Enums;

namespace BaggageDelivery.Core.Http;

public sealed class DespatchOptions
{
    public const string SectionName = "Despatch";

    public string TimeZone { get; set; } = string.Empty;

    public string[] Countries { get; set; } = ["NZ"];

    // Shown to the passenger as "Need help? Call …" when the job's client has no
    // phone of its own on tucClient. Optional — the portal hides the line rather
    // than printing a dead "Need help?".
    public string SupportPhone { get; set; } = string.Empty;

    // Authority-to-Leave handoff points the passenger flow must never offer.
    // Matched on tblJobLeaveNotHome.LeaveNotHomeId, so a tenant rewording the row
    // can't reintroduce it. A suitcase does not fit in a letter box.
    public LeaveNotHomeOption[] ExcludedAtlOptions { get; set; } =
    [
        LeaveNotHomeOption.LetterBox,
        LeaveNotHomeOption.Other
    ];

    // Preselected on the confirm page, which arrives with Authority to Leave on.
    // Deployments whose tblJobLeaveNotHome carries no such row fall back to the
    // first surviving option.
    public LeaveNotHomeOption DefaultAtlOption { get; set; } = LeaveNotHomeOption.FrontDoor;
}