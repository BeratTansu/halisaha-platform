using Halisaha.Domain.Common;
using Halisaha.Domain.Entities;

namespace Halisaha.Domain.Tests;

public class VenueTests
{
    [Fact]
    public void Create_WithValidData_TrimsNameAndIsActive()
    {
        var venue = new Venue("  Yildiz Halisaha ", "Moda Cd. 1", 40.98, 29.03, 34001);

        Assert.Equal("Yildiz Halisaha", venue.Name);
        Assert.True(venue.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_Throws(string name)
    {
        var ex = Assert.Throws<DomainException>(
            () => new Venue(name, "Moda Cd. 1", 40.98, 29.03, 34001));

        Assert.Equal("venue.name_required", ex.Code);
    }

    [Theory]
    [InlineData(90.1, 29.0)]
    [InlineData(-90.1, 29.0)]
    [InlineData(41.0, 180.1)]
    [InlineData(41.0, -180.1)]
    [InlineData(double.NaN, 29.0)]
    [InlineData(41.0, double.NaN)]
    [InlineData(double.PositiveInfinity, 29.0)]
    [InlineData(double.NegativeInfinity, 29.0)]
    public void Create_WithInvalidCoordinates_Throws(
        double latitude,
        double longitude)
    {
        var ex = Assert.Throws<DomainException>(
            () => new Venue("Yildiz", "Moda Cd. 1", latitude, longitude, 34001));

        Assert.Equal("venue.invalid_coordinates", ex.Code);
    }

    [Theory]
    [InlineData(90.0, 180.0)]
    [InlineData(-90.0, -180.0)]
    public void Create_WithBoundaryCoordinates_Succeeds(
        double latitude,
        double longitude)
    {
        var venue = new Venue("Yildiz", "Moda Cd. 1", latitude, longitude, 34001);

        Assert.Equal(latitude, venue.Latitude);
        Assert.Equal(longitude, venue.Longitude);
    }

    [Fact]
    public void SetDetails_OnDeactivatedVenue_DoesNotReactivate()
    {
        var venue = new Venue("Yildiz", "Moda Cd. 1", 40.98, 29.03, 34001);

        venue.Deactivate();

        venue.SetDetails("Yildiz Spor", "Moda Cd. 2", 40.98, 29.03, 34001);

        Assert.False(venue.IsActive);
        Assert.Equal("Yildiz Spor", venue.Name);
    }

    [Fact]
    public void SetDetails_WithInvalidCoordinates_LeavesVenueUnchanged()
    {
        var venue = new Venue("Yildiz", "Moda Cd. 1", 40.98, 29.03, 34001);

        Assert.Throws<DomainException>(() =>
            venue.SetDetails("Yildiz Spor", "Moda Cd. 2", 200, 29.03, 34001));

        Assert.Equal("Yildiz", venue.Name);
        Assert.Equal("Moda Cd. 1", venue.Address);
        Assert.Equal(40.98, venue.Latitude);
        Assert.Equal(29.03, venue.Longitude);
        Assert.Equal(34001, venue.DistrictId);
    }
}