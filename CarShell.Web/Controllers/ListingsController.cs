using CarShell.Web.Auth;
using CarShell.Web.Data;
using CarShell.Web.Data.Entities;
using CarShell.Web.Services;
using CarShell.Web.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Npgsql;

namespace CarShell.Web.Controllers;

// Matches the API Surface (Phase 1) section of the design doc.
[ApiController]
[Route("api/listings")]
public class ListingsController(
    CarShellDbContext db,
    IGeocodingService geocoding,
    ISupabaseStorageService storage) : ControllerBase
{
    private static readonly GeometryFactory GeometryFactory =
        NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    private const int PageSize = 24;
    private const double MilesToMeters = 1609.34;
    private const int MaxImagesPerListing = 20;
    private const long MaxImageSizeBytes = 10 * 1024 * 1024;

    private static readonly Dictionary<string, string> AllowedImageContentTypes = new()
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
    };

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
    public async Task<IActionResult> Create([FromBody] CreateListingRequest request, CancellationToken ct)
    {
        var vin = Vin.Normalize(request.Vin);
        if (!Vin.IsValid(vin))
        {
            return BadRequest("VIN must be 17 characters, using the standard VIN character set (no I, O, or Q).");
        }

        var sellerId = User.GetUserId();

        var duplicate = await db.Listings.AnyAsync(l => l.SellerId == sellerId && l.Vin == vin, ct);
        if (duplicate)
        {
            return Conflict("A listing with this VIN already exists for this seller.");
        }

        var geocode = await geocoding.GeocodeAsync(request.Postcode, ct);
        if (geocode is null)
        {
            return BadRequest("Could not resolve the given postcode.");
        }

        var now = DateTimeOffset.UtcNow;
        var listing = new Listing
        {
            Id = Guid.NewGuid(),
            SellerId = sellerId,
            MakeId = request.MakeId,
            ModelId = request.ModelId,
            Trim = request.Trim,
            Year = request.Year,
            Mileage = request.Mileage,
            EngineCapacityLitres = request.EngineCapacityLitres,
            Price = request.Price,
            FuelType = request.FuelType,
            Transmission = request.Transmission,
            BodyType = request.BodyType,
            Description = request.Description,
            Vin = vin,
            Status = ListingStatus.Active,
            Postcode = request.Postcode.Trim().ToUpperInvariant(),
            Lat = geocode.Lat,
            Lng = geocode.Lng,
            Location = GeometryFactory.CreatePoint(new Coordinate(geocode.Lng, geocode.Lat)),
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Listings.Add(listing);
        db.ListingStatusEvents.Add(new ListingStatusEvent
        {
            Id = Guid.NewGuid(),
            ListingId = listing.Id,
            FromStatus = ListingStatus.Draft,
            ToStatus = ListingStatus.Active,
            ChangedAt = now,
            ChangedBy = sellerId,
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Conflict("A listing with this VIN already exists for this seller.");
        }

        return CreatedAtAction(nameof(GetById), new { id = listing.Id }, listing);
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateListingRequest request, CancellationToken ct)
    {
        var listing = await db.Listings.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (listing is null)
        {
            return NotFound();
        }

        if (request.Postcode is not null &&
            !string.Equals(request.Postcode.Trim(), listing.Postcode, StringComparison.OrdinalIgnoreCase))
        {
            var geocode = await geocoding.GeocodeAsync(request.Postcode, ct);
            if (geocode is null)
            {
                return BadRequest("Could not resolve the given postcode.");
            }

            listing.Postcode = request.Postcode.Trim().ToUpperInvariant();
            listing.Lat = geocode.Lat;
            listing.Lng = geocode.Lng;
            listing.Location = GeometryFactory.CreatePoint(new Coordinate(geocode.Lng, geocode.Lat));
        }

        if (request.MakeId is not null) listing.MakeId = request.MakeId.Value;
        if (request.ModelId is not null) listing.ModelId = request.ModelId.Value;
        if (request.Trim is not null) listing.Trim = request.Trim;
        if (request.Year is not null) listing.Year = request.Year.Value;
        if (request.Mileage is not null) listing.Mileage = request.Mileage.Value;
        if (request.EngineCapacityLitres is not null) listing.EngineCapacityLitres = request.EngineCapacityLitres.Value;
        if (request.Price is not null) listing.Price = request.Price.Value;
        if (request.FuelType is not null) listing.FuelType = request.FuelType.Value;
        if (request.Transmission is not null) listing.Transmission = request.Transmission.Value;
        if (request.BodyType is not null) listing.BodyType = request.BodyType.Value;
        if (request.Description is not null) listing.Description = request.Description;

        if (request.Status is not null && request.Status.Value != listing.Status)
        {
            if (request.Status.Value == ListingStatus.Sold && request.SalePrice is null)
            {
                return BadRequest("SalePrice is required when marking a listing as sold.");
            }

            db.ListingStatusEvents.Add(new ListingStatusEvent
            {
                Id = Guid.NewGuid(),
                ListingId = listing.Id,
                FromStatus = listing.Status,
                ToStatus = request.Status.Value,
                SalePrice = request.Status.Value == ListingStatus.Sold ? request.SalePrice : null,
                ChangedAt = DateTimeOffset.UtcNow,
                ChangedBy = User.GetUserId(),
            });
            listing.Status = request.Status.Value;
        }

        listing.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Conflict("A listing with this VIN already exists for this seller.");
        }

        return Ok(listing);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var listing = await db.Listings.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (listing is null)
        {
            return NotFound();
        }

        if (listing.Status != ListingStatus.Removed)
        {
            db.ListingStatusEvents.Add(new ListingStatusEvent
            {
                Id = Guid.NewGuid(),
                ListingId = listing.Id,
                FromStatus = listing.Status,
                ToStatus = ListingStatus.Removed,
                ChangedAt = DateTimeOffset.UtcNow,
                ChangedBy = User.GetUserId(),
            });
            listing.Status = ListingStatus.Removed;
            listing.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return NoContent();
    }

    [HttpPost("{id:guid}/images/upload-url")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetImageUploadUrl(
        Guid id, [FromBody] RequestImageUploadRequest request, CancellationToken ct)
    {
        var exists = await db.Listings.AnyAsync(l => l.Id == id, ct);
        if (!exists)
        {
            return NotFound();
        }

        if (!AllowedImageContentTypes.TryGetValue(request.ContentType, out var extension))
        {
            return BadRequest("Content type must be one of: " + string.Join(", ", AllowedImageContentTypes.Keys));
        }

        var imageCount = await db.ListingImages.CountAsync(i => i.ListingId == id, ct);
        if (imageCount >= MaxImagesPerListing)
        {
            return BadRequest($"A listing can have at most {MaxImagesPerListing} images.");
        }

        var storageKey = $"listings/{id}/{Guid.NewGuid():N}{extension}";
        var signed = await storage.CreateSignedUploadUrlAsync(storageKey, ct);

        return Ok(new { uploadUrl = signed.UploadUrl, storageKey = signed.StorageKey });
    }

    [HttpPost("{id:guid}/images/confirm")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ConfirmImageUpload(
        Guid id, [FromBody] ConfirmImageUploadRequest request, CancellationToken ct)
    {
        var exists = await db.Listings.AnyAsync(l => l.Id == id, ct);
        if (!exists)
        {
            return NotFound();
        }

        if (!request.StorageKey.StartsWith($"listings/{id}/", StringComparison.Ordinal))
        {
            return BadRequest("Storage key does not belong to this listing.");
        }

        var info = await storage.GetObjectInfoAsync(request.StorageKey, ct);
        if (info is null)
        {
            return BadRequest("No uploaded object was found at that storage key.");
        }
        if (!AllowedImageContentTypes.ContainsKey(info.ContentType))
        {
            return BadRequest("Unsupported content type.");
        }
        if (info.SizeBytes > MaxImageSizeBytes)
        {
            return BadRequest($"Image exceeds the maximum allowed size of {MaxImageSizeBytes / (1024 * 1024)}MB.");
        }

        var position = await db.ListingImages.CountAsync(i => i.ListingId == id, ct);
        var image = new ListingImage
        {
            Id = Guid.NewGuid(),
            ListingId = id,
            StorageKey = request.StorageKey,
            Position = position,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.ListingImages.Add(image);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id }, new { image.Id, image.StorageKey, image.Position });
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: "23505" };
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

public record UpdateListingRequest(
    int? MakeId,
    int? ModelId,
    string? Trim,
    int? Year,
    int? Mileage,
    decimal? EngineCapacityLitres,
    decimal? Price,
    FuelType? FuelType,
    TransmissionType? Transmission,
    BodyType? BodyType,
    string? Description,
    string? Postcode,
    ListingStatus? Status,
    decimal? SalePrice);

public record RequestImageUploadRequest(string ContentType);

public record ConfirmImageUploadRequest(string StorageKey);
