using System.Net.Http.Headers;
using CarShell.Web.Data;
using CarShell.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<CarShellDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Postgres"),
        npgsql => npgsql.UseNetTopologySuite()));

// Auth & Security Architecture: the client decides nothing about permissions.
// Supabase Auth issues the JWT; ASP.NET Core's own bearer middleware
// validates it on every request, this is standard middleware configuration,
// not custom auth code.
var supabaseUrl = builder.Configuration["Supabase:Url"]
    ?? throw new InvalidOperationException(
        "Supabase:Url is not configured — set it in appsettings.Development.json or user secrets.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"{supabaseUrl}/auth/v1";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"{supabaseUrl}/auth/v1",
            ValidateAudience = true,
            ValidAudience = "authenticated",
            ValidateLifetime = true,
            RoleClaimType = "role",
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Phase 1: only the admin role can create/edit/delete listings.
    // Phase 2 widens this to "admin OR (seller AND owns the listing AND
    // has an active subscription)" — an authorization rule change, not a
    // new auth system.
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
});

// Distance + Price Filtering: postcodes.io resolves a postcode to lat/lng
// once per postcode, cached in PostcodeGeocodes.
builder.Services.AddHttpClient<IGeocodingService, PostcodesIoGeocodingService>(client =>
{
    client.BaseAddress = new Uri("https://api.postcodes.io/");
});

// Image Upload Pipeline: talks to Supabase Storage's REST API using the
// service-role key, never exposed to the client. Needs Supabase:Url and
// Supabase:ServiceRoleKey configured to actually work end-to-end.
builder.Services.AddHttpClient<ISupabaseStorageService, SupabaseStorageService>((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri($"{config["Supabase:Url"]}/storage/v1/");
    var serviceRoleKey = config["Supabase:ServiceRoleKey"];
    if (!string.IsNullOrEmpty(serviceRoleKey))
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", serviceRoleKey);
    }
});

builder.Services.AddRazorPages();
builder.Services.AddControllers();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();
