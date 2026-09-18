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
    CarShellDbContext db, ISupabaseStorageService storage, ILogger<ListingsController> logger) : ControllerBase
{
    private static readonly GeometryFactory GeometryFactory =
        NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    private const int PageSize = 24;
    private const double KmToMeters = 1000;
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
        [FromQuery] double? radiusKm,
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
        if (lat is not null && lng is not null && radiusKm is not null)
        {
            var origin = GeometryFactory.CreatePoint(new Coordinate(lng.Value, lat.Value));
            var radiusMeters = radiusKm.Value * KmToMeters;
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
                l.Id, l.Make.Name, l.Model.Name, l.Year, l.Price, l.Mileage, l.Suburb.Name, l.Suburb.City))
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
            .Include(l => l.Suburb)
            .FirstOrDefaultAsync(l => l.Id == id, ct);

        return listing is null ? NotFound() : Ok(ToDetail(listing));
    }

    // Backs the admin listing-management UI: every one of the caller's own
    // listings regardless of status, not just Active ones like the public
    // Search endpoint returns.
    [HttpGet("mine")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var sellerId = User.GetUserId();

        var listings = await db.Listings.AsNoTracking()
            .Where(l => l.SellerId == sellerId)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new MyListingSummary(
                l.Id, l.Make.Name, l.Model.Name, l.Year, l.Price, l.Status, l.Suburb.Name, l.Suburb.City))
            .ToListAsync(ct);

        return Ok(listings);
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

        var suburb = await db.Suburbs.FindAsync([request.SuburbId], ct);
        if (suburb is null)
        {
            return BadRequest("Unknown suburb.");
        }

        var model = await db.VehicleModels.FindAsync([request.ModelId], ct);
        if (model is null || model.MakeId != request.MakeId)
        {
            return BadRequest("Unknown make/model, or the model does not belong to the given make.");
        }

        var rangeError = ValidateVehicleRanges(
            request.Year, request.Mileage, request.EngineCapacityLitres, request.Price, request.Description);
        if (rangeError is not null)
        {
            return BadRequest(rangeError);
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
            SuburbId = suburb.Id,
            Lat = suburb.Lat,
            Lng = suburb.Lng,
            Location = GeometryFactory.CreatePoint(new Coordinate(suburb.Lng, suburb.Lat)),
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

        listing.Suburb = suburb;
        logger.LogInformation(
            "Listing {ListingId} created by {SellerId} (VIN {Vin})", listing.Id, sellerId, vin);
        return CreatedAtAction(nameof(GetById), new { id = listing.Id }, ToDetail(listing));
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateListingRequest request, CancellationToken ct)
    {
        var listing = await db.Listings.Include(l => l.Suburb).FirstOrDefaultAsync(l => l.Id == id, ct);
        if (listing is null)
        {
            return NotFound();
        }

        // Optimistic concurrency: the client must send back the Version it
        // last read. If someone else's write landed in between, the xmin
        // this compares against in the UPDATE's WHERE clause no longer
        // matches, zero rows update, and SaveChangesAsync throws below.
        db.Entry(listing).Property(l => l.Version).OriginalValue = request.Version;

        if (request.SuburbId is not null && request.SuburbId.Value != listing.SuburbId)
        {
            var suburb = await db.Suburbs.FindAsync([request.SuburbId.Value], ct);
            if (suburb is null)
            {
                return BadRequest("Unknown suburb.");
            }

            listing.SuburbId = suburb.Id;
            listing.Suburb = suburb;
            listing.Lat = suburb.Lat;
            listing.Lng = suburb.Lng;
            listing.Location = GeometryFactory.CreatePoint(new Coordinate(suburb.Lng, suburb.Lat));
        }

        if (request.MakeId is not null || request.ModelId is not null)
        {
            var newMakeId = request.MakeId ?? listing.MakeId;
            var newModelId = request.ModelId ?? listing.ModelId;

            var model = await db.VehicleModels.FindAsync([newModelId], ct);
            if (model is null || model.MakeId != newMakeId)
            {
                return BadRequest("Unknown make/model, or the model does not belong to the given make.");
            }

            listing.MakeId = newMakeId;
            listing.ModelId = newModelId;
        }

        var rangeError = ValidateVehicleRanges(
            request.Year, request.Mileage, request.EngineCapacityLitres, request.Price, request.Description);
        if (rangeError is not null)
        {
            return BadRequest(rangeError);
        }

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
            logger.LogInformation(
                "Listing {ListingId} status changed {FromStatus} -> {ToStatus} by {UserId}",
                listing.Id, listing.Status, request.Status.Value, User.GetUserId());
            listing.Status = request.Status.Value;
        }

        listing.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict("This listing was changed by someone else since you loaded it. Reload and try again.");
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Conflict("A listing with this VIN already exists for this seller.");
        }

        return Ok(ToDetail(listing));
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

        // Soft delete: the row and its status history stay, since
        // listing_status_events is the append-only source the sold/dispatched
        // reporting reads from — see Data Model in the design doc.
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
            logger.LogInformation("Listing {ListingId} soft-deleted by {UserId}", listing.Id, User.GetUserId());
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

    // Defense in depth against the DB columns' own limits (EngineCapacityLitres
    // is decimal(3,1), Price is decimal(10,2) -- see CarShellDbContext) and
    // against nonsense a client could otherwise slip through with no server-side
    // check at all, e.g. a negative price or a 9999 model year.
    private static string? ValidateVehicleRanges(
        int? year, int? mileage, decimal? engineCapacityLitres, decimal? price, string? description)
    {
        var maxYear = DateTime.UtcNow.Year + 1;
        if (year is not null && (year < 1950 || year > maxYear))
        {
            return $"Year must be between 1950 and {maxYear}.";
        }
        if (mileage is not null && mileage < 0)
        {
            return "Mileage cannot be negative.";
        }
        if (engineCapacityLitres is not null && (engineCapacityLitres <= 0 || engineCapacityLitres > 99.9m))
        {
            return "Engine capacity must be greater than 0 and no more than 99.9 litres.";
        }
        if (price is not null && price <= 0)
        {
            return "Price must be greater than 0.";
        }
        if (description is not null && description.Length > 5000)
        {
            return "Description cannot exceed 5000 characters.";
        }
        return null;
    }

    // Never serialize the entity's raw NetTopologySuite Point directly — its
    // unset Z coordinate is NaN, which System.Text.Json can't write, and it's
    // not data an API consumer needs anyway when Lat/Lng are already there.
    private static ListingDetail ToDetail(Listing listing) => new(
        listing.Id,
        listing.SellerId,
        listing.MakeId,
        listing.Make?.Name,
        listing.ModelId,
        listing.Model?.Name,
        listing.Trim,
        listing.Year,
        listing.Mileage,
        listing.EngineCapacityLitres,
        listing.Price,
        listing.FuelType,
        listing.Transmission,
        listing.BodyType,
        listing.Description,
        listing.Vin,
        listing.Status,
        listing.SuburbId,
        listing.Suburb?.Name,
        listing.Suburb?.City,
        listing.Lat,
        listing.Lng,
        listing.CreatedAt,
        listing.UpdatedAt,
        listing.Version,
        listing.Images.Select(i => new ListingImageSummary(i.Id, i.StorageKey, i.Position)).ToList());
}

public record ListingSummary(
    Guid Id, string Make, string Model, int Year, decimal Price, int Mileage, string Suburb, string City);

public record MyListingSummary(
    Guid Id, string Make, string Model, int Year, decimal Price, ListingStatus Status, string Suburb, string City);

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
    int SuburbId);

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
    int? SuburbId,
    ListingStatus? Status,
    decimal? SalePrice,
    uint Version);

public record RequestImageUploadRequest(string ContentType);

public record ConfirmImageUploadRequest(string StorageKey);

public record ListingImageSummary(Guid Id, string StorageKey, int Position);

public record ListingDetail(
    Guid Id,
    Guid SellerId,
    int MakeId,
    string? MakeName,
    int ModelId,
    string? ModelName,
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
    ListingStatus Status,
    int SuburbId,
    string? SuburbName,
    string? City,
    double Lat,
    double Lng,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version,
    IReadOnlyList<ListingImageSummary> Images);
