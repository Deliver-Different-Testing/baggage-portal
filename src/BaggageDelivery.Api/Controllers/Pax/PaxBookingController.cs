using BaggageDelivery.Api.DTOs.Pax;
using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.Notifications;
using BaggageDelivery.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace BaggageDelivery.Api.Controllers.Pax;

[ApiController]
[Route("api/v1/pax/{id}/booking")]
[AllowAnonymous]
public sealed class PaxBookingController(
    IEncryptionService encryption,
    IPaxBookingService paxBooking,
    IJobTrackingLinkService trackingLinks,
    INotificationService notifications) : ControllerBase
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
        if (summary is null)
        {
            return NotFound();
        }

        var trackingUrl = await trackingLinks.GetTrackingUrlAsync(jobId.Value, ct);
        return Ok(MapSummary(summary, trackingUrl));
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

        var trackingUrl = await trackingLinks.GetTrackingUrlAsync(jobId.Value, ct);
        await SendConfirmationAsync(jobId.Value, body, trackingUrl, ct);

        return Ok(new ConfirmBookingResponse("Released", DateTime.UtcNow, trackingUrl));
    }

    private async Task SendConfirmationAsync(
        int jobId, ConfirmBookingRequest body, string? trackingUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.PassengerPhone) && string.IsNullOrWhiteSpace(body.PassengerEmail))
        {
            return;
        }

        try
        {
            var summary = await paxBooking.GetSummaryAsync(jobId, ct);
            if (summary?.Confirmation is not { } confirmation)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(body.PassengerPhone))
            {
                await notifications.SendBookingConfirmedAsync(jobId, body.PassengerPhone,
                    NewContext(NotificationChannel.Sms, summary, confirmation, trackingUrl), ct);
            }

            if (!string.IsNullOrWhiteSpace(body.PassengerEmail))
            {
                await notifications.SendBookingConfirmedAsync(jobId, body.PassengerEmail,
                    NewContext(NotificationChannel.Email, summary, confirmation, trackingUrl), ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warning(ex,
                "Pax confirmation JobId={JobId}: confirmation notification not queued", jobId);
        }
    }

    private static BookingConfirmedNotificationContext NewContext(
        string channel, BookingSummary summary, BookingConfirmation confirmation, string? trackingUrl) =>
        new(channel, summary.PassengerName, summary.AirlineLabel, summary.FileReference,
            confirmation.DayLabel, confirmation.WindowLabel, trackingUrl);

    private static BookingSummaryDto MapSummary(BookingSummary s, string? trackingUrl) => new(
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
        TrackingUrl: trackingUrl,
        Confirmation: s.Confirmation is { } c
            ? new BookingConfirmationDto(
                c.ConfirmedAtUtc, c.DeliveryTimeUtc, c.DayLabel, c.WindowLabel,
                c.AtlOptionId, c.AccessNotes)
            : null);
}
