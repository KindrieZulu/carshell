namespace CarShell.Web.Data.Entities;

// Cache of postcode -> coordinates so search never calls the geocoding
// API more than once per postcode.
public class PostcodeGeocode
{
    public string Postcode { get; set; } = default!; // primary key
    public double Lat { get; set; }
    public double Lng { get; set; }
    public DateTimeOffset FetchedAt { get; set; } = DateTimeOffset.UtcNow;
}
