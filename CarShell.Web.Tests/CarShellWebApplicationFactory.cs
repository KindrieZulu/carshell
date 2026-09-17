using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CarShell.Web.Tests;

// A real in-process ASP.NET Core host, used for tests that need to go
// through the actual HTTP pipeline (JSON formatters, model binding,
// middleware) rather than calling a controller method directly in C# —
// see HttpSerializationTests for why that distinction matters.
public class CarShellWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Postgres", TestDb.ConnectionString);
        // A placeholder is fine here — these tests only exercise public,
        // unauthenticated endpoints, so the JWT bearer options never get used.
        builder.UseSetting("Supabase:Url", "https://example.supabase.co");
        builder.UseEnvironment("Development");
    }
}
