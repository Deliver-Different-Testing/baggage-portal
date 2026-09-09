using System.Net;
using Mjml.Net;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;

namespace BaggageDelivery.Core.Notifications;

internal sealed class NotificationRenderer(IMjmlRenderer mjml) : INotificationRenderer
{
    public Task<RenderedNotification> RenderBookingLinkAsync(BookingNotificationContext context, CancellationToken ct)
    {
        return context.Channel switch
        {
            NotificationChannel.Sms => Task.FromResult(RenderSms(context)),
            NotificationChannel.Email => Task.FromResult(RenderEmail(context)),
            _ => throw new ArgumentOutOfRangeException(nameof(context),
                $"Unknown channel '{context.Channel}'")
        };
    }

    public Task<RenderedNotification> RenderBookingConfirmedAsync(
        BookingConfirmedNotificationContext context, CancellationToken ct)
    {
        return context.Channel switch
        {
            NotificationChannel.Sms => Task.FromResult(RenderConfirmedSms(context)),
            NotificationChannel.Email => Task.FromResult(RenderConfirmedEmail(context)),
            _ => throw new ArgumentOutOfRangeException(nameof(context),
                $"Unknown channel '{context.Channel}'")
        };
    }

    public Task<RenderedNotification> RenderAddressUnserviceableAsync(
        AddressUnserviceableNotificationContext context, CancellationToken ct)
    {
        var lines = new List<string>
        {
            $"Job {context.JobNumber} ({context.AirlineName}) - the passenger has entered a delivery",
            "address we cannot service. The delivery has NOT been booked.",
            "",
            $"Passenger: {context.PassengerName}"
        };

        if (!string.IsNullOrWhiteSpace(context.PassengerPhone))
        {
            lines.Add($"Phone: {context.PassengerPhone}");
        }

        if (!string.IsNullOrWhiteSpace(context.PassengerEmail))
        {
            lines.Add($"Email: {context.PassengerEmail}");
        }

        if (!string.IsNullOrWhiteSpace(context.FileReference))
        {
            lines.Add($"File reference: {context.FileReference}");
        }

        lines.AddRange([
            "",
            $"Address on file: {context.CurrentAddress}",
            $"Address requested: {context.RequestedAddress}",
            "",
            "The passenger has asked you to contact them to arrange delivery."
        ]);

        return Task.FromResult(new RenderedNotification(
            Subject: $"Baggage delivery address needs checking - {context.JobNumber}",
            Body: string.Join(Environment.NewLine, lines)));
    }

    private static RenderedNotification RenderSms(BookingNotificationContext c)
    {
        var body =
            $"Hi {FirstNameOf(c.PassengerName)}, your {c.AirlineName} baggage is here. " +
            $"Confirm delivery to receive it{RefSuffix(c.FileReference)} {c.BookingUrl}";
        return new RenderedNotification(Subject: "", Body: body);
    }

    private RenderedNotification RenderEmail(BookingNotificationContext c)
    {
        var headerLine = string.IsNullOrWhiteSpace(c.FileReference)
            ? Escape(c.AirlineName)
            : $"File Reference: {Escape(c.FileReference)} · {Escape(c.AirlineName)}";

        var mjmlTemplate = $"""
            <mjml>
              <mj-head>
                <mj-title>Confirm your baggage delivery</mj-title>
                <mj-attributes>
                  <mj-all font-family="Plus Jakarta Sans, Arial, sans-serif" />
                </mj-attributes>
              </mj-head>
              <mj-body background-color="#f4f2f1">
                <mj-section background-color="#001F3D" padding="32px">
                  <mj-column>
                    <mj-text color="#ffffff" font-size="22px" font-weight="700">Your baggage is here</mj-text>
                    <mj-text color="#9eb2cf" font-size="13px">{headerLine}</mj-text>
                  </mj-column>
                </mj-section>
                <mj-section background-color="#ffffff" padding="32px">
                  <mj-column>
                    <mj-text font-size="16px">Hi {Escape(c.PassengerName)},</mj-text>
                    <mj-text font-size="15px" line-height="1.5">
                      Good news — your baggage is here and we're ready to deliver it. Please confirm
                      your delivery address, pick a time slot, and let us know if it's OK to leave the bag
                      unattended.
                    </mj-text>
                    <mj-button background-color="#00B0B9" color="#001F3D" font-weight="700"
                               border-radius="999px" padding="24px 0" href="{Escape(c.BookingUrl)}">
                      Confirm delivery details
                    </mj-button>
                  </mj-column>
                </mj-section>
                <mj-section background-color="#f4f2f1" padding="24px 32px">
                  <mj-column>
                    <mj-text align="center" color="#6b6764" font-size="12px" letter-spacing="0.04em">
                      Powered by Deliver DFRNT
                    </mj-text>
                  </mj-column>
                </mj-section>
              </mj-body>
            </mjml>
            """;

        var (html, _) = mjml.Render(mjmlTemplate);
        return new RenderedNotification(
            Subject:
            $"{c.AirlineName}: your baggage is here - confirm delivery to receive it{RefSuffix(c.FileReference)}",
            Body: html);
    }

