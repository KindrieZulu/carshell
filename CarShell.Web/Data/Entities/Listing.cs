using NetTopologySuite.Geometries;

namespace CarShell.Web.Data.Entities;

public enum ListingStatus { Draft, Active, Sold, Removed }
public enum FuelType { Petrol, Diesel, Electric, Hybrid, PluginHybrid, Lpg }
public enum TransmissionType { Manual, Automatic }
public enum BodyType { Hatchback, Saloon, Estate, Suv, Coupe, Convertible, Mpv, Pickup }

public class Listing
{
    public Guid Id { get; set; }

    public Guid SellerId { get; set; }
    public User Seller { get; set; } = default!;

    public int MakeId { get; set; }
    public Make Make { get; set; } = default!;
    public int ModelId { get; set; }
    public VehicleModel Model { get; set; } = default!;

    public string? Trim { get; set; }
    public int Year { get; set; }
    public int Mileage { get; set; }
    public decimal EngineCapacityLitres { get; set; } // decimal(3,1): 0.1-99.9L, plenty for any real car
    public decimal Price { get; set; }
    public FuelType FuelType { get; set; }
    public TransmissionType Transmission { get; set; }
    public BodyType BodyType { get; set; }
    public string? Description { get; set; }

    // 17-character VIN; format-checked and de-duplicated per seller at the
    // application layer, see ListingsController.
    public string Vin { get; set; } = default!;

    public ListingStatus Status { get; set; } = ListingStatus.Draft;

    public string Postcode { get; set; } = default!;
    public double Lat { get; set; }
    public double Lng { get; set; }

    // Geography point for PostGIS radius queries (ST_DWithin / IsWithinDistance).
    // Kept in sync with Lat/Lng whenever a listing is geocoded.
    public Point Location { get; set; } = default!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ListingImage> Images { get; set; } = new List<ListingImage>();
    public ICollection<ListingStatusEvent> StatusEvents { get; set; } = new List<ListingStatusEvent>();
}
