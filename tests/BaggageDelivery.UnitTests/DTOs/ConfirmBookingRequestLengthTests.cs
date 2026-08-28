using System.ComponentModel.DataAnnotations;
using System.Reflection;
using BaggageDelivery.Api.DTOs.Pax;
using BaggageDelivery.Core.Models;
using BaggageDelivery.UnitTests.Helpers;
using Xunit;

namespace BaggageDelivery.UnitTests.DTOs;

public class ConfirmBookingRequestLengthTests
{
    public static TheoryData<string, string> WrittenColumns() => new()
    {
        { nameof(ConfirmBookingRequest.AccessNotes), nameof(TucJob.UcjbToSpecial) },
        { nameof(ConfirmBookingRequest.PassengerName), nameof(TucJob.DeliverToContact) },
        { nameof(ConfirmBookingRequest.PassengerPhone), nameof(TucJob.DeliverToPhone) },
        { nameof(ConfirmBookingRequest.PassengerEmail), nameof(TucJob.ProofOfDeliveryEmail) },
    };

    [Theory]
    [MemberData(nameof(WrittenColumns))]
    public void Request_length_limits_fit_the_tucJob_columns_they_are_written_to(
        string requestProperty, string jobProperty)
    {
        var declared = MaxLengthOf(typeof(ConfirmBookingRequest), requestProperty);
        var column = ColumnLengthOf(jobProperty);

        Assert.NotNull(declared);
        Assert.NotNull(column);
        Assert.True(declared <= column,
            $"{requestProperty} allows {declared} characters but is written to "
            + $"tucJob.{jobProperty}, which holds {column}.");
    }

    public static TheoryData<string, string> WrittenAddressColumns() => new()
    {
        { nameof(AddressDto.Line1), nameof(TucJob.DeliveryAddressLine4) },
        { nameof(AddressDto.Suburb), nameof(TucJob.DeliveryAddressLine5) },
        { nameof(AddressDto.City), nameof(TucJob.DeliveryAddressLine6) },
        { nameof(AddressDto.PostCode), nameof(TucJob.DeliveryAddressLine7) },
    };

    [Theory]
    [MemberData(nameof(WrittenAddressColumns))]
    public void Address_length_limits_fit_the_tucJob_columns_they_are_written_to(
        string addressProperty, string jobProperty)
    {
        var declared = MaxLengthOf(typeof(AddressDto), addressProperty);
        var column = ColumnLengthOf(jobProperty);

        Assert.NotNull(declared);
        Assert.NotNull(column);
        Assert.True(declared <= column,
            $"AddressDto.{addressProperty} allows {declared} characters but is written to "
            + $"tucJob.{jobProperty}, which holds {column}.");
    }

    [Fact]
    public void Country_is_normalised_before_it_reaches_a_column_so_it_stays_unconstrained() => Assert.True(MaxLengthOf(typeof(AddressDto), nameof(AddressDto.Country)) > 2);

    private static int? MaxLengthOf(Type type, string propertyName) =>
        type.GetConstructors()
            .SelectMany(c => c.GetParameters())
            .FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase))?
            .GetCustomAttribute<MaxLengthAttribute>()?.Length;

    private static int? ColumnLengthOf(string jobProperty)
    {
        using var db = InMemoryDb.NewContext();
        return db.Model.FindEntityType(typeof(TucJob))?.FindProperty(jobProperty)?.GetMaxLength();
    }
}
