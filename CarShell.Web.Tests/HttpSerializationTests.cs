using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CarShell.Web.Controllers;
using CarShell.Web.Data;
using CarShell.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Xunit;

namespace CarShell.Web.Tests;

// Regression coverage for a real bug: GetById used to return the raw
// Listing entity, including its NetTopologySuite Point geometry. That
// Point's unset Z coordinate is NaN, which System.Text.Json can't write,
// so any listing with a location crashed the response with a 500 — a bug
// none of the other tests could see, because they call the controller
// method directly and never touch the real JSON pipeline. This test does.
public class HttpSerializationTests : IClassFixture<CarShellWebApplicationFactory>, IAsyncLifetime
{
    // Mirrors the JsonStringEnumConverter registered in Program.cs — the
    // server serializes enums as strings, so the test client needs to know
    // that to deserialize the response back.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    private readonly CarShellWebApplicationFactory _factory;
    private CarShellDbContext _db = default!;
    private Guid _listingId;
    private Guid _sellerId;
    private int _makeId;
    private int _modelId;

    public HttpSerializationTests(CarShellWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _db = TestDb.CreateContext();

        var make = new Make { Name = $"HttpTestMake-{Guid.NewGuid():N}" };
        var seller = new User { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@test.local", Role = UserRole.Admin };
        _db.Makes.Add(make);
        _db.Users.Add(seller);
        await _db.SaveChangesAsync();

        var model = new VehicleModel { MakeId = make.Id, Name = "HttpTestModel" };
        _db.VehicleModels.Add(model);
        await _db.SaveChangesAsync();

        var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var listing = new Listing
        {
            Id = Guid.NewGuid(),
            SellerId = seller.Id,
            MakeId = make.Id,
            ModelId = model.Id,
            Year = 2020,
            Mileage = 1000,
            EngineCapacityLitres = 1.6m,
            Price = 9999m,
            FuelType = FuelType.Petrol,
            Transmission = TransmissionType.Manual,
            BodyType = BodyType.Hatchback,
            Vin = "1HGCM82633A004352",
            Status = ListingStatus.Active,
            Postcode = "SW1A 1AA",
            Lat = 51.5074,
            Lng = -0.1278,
            Location = geometryFactory.CreatePoint(new Coordinate(-0.1278, 51.5074)),
        };
        _db.Listings.Add(listing);
        await _db.SaveChangesAsync();

        _listingId = listing.Id;
        _sellerId = seller.Id;
        _makeId = make.Id;
        _modelId = model.Id;
    }

    public async Task DisposeAsync()
    {
        await _db.Listings.Where(l => l.Id == _listingId).ExecuteDeleteAsync();
        await _db.VehicleModels.Where(m => m.Id == _modelId).ExecuteDeleteAsync();
        await _db.Makes.Where(m => m.Id == _makeId).ExecuteDeleteAsync();
        await _db.Users.Where(u => u.Id == _sellerId).ExecuteDeleteAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task GetById_serializes_a_listing_with_a_real_location_over_http()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/listings/{_listingId}");

        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<ListingDetail>(JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(_listingId, detail!.Id);
        Assert.Equal(51.5074, detail.Lat);
        Assert.Equal(FuelType.Petrol, detail.FuelType);
    }

    [Fact]
    public async Task Search_serializes_results_over_http()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/listings");

        response.EnsureSuccessStatusCode();
        var results = await response.Content.ReadFromJsonAsync<List<ListingSummary>>(JsonOptions);
        Assert.NotNull(results);
    }
}
