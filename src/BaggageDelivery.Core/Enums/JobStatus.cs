namespace BaggageDelivery.Core.Enums;

// Integer values for tucJob.UcjbStatus. Mirror of DespatchWeb.Enums.JobStatus
// — keep names in sync so cross-repo grep continues to work. Only the values
// BaggageDelivery sets or compares against are listed here; despatchweb owns
// the canonical set.
public enum JobStatus
{
    New = 0,
    Dispatched = 1,
    Completed = 6,
    Void = 1000
}
