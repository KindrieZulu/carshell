using CarShell.Web.Data;
using CarShell.Web.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace CarShell.Web.Controllers;

// Matches the API Surface (Phase 1) section of the design doc. The write
// endpoints are stubbed with TODOs — they depend on a real Supabase project
// (geocoding cache, image storage) that isn't wired up yet in this scaffold.
[ApiController]
[Route("api/listings")]
public class ListingsController(CarShellDbContext db) : ControllerBase
{
    private static readonly GeometryFactory GeometryFactory =
        NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    private const int PageSize = 24;
    private const double MilesToMeters = 1609.34;

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] double? lat,
        [FromQuery] double? lng,
        [FromQuery] double? radiusMiles,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] string? make,
        [FromQuery] string? model,
        [FromQuery] int? minYear,
        [FromQuery] int? maxYear,
        [FromQuery] decimal? minEngineCapacity,
        [FromQuery] decimal? maxEngineCapacity,
        [FromQuery] FuelType? fuelType,
        [FromQuery] TransmissionType? transmission,
        [FromQuery] BodyType? bodyType,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        var query = db.Listings.AsNoTracking().Where(l => l.Status == ListingStatus.Active);

        if (minPrice is not null) query = query.Where(l => l.Price >= minPrice);
        if (maxPrice is not null) query = query.Where(l => l.Price <= maxPrice);
        if (minYear is not null) query = query.Where(l => l.Year >= minYear);
        if (maxYear is not null) query = query.Where(l => l.Year <= maxYear);
        if (minEngineCapacity is not null) query = query.Where(l => l.EngineCapacityLitres >= minEngineCapacity);
        if (maxEngineCapacity is not null) query = query.Where(l => l.EngineCapacityLitres <= maxEngineCapacity);
        if (fuelType is not null) query = query.Where(l => l.FuelType == fuelType);
        if (transmission is not null) query = query.Where(l => l.Transmission == transmission);
        if (bodyType is not null) query = query.Where(l => l.BodyType == bodyType);
        if (!string.IsNullOrWhiteSpace(make)) query = query.Where(l => l.Make.Name == make);
        if (!string.IsNullOrWhiteSpace(model)) query = query.Where(l => l.Model.Name == model);

        // PostGIS radius filter — ST_DWithin under the hood via IsWithinDistance,
        // using the GIST index on Listing.Location. See Distance + Price
        // Filtering in the design doc.
        if (lat is not null && lng is not null && radiusMiles is not null)
        {
            var origin = GeometryFactory.CreatePoint(new Coordinate(lng.Value, lat.Value));
            var radiusMeters = radiusMiles.Value * MilesToMeters;
            query = query.Where(l => l.Location.IsWithinDistance(origin, radiusMeters));
        }

        query = sort switch
        {
            "price_asc" => query.OrderBy(l => l.Price),
            "price_desc" => query.OrderByDescending(l => l.Price),
            "newest" => query.OrderByDescending(l => l.CreatedAt),
            _ => query.OrderByDescending(l => l.CreatedAt),
        };

        var results = await query
            .Skip((Math.Max(page, 1) - 1) * PageSize)
            .Take(PageSize)
            .Select(l => new ListingSummary(
                l.Id, l.Make.Name, l.Model.Name, l.Year, l.Price, l.Mileage, l.Postcode))
            .ToListAsync(ct);

        return Ok(results);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var listing = await db.Listings.AsNoTracking()
            .Include(l => l.Images.OrderBy(i => i.Position))
            .Include(l => l.Seller)
            .Include(l => l.Make)
            .Include(l => l.Model)
            .FirstOrDefaultAsync(l => l.Id == id, ct);

        return listing is null ? NotFound() : Ok(listing);
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public IActionResult Create([FromBody] CreateListingRequest request)
    {
        // TODO: geocode request.Postcode via postcodes.io, caching the result
        // in PostcodeGeocodes, and populate Lat/Lng/Location from it.
        // TODO: validate the VIN format (the unique index on SellerId+Vin
        // already rejects a duplicate).
        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public IActionResult Update(Guid id)
    {
        // TODO: apply the update, re-geocoding if the postcode changed, and
        // write a ListingStatusEvent row if Status changed.
        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public IActionResult Delete(Guid id)
    {
        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost("{id:guid}/images/upload-url")]
    [Authorize(Policy = "AdminOnly")]
    public IActionResult GetImageUploadUrl(Guid id)
    {
        // TODO: issue a short-lived pre-signed upload URL against Supabase
        // Storage. See Image Upload Pipeline in the design doc — the byte
        // stream never passes through this API.
        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost("{id:guid}/images/confirm")]
    [Authorize(Policy = "AdminOnly")]
    public IActionResult ConfirmImageUpload(Guid id)
    {
        // TODO: verify the object exists in storage and its size/content-type,
        // then record a ListingImage row.
        return StatusCode(StatusCodes.Status501NotImplemented);
    }
}

public record ListingSummary(
    Guid Id, string Make, string Model, int Year, decimal Price, int Mileage, string Postcode);

public record CreateListingRequest(
    int MakeId,
    int ModelId,
    string? Trim,
    int Year,
    int Mileage,
    decimal EngineCapacityLitres,
    decimal Price,
    FuelType FuelType,
    TransmissionType Transmission,
    BodyType BodyType,
    string? Description,
    string Vin,
    string Postcode);
