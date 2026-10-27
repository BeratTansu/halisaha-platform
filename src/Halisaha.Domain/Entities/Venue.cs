using Halisaha.Domain.Common;

namespace Halisaha.Domain.Entities;

/// <summary>
/// A pitch. Venues are never deleted, only deactivated, so past matches keep their location.
/// Matches show the venue's current details; a real move means a new venue.
/// </summary>
public sealed class Venue
{
    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public int DistrictId { get; private set; }
    public District? District { get; private set; }
    public bool IsActive { get; private set; }

    private Venue() { }

    public Venue(string name, string address, double latitude, double longitude, int districtId)
    {
        SetDetails(name, address, latitude, longitude, districtId);
        IsActive = true;
    }

    public void SetDetails(string name, string address, double latitude, double longitude, int districtId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw DomainException.Validation("venue.name_required", "Venue name is required.");
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
            throw DomainException.Validation("venue.invalid_coordinates", "Coordinates are out of range.");

        Name = name.Trim();
        Address = address.Trim();
        Latitude = latitude;
        Longitude = longitude;
        DistrictId = districtId;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