    private static RenderedNotification RenderConfirmedSms(BookingConfirmedNotificationContext c)
    {
        var body = c.IsUpdate
            ? $"Thanks {FirstNameOf(c.PassengerName)}, your {c.AirlineName} baggage delivery has been "
              + $"updated to {c.DayLabel}, {c.WindowLabel}."
            : $"Thanks {FirstNameOf(c.PassengerName)}, your {c.AirlineName} baggage delivery is booked for "
              + $"{c.DayLabel}, {c.WindowLabel}.";

        if (!string.IsNullOrWhiteSpace(c.TrackingUrl))
        {
            body += $" Track it: {c.TrackingUrl}";
        }

        return new RenderedNotification(Subject: "", Body: body);
    }

    private RenderedNotification RenderConfirmedEmail(BookingConfirmedNotificationContext c)
    {
        var headerLine = string.IsNullOrWhiteSpace(c.FileReference)
            ? Escape(c.AirlineName)
            : $"File Reference: {Escape(c.FileReference)} · {Escape(c.AirlineName)}";

        var trackingNumberLine = string.IsNullOrWhiteSpace(c.JobNumber)
            ? string.Empty
            : $"""
                   <mj-text font-size="15px" line-height="1.5">
                     Urgent Couriers will deliver your baggage - Tracking Number
                     <strong>{Escape(c.JobNumber)}</strong>
                   </mj-text>
               """;

        var trackingBlock = string.IsNullOrWhiteSpace(c.TrackingUrl)
            ? string.Empty
            : $"""
                   <mj-button background-color="#00B0B9" color="#001F3D" font-weight="700"
                              border-radius="999px" padding="24px 0" href="{Escape(c.TrackingUrl)}">
                     Track your delivery
                   </mj-button>
               """;

        var heading = c.IsUpdate ? "Your delivery has been updated" : "Your delivery is booked";
        var intro = c.IsUpdate
            ? "Thanks - we have updated your booking. Your baggage is now booked in for delivery"
            : "Thanks - we have everything we need. Your baggage is booked in for delivery";

        var mjmlTemplate = $"""
            <mjml>
              <mj-head>
                <mj-title>{heading}</mj-title>
                <mj-attributes>
                  <mj-all font-family="Plus Jakarta Sans, Arial, sans-serif" />
                </mj-attributes>
              </mj-head>
              <mj-body background-color="#f4f2f1">
                <mj-section background-color="#001F3D" padding="32px">
                  <mj-column>
                    <mj-text color="#ffffff" font-size="22px" font-weight="700">{heading}</mj-text>
                    <mj-text color="#9eb2cf" font-size="13px">{headerLine}</mj-text>
                  </mj-column>
                </mj-section>
                <mj-section background-color="#ffffff" padding="32px">
                  <mj-column>
                    <mj-text font-size="16px">Hi {Escape(c.PassengerName)},</mj-text>
                    <mj-text font-size="15px" line-height="1.5">
                      {intro}
                      <strong>{Escape(c.DayLabel)}</strong>, between <strong>{Escape(c.WindowLabel)}</strong>.
                    </mj-text>
            {trackingNumberLine}
                    <mj-text font-size="15px" font-weight="700" padding-top="16px">What happens next</mj-text>
                    <mj-text font-size="15px" line-height="1.5">
                      <ul style="margin:0;padding-left:20px;">
                        <li>We collect your bag and deliver it to your address within the Delivery Window.</li>
                        <li>We will text/email you when your bag is collected from the airport with a tracking link.</li>
                        <li>You can track the driver from the airport to your address.</li>
                      </ul>
                    </mj-text>
                    <mj-text font-size="15px" line-height="1.5">
                      If anything changes please reply to this email with your update.
                    </mj-text>
            {trackingBlock}
                  </mj-column>
                </mj-section>
                <mj-section background-color="#f4f2f1" padding="24px 32px">
                  <mj-column>
                    <mj-text align="center" color="#6b6764" font-size="12px" letter-spacing="0.04em">
                      Powered by Deliver DFRNT
                    </mj-text>
                  </mj-column>
                </mj-section>
              </mj-body>
            </mjml>
            """;

        var (html, _) = mjml.Render(mjmlTemplate);
        return new RenderedNotification(
            Subject: $"Your {c.AirlineName} baggage delivery is booked",
            Body: html);
    }

    private static string FirstNameOf(string passengerName) => passengerName.Split(' ')[0];

    private static string RefSuffix(string? fileReference) =>
        string.IsNullOrWhiteSpace(fileReference) ? string.Empty : $" (Ref {fileReference.Trim()})";

    private static string Escape(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
