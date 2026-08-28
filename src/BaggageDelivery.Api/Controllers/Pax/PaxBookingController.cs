using BaggageDelivery.Api.DTOs.Pax;
using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BaggageDelivery.Api.Controllers.Pax;

[ApiController]
[Route("api/v1/pax/{id}/booking")]
[AllowAnonymous]
public sealed class PaxBookingController(
    IEncryptionService encryption,
    IPaxBookingService paxBooking) : ControllerBase
{
    [HttpGet("")]
    public async Task<ActionResult<BookingSummaryDto>> GetBooking(string id, CancellationToken ct)
    {
        var jobId = encryption.DecryptId(id);
        if (jobId is null)
        {
            return NotFound();
        }

        var summary = await paxBooking.GetSummaryAsync(jobId.Value, ct);
        return summary is null ? NotFound() : Ok(MapSummary(summary));
    }

    [HttpGet("timeslots")]
    public async Task<ActionResult<TimeSlotDto[]>> GetTimeslots(
        string id, [FromQuery] DateTime? date, CancellationToken ct)
    {
        var jobId = encryption.DecryptId(id);
        if (jobId is null)
        {
            return NotFound();
        }

        var slots = await paxBooking.GetTimeslotsAsync(jobId.Value, date, ct);
        return Ok(slots
            .Select(s => new TimeSlotDto(s.Id, s.RunUtc, s.DayLabel, s.Label, s.FirstAvailable))
            .ToArray());
    }

    [HttpPost("confirm")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<ConfirmBookingResponse>> Confirm(
        string id,
        [FromBody] ConfirmBookingRequest body,
        CancellationToken ct)
    {
        var jobId = encryption.DecryptId(id);
        if (jobId is null)
        {
            return NotFound();
        }

        try
        {
            await paxBooking.ConfirmAsync(new ConfirmBookingInput(
                JobId: jobId.Value,
                Address: new AddressUpdateDto
                {
                    Line1 = body.Address.Line1,
                    Line2 = body.Address.Line2,
                    Line3 = body.Address.Line3,
                    Line4 = body.Address.Line4,
                    Line5 = body.Address.Line5,
                    Line6 = body.Address.Line6,
                    Line7 = body.Address.Line7,
                    Country = body.Address.Country,
                    Latitude = body.Address.Latitude,
                    Longitude = body.Address.Longitude
                },
                DeliveryTimeUtc: body.DeliveryTimeUtc!.Value,
                AtlOptionId: body.AtlOptionId,
                AccessNotes: body.AccessNotes,
                PassengerName: body.PassengerName,
                PassengerPhone: body.PassengerPhone,
                PassengerEmail: body.PassengerEmail), ct);
        }
        catch (PaxAlreadyConfirmedException)
        {
            return Conflict();
        }
        catch (PaxAddressValidationException ex)
        {
            ModelState.AddModelError("Address.Country", ex.Message);
            return ValidationProblem(ModelState);
        }

        return Ok(new ConfirmBookingResponse("Released", DateTime.UtcNow));
    }

    private static BookingSummaryDto MapSummary(BookingSummary s) => new(
        JobId: s.JobId,
        JobNumber: s.JobNumber,
        FileReference: s.FileReference,
        AirlineLabel: s.AirlineLabel,
        AirlineCode: s.AirlineCode,
        SupportPhone: s.SupportPhone,
        PassengerName: s.PassengerName,
        PassengerPhone: s.PassengerPhone,
        PassengerEmail: s.PassengerEmail,
        DeliveryAddress: new AddressDto(
            s.DeliveryAddress.Line1,
            s.DeliveryAddress.Line2,
            s.DeliveryAddress.Line3,
            s.DeliveryAddress.Line4,
            s.DeliveryAddress.Line5,
            s.DeliveryAddress.Line6,
            s.DeliveryAddress.Line7,
            s.DeliveryAddress.Country,
            s.DeliveryAddress.Latitude,
            s.DeliveryAddress.Longitude),
        EarliestSlotUtc: s.EarliestSlotUtc,
        LatestSlotUtc: s.LatestSlotUtc,
        AtlOptions: [.. s.AtlOptions.Select(o => new DTOs.Pax.AtlOptionDto(o.Id, o.Name))],
        DefaultAtlOptionId: s.DefaultAtlOptionId,
        TrackingAvailable: s.TrackingAvailable,
        Confirmation: s.Confirmation is { } c
            ? new BookingConfirmationDto(
                c.ConfirmedAtUtc, c.DeliveryTimeUtc, c.DayLabel, c.WindowLabel,
                c.AtlOptionId, c.AccessNotes)
            : null);
}
