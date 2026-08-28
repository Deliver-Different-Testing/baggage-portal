#nullable disable
using System;
using System.Collections.Generic;

namespace BaggageDelivery.Core.Models;

public partial class TucJobType
{
    public int UcjtId { get; set; }

    public string UcjtName { get; set; }

    public string ShortName { get; set; }

    public string UcjtDescription { get; set; }

    public decimal UcjtBaseRate { get; set; }

    public decimal UcjtUnitRate { get; set; }

    public string UcjtCode { get; set; }

    public int? Minutes { get; set; }

    public int? DeliveryMargin { get; set; }

    public string JobLetter { get; set; }

    public int? PickupTime { get; set; }

    public int? DeliveryTime { get; set; }

    public bool JobEntry { get; set; }

    public bool NationwideEntry { get; set; }

    public bool DatacomEntry { get; set; }

    public bool WebJobEntry { get; set; }

    public decimal? CourierPercentage { get; set; }

    public decimal? SuccessRate { get; set; }

    public DateTime Created { get; set; }

    public string CreatedBy { get; set; }

    public DateTime LastModified { get; set; }

    public string LastModifiedBy { get; set; }

    public string Notes { get; set; }

    public string SystemName { get; set; }

    public bool WebServiceEntry { get; set; }

    public int? SplitJobDeliveryTime { get; set; }

    public bool? TruckJobEntry { get; set; }

    public string ExtraName { get; set; }

    public string Alias { get; set; }

    public bool? ZoneRated { get; set; }

    public int? ServiceTrackingId { get; set; }

    public decimal? AddonPercentage { get; set; }

    public bool? Mfv { get; set; }

    public bool? Faf { get; set; }

    public int? UcjtClientId { get; set; }

    public int GroupingId { get; set; }

    public int? LabelId { get; set; }

    public bool ShowPhotosWhenChild { get; set; }

    public bool AutoDispatchEnabled { get; set; }

    public bool Routed { get; set; }

    public int? PickupWindowMinutesBefore { get; set; }

    public int? PickupWindowMinutesAfter { get; set; }

    public int? DeliveryWindowMinutesBefore { get; set; }

    public int? DeliveryWindowMinutesAfter { get; set; }

    public string ServiceType { get; set; }

    public string ServiceDescription { get; set; }

    public virtual ICollection<TucJob> TucJobAcceptedJobTypes { get; set; } = new List<TucJob>();

    public virtual ICollection<TucJob> TucJobDesiredJobTypes { get; set; } = new List<TucJob>();

    public virtual ICollection<TucJob> TucJobNotifiedJobTypes { get; set; } = new List<TucJob>();

    public virtual ICollection<TucJob> TucJobUcjbSpeedNavigations { get; set; } = new List<TucJob>();
}