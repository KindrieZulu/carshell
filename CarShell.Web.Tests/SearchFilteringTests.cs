using CarShell.Web.Controllers;
using Microsoft.Extensions.Logging.Abstractions;
using CarShell.Web.Data;
using CarShell.Web.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;
using NetTopologySuite.Geometries;
using Xunit;

namespace CarShell.Web.Tests;

// Integration test against a real Postgres+PostGIS instance, per the design
// doc's explicit ask: the radius filter's behavior depends on PostGIS, which
// an in-memory provider can't reproduce. Each test runs in its own
// transaction that's rolled back afterward, so the shared carshell_test
// database never accumulates test data.
public class SearchFilteringTests : IAsyncLifetime
{
    private static readonly GeometryFactory GeometryFactory =
        NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    private CarShellDbContext _db = default!;
    private IDbContextTransaction _transaction = default!;
    private int _makeId;
    private int _modelId;
    private int _suburbId;

    public async Task InitializeAsync()
    {
        _db = TestDb.CreateContext();
        _transaction = await _db.Database.BeginTransactionAsync();

        var make = new Make { Name = $"TestMake-{Guid.NewGuid():N}" };
        _db.Makes.Add(make);
        var suburb = new Suburb { City = "Harare", Name = $"TestSuburb-{Guid.NewGuid():N}", Lat = -17.8292, Lng = 31.0522 };
        _db.Suburbs.Add(suburb);
        await _db.SaveChangesAsync();

        var model = new VehicleModel { MakeId = make.Id, Name = "TestModel" };
        _db.VehicleModels.Add(model);
        await _db.SaveChangesAsync();

        _makeId = make.Id;
        _modelId = model.Id;
        _suburbId = suburb.Id;
    }

    public async Task DisposeAsync()
    {
        await _transaction.RollbackAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Search_filters_by_price_range()
    {
        var sellerId = await SeedAdminUser();
        _db.Listings.AddRange(
            BuildListing(5000m, -17.8292, 31.0522, sellerId),
            BuildListing(10000m, -17.8292, 31.0522, sellerId),
            BuildListing(20000m, -17.8292, 31.0522, sellerId));
        await _db.SaveChangesAsync();

        var controller = new ListingsController(_db, new ThrowingStorageService(), NullLogger<ListingsController>.Instance);
        var result = await controller.Search(
            lat: null, lng: null, radiusKm: null,
            minPrice: 8000m, maxPrice: 15000m,
            make: null, model: null, minYear: null, maxYear: null,
            minEngineCapacity: null, maxEngineCapacity: null,
            fuelType: null, transmission: null, bodyType: null,
            sort: null, page: 1, ct: default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var listings = Assert.IsAssignableFrom<IEnumerable<ListingSummary>>(ok.Value).ToList();

        Assert.Single(listings);
        Assert.Equal(10000m, listings[0].Price);
    }

    [Fact]
    public async Task Search_filters_by_radius()
    {
        var sellerId = await SeedAdminUser();
        var near = BuildListing(10000m, -17.8292, 31.0522, sellerId); // Harare
        var far = BuildListing(10000m, -20.1500, 28.5833, sellerId); // Bulawayo, ~440km away
        _db.Listings.AddRange(near, far);
        await _db.SaveChangesAsync();

        var controller = new ListingsController(_db, new ThrowingStorageService(), NullLogger<ListingsController>.Instance);
        var result = await controller.Search(
            lat: -17.8292, lng: 31.0522, radiusKm: 20,
            minPrice: null, maxPrice: null,
            make: null, model: null, minYear: null, maxYear: null,
            minEngineCapacity: null, maxEngineCapacity: null,
            fuelType: null, transmission: null, bodyType: null,
            sort: null, page: 1, ct: default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var listings = Assert.IsAssignableFrom<IEnumerable<ListingSummary>>(ok.Value).ToList();

        Assert.Single(listings);
        Assert.Equal(near.Id, listings[0].Id);
    }

    [Fact]
    public async Task Search_excludes_non_active_listings()
    {
        var sellerId = await SeedAdminUser();
        var draft = BuildListing(10000m, -17.8292, 31.0522, sellerId);
        draft.Status = ListingStatus.Draft;
        _db.Listings.Add(draft);
        await _db.SaveChangesAsync();

        var controller = new ListingsController(_db, new ThrowingStorageService(), NullLogger<ListingsController>.Instance);
        var result = await controller.Search(
            lat: null, lng: null, radiusKm: null,
            minPrice: null, maxPrice: null,
            make: null, model: null, minYear: null, maxYear: null,
            minEngineCapacity: null, maxEngineCapacity: null,
            fuelType: null, transmission: null, bodyType: null,
            sort: null, page: 1, ct: default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var listings = Assert.IsAssignableFrom<IEnumerable<ListingSummary>>(ok.Value).ToList();

        Assert.DoesNotContain(listings, l => l.Id == draft.Id);
    }

    private Listing BuildListing(decimal price, double lat, double lng, Guid sellerId) => new()
    {
        Id = Guid.NewGuid(),
        SellerId = sellerId,
        MakeId = _makeId,
        ModelId = _modelId,
        Year = 2020,
        Mileage = 10000,
        EngineCapacityLitres = 1.6m,
        Price = price,
        FuelType = FuelType.Petrol,
        Transmission = TransmissionType.Manual,
        BodyType = BodyType.Hatchback,
        Vin = Guid.NewGuid().ToString("N")[..17].ToUpperInvariant(),
        Status = ListingStatus.Active,
        SuburbId = _suburbId,
        Lat = lat,
        Lng = lng,
        Location = GeometryFactory.CreatePoint(new Coordinate(lng, lat)),
    };

    private async Task<Guid> SeedAdminUser()
    {
        var user = new User { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@test.local", Role = UserRole.Admin };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user.Id;
    }
}
