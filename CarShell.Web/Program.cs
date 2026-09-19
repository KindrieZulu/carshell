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

// A safety net, not the primary defense (that's request validation in each
// controller): whatever exception slips through unhandled on an /api/* route,
// in every environment including Development, the caller gets a clean JSON
// ProblemDetails response and never a raw stack trace. Scoped to /api so
// Razor Pages keeps its own HTML error handling below.
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api"),
    apiApp => apiApp.UseExceptionHandler(errorApp => errorApp.Run(async context =>
    {
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        context.RequestServices.GetRequiredService<ILogger<Program>>()
            .LogError(exception, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            title = "An unexpected error occurred.",
            status = 500,
            traceId = context.TraceIdentifier,
        });
    })));

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

// A non-exception failure result (NotFound() from a Razor page, e.g. a
// listing that has been removed or never existed) otherwise reaches the
// browser as a bare, empty-body 404 -- Kestrel does not render anything
// for those on its own. Scoped away from /api the same way the exception
// handler above is: that surface already returns structured JSON via
// ApiController's built-in ProblemDetails behavior and re-executing it as
// HTML would break that contract.
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api"),
    htmlApp => htmlApp.UseStatusCodePagesWithReExecute("/Error/{0}"));

app.UseHttpsRedirection();

// Without explicit Cache-Control, some browsers/proxies apply heuristic
// freshness (RFC 7234) to CSS/JS and keep serving a pre-deploy copy for a
// long time with no revalidation at all -- found by hitting exactly that
// with a caching layer during this session's own testing. "no-cache" still
// lets the browser cache the file, it just forces an If-None-Match
// revalidation on every request, so a changed file is always picked up.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache",
});
app.UseRouting();

// The admin area (login, MFA, dashboard) must never be served from the
// browser's back/forward cache: after a real logout, pressing back then
// forward could otherwise repaint an authenticated page before its own
// script re-checks anything. no-store is what actually disqualifies a
// page from bfcache in modern browsers -- the pageshow/persisted reload
// each of these pages also does is defense in depth on top of this, not
// the primary fix.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/admin"))
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Task.CompletedTask;
        });
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();

// Makes the top-level Program class visible to WebApplicationFactory<Program> in tests.
public partial class Program;
