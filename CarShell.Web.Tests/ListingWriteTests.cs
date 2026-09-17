using System.Security.Claims;
using CarShell.Web.Controllers;
using CarShell.Web.Data;
using CarShell.Web.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NetTopologySuite.Geometries;
using Xunit;

namespace CarShell.Web.Tests;

// Integration tests for the listing write path (create/update/delete),
// calling the controller directly rather than over HTTP — [Authorize]
// policies are enforced by ASP.NET Core's request pipeline, not by a plain
// method call, so this exercises exactly the logic these endpoints run
// without needing a real Supabase-issued admin JWT.
public class ListingWriteTests : IAsyncLifetime
{
    private CarShellDbContext _db = default!;
    private IDbContextTransaction _transaction = default!;
    private int _makeId;
    private int _modelId;
    private int _suburbId;
    private double _suburbLat;
    private double _suburbLng;
    private Guid _adminId;

    public async Task InitializeAsync()
    {
        _db = TestDb.CreateContext();
        _transaction = await _db.Database.BeginTransactionAsync();

        var make = new Make { Name = $"TestMake-{Guid.NewGuid():N}" };
        _db.Makes.Add(make);
        var suburb = new Suburb { City = "Harare", Name = $"TestSuburb-{Guid.NewGuid():N}", Lat = -17.8292, Lng = 31.0522 };
        _db.Suburbs.Add(suburb);
        var admin = new User { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@test.local", Role = UserRole.Admin };
        _db.Users.Add(admin);
        await _db.SaveChangesAsync();

        var model = new VehicleModel { MakeId = make.Id, Name = "TestModel" };
        _db.VehicleModels.Add(model);
        await _db.SaveChangesAsync();

        _makeId = make.Id;
        _modelId = model.Id;
        _suburbId = suburb.Id;
        _suburbLat = suburb.Lat;
        _suburbLng = suburb.Lng;
        _adminId = admin.Id;
    }

    public async Task DisposeAsync()
    {
        await _transaction.RollbackAsync();
        await _db.DisposeAsync();
    }

    private ListingsController BuildController()
    {
        var controller = new ListingsController(_db, new ThrowingStorageService());

        var claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, _adminId.ToString()) });
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(claims) },
        };
        return controller;
    }

    private CreateListingRequest ValidCreateRequest(string vin = "1HGCM82633A004352", int? suburbId = null) => new(
        MakeId: _makeId,
        ModelId: _modelId,
        Trim: "SE",
        Year: 2020,
        Mileage: 15000,
        EngineCapacityLitres: 1.6m,
        Price: 12000m,
        FuelType: FuelType.Petrol,
        Transmission: TransmissionType.Manual,
        BodyType: BodyType.Hatchback,
        Description: "A test listing.",
        Vin: vin,
        SuburbId: suburbId ?? _suburbId);

    [Fact]
    public async Task Create_creates_active_listing_with_the_suburbs_coordinates()
    {
        var controller = BuildController();

        var result = await controller.Create(ValidCreateRequest(), default);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var listing = Assert.IsType<ListingDetail>(created.Value);

        Assert.Equal(ListingStatus.Active, listing.Status);
        Assert.Equal(_suburbLat, listing.Lat);
        Assert.Equal(_suburbLng, listing.Lng);

        var events = await _db.ListingStatusEvents.Where(e => e.ListingId == listing.Id).ToListAsync();
        var createdEvent = Assert.Single(events);
        Assert.Equal(ListingStatus.Draft, createdEvent.FromStatus);
        Assert.Equal(ListingStatus.Active, createdEvent.ToStatus);
    }

    [Fact]
    public async Task Create_rejects_unknown_suburb()
    {
        var controller = BuildController();

        var result = await controller.Create(ValidCreateRequest(suburbId: 999_999), default);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_rejects_invalid_vin()
    {
        var controller = BuildController();

        var result = await controller.Create(ValidCreateRequest(vin: "TOO-SHORT"), default);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_rejects_duplicate_vin_for_same_seller()
    {
        var controller = BuildController();
        var vin = "1HGCM82633A004352";

        var first = await controller.Create(ValidCreateRequest(vin: vin), default);
        Assert.IsType<CreatedAtActionResult>(first);

        var second = await controller.Create(ValidCreateRequest(vin: vin), default);

        Assert.IsType<ConflictObjectResult>(second);
    }

    [Fact]
    public async Task Update_marking_sold_requires_sale_price()
    {
        var controller = BuildController();
        var created = (CreatedAtActionResult)await controller.Create(ValidCreateRequest(), default);
        var listing = (ListingDetail)created.Value!;

        var result = await controller.Update(
            listing.Id,
            new UpdateListingRequest(null, null, null, null, null, null, null, null, null, null, null, null,
                ListingStatus.Sold, null),
            default);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Update_marking_sold_records_status_event_with_sale_price()
    {
        var controller = BuildController();
        var created = (CreatedAtActionResult)await controller.Create(ValidCreateRequest(), default);
        var listing = (ListingDetail)created.Value!;

        var result = await controller.Update(
            listing.Id,
            new UpdateListingRequest(null, null, null, null, null, null, null, null, null, null, null, null,
                ListingStatus.Sold, 11500m),
            default);

        Assert.IsType<OkObjectResult>(result);

        var soldEvent = await _db.ListingStatusEvents
            .Where(e => e.ListingId == listing.Id && e.ToStatus == ListingStatus.Sold)
            .SingleAsync();
        Assert.Equal(11500m, soldEvent.SalePrice);
    }

    [Fact]
    public async Task Delete_soft_deletes_and_preserves_the_row()
    {
        var controller = BuildController();
        var created = (CreatedAtActionResult)await controller.Create(ValidCreateRequest(), default);
        var listing = (ListingDetail)created.Value!;

        var result = await controller.Delete(listing.Id, default);

        Assert.IsType<NoContentResult>(result);

        var reloaded = await _db.Listings.AsNoTracking().SingleAsync(l => l.Id == listing.Id);
        Assert.Equal(ListingStatus.Removed, reloaded.Status);

        var removedEvent = await _db.ListingStatusEvents
            .Where(e => e.ListingId == listing.Id && e.ToStatus == ListingStatus.Removed)
            .SingleAsync();
        Assert.Equal(ListingStatus.Active, removedEvent.FromStatus);
    }

    [Fact]
    public async Task GetMine_returns_all_of_the_callers_listings_regardless_of_status()
    {
        var controller = BuildController();
        var created = (CreatedAtActionResult)await controller.Create(ValidCreateRequest(), default);
        var listing = (ListingDetail)created.Value!;
        await controller.Delete(listing.Id, default); // soft-deletes to Removed

        var otherSeller = new User { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@test.local", Role = UserRole.Admin };
        _db.Users.Add(otherSeller);
        await _db.SaveChangesAsync();
        _db.Listings.Add(new Listing
        {
            Id = Guid.NewGuid(),
            SellerId = otherSeller.Id,
            MakeId = _makeId,
            ModelId = _modelId,
            Year = 2021,
            Mileage = 5000,
            EngineCapacityLitres = 2.0m,
            Price = 20000m,
            FuelType = FuelType.Diesel,
            Transmission = TransmissionType.Automatic,
            BodyType = BodyType.Suv,
            Vin = "WVWZZZ1JZXW000099",
            Status = ListingStatus.Active,
            SuburbId = _suburbId,
            Lat = _suburbLat,
            Lng = _suburbLng,
            Location = NetTopologySuite.NtsGeometryServices.Instance
                .CreateGeometryFactory(srid: 4326)
                .CreatePoint(new Coordinate(_suburbLng, _suburbLat)),
        });
        await _db.SaveChangesAsync();

        var result = await controller.GetMine(default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var mine = Assert.IsAssignableFrom<IEnumerable<MyListingSummary>>(ok.Value).ToList();

        var own = Assert.Single(mine);
        Assert.Equal(listing.Id, own.Id);
        Assert.Equal(ListingStatus.Removed, own.Status);
    }
}
