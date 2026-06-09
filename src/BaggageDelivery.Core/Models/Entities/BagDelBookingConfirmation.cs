using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BaggageDelivery.Core.Models.Entities;

[Table("BagDelBookingConfirmation")]
public class BagDelBookingConfirmation
{
    [Key]
    public int Id { get; set; }

    public required int JobId { get; set; }

    public required int TokenId { get; set; }

    public required DateTime ConfirmedAtUtc { get; set; }

    [MaxLength(200)]
    public required string AddressLine1 { get; set; }

    [MaxLength(200)]
    public string? AddressLine2 { get; set; }

    [MaxLength(100)]
    public string? Suburb { get; set; }

    [MaxLength(100)]
    public required string City { get; set; }

    [MaxLength(20)]
    public string? PostCode { get; set; }

    [MaxLength(2)]
    public required string Country { get; set; }

    [Column(TypeName = "decimal(9, 6)")]
    public decimal? Latitude { get; set; }

    [Column(TypeName = "decimal(9, 6)")]
    public decimal? Longitude { get; set; }

    public required DateTime TimeSlotStartUtc { get; set; }

    public required DateTime TimeSlotEndUtc { get; set; }

    [MaxLength(30)]
    public required string AtlOption { get; set; }

    [MaxLength(500)]
    public string? AccessNotes { get; set; }

    [MaxLength(40)]
    public string? PhoneOverride { get; set; }

    public DateTime? DespatchSyncedAtUtc { get; set; }

    public string? DespatchSyncError { get; set; }
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
