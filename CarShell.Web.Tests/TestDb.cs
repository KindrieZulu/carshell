using CarShell.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace CarShell.Web.Tests;

// Points at a dedicated carshell_test database (see README) — the design
// doc explicitly calls for "an integration test against a real Postgres
// instance" for the filtering/search logic rather than mocking it away,
// since the radius filter depends on PostGIS behavior an in-memory
// provider can't reproduce.
public static class TestDb
{
    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("CARSHELL_TEST_DB")
        ?? "Host=localhost;Port=5432;Database=carshell_test;Username=postgres;Password=123passed";

    public static CarShellDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CarShellDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.UseNetTopologySuite())
            .Options;
        return new CarShellDbContext(options);
    }
}
