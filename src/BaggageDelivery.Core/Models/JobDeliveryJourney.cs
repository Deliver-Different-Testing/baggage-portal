#nullable disable
using System;
using System.Collections.Generic;

namespace BaggageDelivery.Core.Models;

public partial class JobDeliveryJourney
{
    public int JourneyId { get; set; }

    public int JobId { get; set; }

    public string ChangeType { get; set; }

    public int? OldInternalStatusId { get; set; }

    public int? NewInternalStatusId { get; set; }

    public int? OldJobStatusId { get; set; }

    public int? NewJobStatusId { get; set; }

    public int? FlightId { get; set; }

    public int? OldAgentId { get; set; }

    public int? NewAgentId { get; set; }

    public int? OldCourierId { get; set; }

    public int? NewCourierId { get; set; }

    public string FieldName { get; set; }

    public string OldValue { get; set; }

    public string NewValue { get; set; }

    public int? StaffId { get; set; }

    public int? CourierId { get; set; }

    public string UpdatedByType { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string Comments { get; set; }

    public virtual TucJob Job { get; set; }
}