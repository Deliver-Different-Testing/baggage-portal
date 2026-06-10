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
    public ActionResult<TimeSlotDto[]> GetTimeslots(string id, [FromQuery] DateTime? date)
    {
        if (encryption.DecryptId(id) is null)
        {
            return NotFound();
        }

        // v1: server generates slot windows in UTC. Real implementation should pull
        // available runs from Despatch once GET api/Jobs/{id}/slots ships.
        var anchor = (date ?? DateTime.UtcNow).Date;
        var slots = new[]
        {
            new TimeSlotDto(Guid.NewGuid(), anchor.AddHours(9), anchor.AddHours(12), "9:00 AM - 12:00 PM", FirstAvailable: true),
            new TimeSlotDto(Guid.NewGuid(), anchor.AddHours(14), anchor.AddHours(17), "2:00 PM - 5:00 PM", FirstAvailable: false),
            new TimeSlotDto(Guid.NewGuid(), anchor.AddDays(1).AddHours(9), anchor.AddDays(1).AddHours(12), "Tomorrow 9 AM - 12 PM", FirstAvailable: false),
            new TimeSlotDto(Guid.NewGuid(), anchor.AddDays(1).AddHours(14), anchor.AddDays(1).AddHours(17), "Tomorrow 2 PM - 5 PM", FirstAvailable: false)
        };
        return Ok(slots);
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

        await paxBooking.ConfirmAsync(new ConfirmBookingInput(
            JobId: jobId.Value,
            Address: new AddressUpdateDto
            {
                Line1 = body.Address.Line1,
                Line2 = body.Address.Line2,
                Suburb = body.Address.Suburb,
                City = body.Address.City,
                PostCode = body.Address.PostCode,
                Country = body.Address.Country,
                Latitude = body.Address.Latitude,
                Longitude = body.Address.Longitude
            },
            TimeSlotStartUtc: body.TimeSlotStartUtc,
            TimeSlotEndUtc: body.TimeSlotEndUtc,
            AtlOption: body.AtlOption,
            AccessNotes: body.AccessNotes,
            PhoneOverride: body.PhoneOverride), ct);

        return Ok(new ConfirmBookingResponse("Released", DateTime.UtcNow));
    }

    private static BookingSummaryDto MapSummary(BookingSummary s) => new(
        JobId: s.JobId,
        Reference: s.Reference,
        AirlineLabel: s.AirlineLabel,
        PassengerName: s.PassengerName,
        PassengerPhone: s.PassengerPhone,
        PassengerEmail: s.PassengerEmail,
        DeliveryAddress: new AddressDto(
            s.DeliveryAddress.Line1,
            s.DeliveryAddress.Line2,
            s.DeliveryAddress.Suburb,
            s.DeliveryAddress.City,
            s.DeliveryAddress.PostCode,
            s.DeliveryAddress.Country,
            s.DeliveryAddress.Latitude,
            s.DeliveryAddress.Longitude),
        EarliestSlotUtc: s.EarliestSlotUtc,
        LatestSlotUtc: s.LatestSlotUtc);
}
