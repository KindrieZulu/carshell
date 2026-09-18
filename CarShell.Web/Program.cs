using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using CarShell.Web.Auth;
using CarShell.Web.Data;
using CarShell.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// System Logs & Reporting: structured logs for every request, every
// authorization decision, and every state change, shipped to whatever the
// hosting platform's own log stream captures from stdout — Render's
// built-in log stream at Phase 0, per the design doc, rather than a
// separate logging service nobody's set up yet.
builder.Host.UseSerilog((context, services, loggerConfig) => loggerConfig
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

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
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Phase 1: only the admin role can create/edit/delete listings.
    // Phase 2 widens this to "admin OR (seller AND owns the listing AND
    // has an active subscription)" — an authorization rule change, not a
    // new auth system. The check itself queries Users, not a JWT claim —
    // see AdminAuthorizationHandler.
    options.AddPolicy("AdminOnly", policy => policy.Requirements.Add(new AdminRequirement()));
    // Only a super admin can create other admin accounts — see SuperAdminRequirement.
    options.AddPolicy("SuperAdminOnly", policy => policy.Requirements.Add(new SuperAdminRequirement()));
});
builder.Services.AddScoped<IAuthorizationHandler, AdminAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, SuperAdminAuthorizationHandler>();

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

// Creating admin accounts: Supabase Auth's Admin API, same service-role key
// as Storage above, never exposed to the client. Unlike Storage, the Auth
// (GoTrue) routes behind Supabase's gateway reject a request that has
// Authorization but no apikey header — confirmed by testing directly
// against a real project, not assumed from docs.
builder.Services.AddHttpClient<ISupabaseAuthAdminService, SupabaseAuthAdminService>((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri($"{config["Supabase:Url"]}/auth/v1/");
    var serviceRoleKey = config["Supabase:ServiceRoleKey"];
    if (!string.IsNullOrEmpty(serviceRoleKey))
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", serviceRoleKey);
        client.DefaultRequestHeaders.Add("apikey", serviceRoleKey);
    }
});

builder.Services.AddRazorPages();
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// No API contract was one of the design doc's own "what's missing" findings:
// a backend serving a web client, and eventually a mobile client, should
// publish a generated, versioned OpenAPI spec rather than leaving "shared
// types across clients" as an aspiration with no actual mechanism.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "CarShell API", Version = "v1" });
});

var app = builder.Build();

app.UseSerilogRequestLogging();

// Prices are USD, formatted explicitly with a literal "$" in the Razor
// pages rather than ToString("C0") — a culture's currency symbol depends on
// its region's own currency (en-ZW would format as ZWL, not USD), which
// isn't what we want here. This just locks down number grouping (thousands
// separators) so it doesn't depend on whatever locale the host OS has.
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(CultureInfo.InvariantCulture),
    SupportedCultures = [CultureInfo.InvariantCulture],
    SupportedUICultures = [CultureInfo.InvariantCulture],
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();

// Makes the top-level Program class visible to WebApplicationFactory<Program> in tests.
public partial class Program;
