using System.Net;
using System.Text;
using CarShell.Web.Data;
using CarShell.Web.Services;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace CarShell.Web.Tests;

public class GeocodingServiceTests : IAsyncLifetime
{
    private CarShellDbContext _db = default!;
    private IDbContextTransaction _transaction = default!;

    public async Task InitializeAsync()
    {
        _db = TestDb.CreateContext();
        _transaction = await _db.Database.BeginTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        await _transaction.RollbackAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task GeocodeAsync_caches_result_and_skips_second_http_call()
    {
        var handler = new CountingFakeHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.postcodes.io/") };
        var service = new PostcodesIoGeocodingService(httpClient, _db);

        var first = await service.GeocodeAsync("SW1A 1AA");
        var second = await service.GeocodeAsync("sw1a 1aa"); // same postcode, different casing/spacing

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first!.Lat, second!.Lat);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GeocodeAsync_returns_null_for_unresolvable_postcode()
    {
        var handler = new CountingFakeHandler(success: false);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.postcodes.io/") };
        var service = new PostcodesIoGeocodingService(httpClient, _db);

        var result = await service.GeocodeAsync("ZZ99 9ZZ");

        Assert.Null(result);
    }

    private class CountingFakeHandler(bool success = true) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            CallCount++;

            if (!success)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            const string json = """{"result": {"latitude": 51.5010, "longitude": -0.1416}}""";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }
}
