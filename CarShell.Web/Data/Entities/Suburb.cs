namespace CarShell.Web.Data.Entities;

// Zimbabwe has no formal national postcode system, unlike the UK postcodes
// this schema originally assumed. Location is captured as a city/suburb
// picked from this reference table instead of geocoding a free-text
// address, which sidesteps needing an external geocoding API entirely —
// the coordinates are just seeded once, like Makes/VehicleModels.
public class Suburb
{
    public int Id { get; set; }
    public string City { get; set; } = default!;
    public string Name { get; set; } = default!;
    public double Lat { get; set; }
    public double Lng { get; set; }
}
