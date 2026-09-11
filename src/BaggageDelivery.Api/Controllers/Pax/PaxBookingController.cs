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
    public const string NotConfirmedProblemType = "urn:baggage:booking-not-confirmed";
    public const string ChangeWindowClosedProblemType = "urn:baggage:change-window-closed";

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
        string id, [FromQuery] DateTime? date, CancellationToken ct,
        [FromQuery] int? jobTypeId = null, [FromQuery] int? scheduleId = null)
    {
        var jobId = encryption.DecryptId(id);
        if (jobId is null)
        {
            return NotFound();
        }

        var slots = await paxBooking.GetTimeslotsAsync(jobId.Value, date, ct, jobTypeId, scheduleId);
        return Ok(slots
            .Select(s => new TimeSlotDto(s.Id, s.RunUtc, s.DayLabel, s.Label, s.FirstAvailable))
            .ToArray());
    }

    [HttpPost("services")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<AvailableServicesResponse>> GetServices(
        string id, [FromBody] AddressServicesRequest body, CancellationToken ct)
    {
        var jobId = encryption.DecryptId(id);
        if (jobId is null)
        {
            return NotFound();
        }

        var services = await paxBooking.GetAvailableServicesAsync(
            jobId.Value, MapServiceAddress(body.Address), ct);

        return Ok(new AvailableServicesResponse(
            [.. services.Select(s => new AvailableServiceDto(
                s.JobTypeId, s.ScheduleId, s.Name, s.Description,
                s.BookDateUtc, s.DurationMinutes, s.IsScheduled))],
            NoServiceAvailable: services.Count == 0));
    }

    [HttpPost("address-help")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<AddressHelpResponse>> RequestAddressHelp(
        string id, [FromBody] AddressHelpRequest body, CancellationToken ct)
    {
        var jobId = encryption.DecryptId(id);
        if (jobId is null)
        {
            return NotFound();
        }

        var requested = await paxBooking.RequestAirlineContactAsync(
            new AddressContactRequestInput(
                JobId: jobId.Value,
                RequestedAddress: MapAddress(body.Address),
                PassengerName: body.PassengerName,
                PassengerPhone: body.PassengerPhone,
                PassengerEmail: body.PassengerEmail),
            ct);

        return requested ? Ok(new AddressHelpResponse(true)) : NotFound();
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
                Address: MapAddress(body.Address),
                DeliveryTimeUtc: body.DeliveryTimeUtc!.Value,
                AtlOptionId: body.AtlOptionId,
                AccessNotes: body.AccessNotes,
                PassengerName: body.PassengerName,
                PassengerPhone: body.PassengerPhone,
                PassengerEmail: body.PassengerEmail,
                ServiceJobTypeId: body.ServiceJobTypeId), ct);
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
        catch (PaxServiceNotAllowedException ex)
        {
            ModelState.AddModelError("Service", ex.Message);
            return ValidationProblem(ModelState);
        }

        var trackingUrl = await trackingLinks.GetTrackingUrlAsync(jobId.Value, ct);
        await SendConfirmationAsync(jobId.Value, trackingUrl, body.PassengerPhone,
            body.PassengerEmail, isUpdate: false, ct);

        return Ok(new ConfirmBookingResponse("Released", DateTime.UtcNow, trackingUrl));
    }

    [HttpPost("amend")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<ConfirmBookingResponse>> Amend(
        string id,
        [FromBody] AmendBookingRequest body,
        CancellationToken ct)
    {
        var jobId = encryption.DecryptId(id);
        if (jobId is null)
        {
            return NotFound();
        }

        try
        {
            await paxBooking.AmendAsync(new AmendBookingInput(
                JobId: jobId.Value,
                DeliveryTimeUtc: body.DeliveryTimeUtc!.Value,
                AtlOptionId: body.AtlOptionId,
                AccessNotes: body.AccessNotes,
                PassengerName: body.PassengerName,
                PassengerPhone: body.PassengerPhone,
                PassengerEmail: body.PassengerEmail), ct);
        }
        catch (PaxNotConfirmedException)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "This booking has not been confirmed yet.",
                Type = NotConfirmedProblemType
            });
        }
        catch (PaxAmendWindowClosedException)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "This booking can no longer be changed online.",
                Type = ChangeWindowClosedProblemType
            });
        }

        var trackingUrl = await trackingLinks.GetTrackingUrlAsync(jobId.Value, ct);
        await SendConfirmationAsync(jobId.Value, trackingUrl, body.PassengerPhone,
            body.PassengerEmail, isUpdate: true, ct);

        return Ok(new ConfirmBookingResponse("Updated", DateTime.UtcNow, trackingUrl));
    }

    private static AddressUpdateDto MapServiceAddress(ServiceAddressDto address) => new()
    {
        Line4 = string.Empty,
        Line5 = address.Line5 ?? string.Empty,
        Line6 = string.Empty,
        Line7 = address.Line7,
        Country = string.Empty,
        Latitude = address.Latitude,
        Longitude = address.Longitude
    };

    private static AddressUpdateDto MapAddress(AddressDto address) => new()
    {
        Line1 = address.Line1,
        Line2 = address.Line2,
        Line3 = address.Line3,
        Line4 = address.Line4,
        Line5 = address.Line5,
        Line6 = address.Line6,
        Line7 = address.Line7,
        Country = address.Country,
        Latitude = address.Latitude,
        Longitude = address.Longitude
    };

    private async Task SendConfirmationAsync(
        int jobId, string? trackingUrl, string? passengerPhone, string? passengerEmail,
        bool isUpdate, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(passengerPhone) && string.IsNullOrWhiteSpace(passengerEmail))
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

            if (!string.IsNullOrWhiteSpace(passengerPhone))
            {
                await notifications.SendBookingConfirmedAsync(jobId, passengerPhone,
                    NewContext(NotificationChannel.Sms, summary, confirmation, trackingUrl, isUpdate),
                    ct);
            }

            if (!string.IsNullOrWhiteSpace(passengerEmail))
            {
                await notifications.SendBookingConfirmedAsync(jobId, passengerEmail,
                    NewContext(NotificationChannel.Email, summary, confirmation, trackingUrl, isUpdate),
                    ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warning(ex,
                "Pax confirmation JobId={JobId}: confirmation notification not queued", jobId);
        }
    }

    private static BookingConfirmedNotificationContext NewContext(
        string channel, BookingSummary summary, BookingConfirmation confirmation,
        string? trackingUrl, bool isUpdate) =>
        new(channel, summary.PassengerName, summary.AirlineSmsName, summary.FileReference,
            summary.JobNumber, confirmation.DayLabel, confirmation.WindowLabel, trackingUrl,
            isUpdate);

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
        DeliveryNotes: [.. s.DeliveryNotes],
        EarliestSlotUtc: s.EarliestSlotUtc,
        LatestSlotUtc: s.LatestSlotUtc,
        AtlOptions: [.. s.AtlOptions.Select(o => new DTOs.Pax.AtlOptionDto(o.Id, o.Name))],
        DefaultAtlOptionId: s.DefaultAtlOptionId,
        TrackingAvailable: s.TrackingAvailable,
        TrackingUrl: trackingUrl,
        BookingLeadTimeMinutes: s.BookingLeadTimeMinutes,
        Confirmation: s.Confirmation is { } c
            ? new BookingConfirmationDto(
                c.ConfirmedAtUtc, c.DeliveryTimeUtc, c.DayLabel, c.WindowLabel,
                c.AtlOptionId, c.AccessNotes, c.EditableUntilUtc, c.CanEdit)
            : null);
}
