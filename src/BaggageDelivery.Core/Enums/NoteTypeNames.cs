namespace BaggageDelivery.Core.Enums;

public static class NoteTypeNames
{
    public const string InternalNote = "Internal Note";
    public const string ClientNote = "Client Note";
    public const string PickupNotes = "Pickup Notes";
    public const string DeliveryNotes = "Delivery Notes";

    public static string For(NoteType type) => type switch
    {
        NoteType.InternalNote => InternalNote,
        NoteType.ClientNote => ClientNote,
        NoteType.PickupNotes => PickupNotes,
        NoteType.DeliveryNotes => DeliveryNotes,
        _ => type.ToString()
    };
}
