# CarShell — Phase 0

The lean, single-service build described in the [architecture doc](https://claude.ai/artifact/f24e2e3e-7249-40e3-8c30-a7ea07052296):
one ASP.NET Core app serving both the API and server-rendered public pages, backed by
PostgreSQL + PostGIS on Supabase. No separate frontend, no Redis, no background job queue yet —
see the doc's "Path to Real-World Ready" section for when each of those gets added.

This is a Zimbabwe-based marketplace: prices are in USD, distances are in kilometres, and
location is a city/suburb picked from a reference table rather than a postcode — Zimbabwe has no
national postcode system the way the UK does. See "Location model" below.

## Prerequisites

- .NET 8 SDK
- A Postgres database with the `postgis` extension available (a local Postgres+PostGIS
  container works for dev; Supabase provides this in every project for the real environment)
- A Supabase project, for Supabase Auth

## First-time setup

```bash
cd CarShell.Web
dotnet restore
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=carshell;Username=postgres;Password=postgres"
dotnet user-secrets set "Supabase:Url" "https://your-project.supabase.co"
dotnet user-secrets set "Supabase:AnonKey" "<anon/publishable key, from Settings -> API>"
dotnet ef database update
dotnet run
```

`appsettings.Development.json` already has placeholder values so the app starts without secrets
configured, but replace them with `dotnet user-secrets` for anything real — that file is
gitignored on purpose.

## Location model

Listings are placed by picking a `Suburb` (city + area name, e.g. "Borrowdale, Harare") from a
seeded reference table, each with a fixed lat/lng — see Bootstrapping reference data below. This
sidesteps needing any external geocoding API: the coordinates are known upfront, same as
Makes/VehicleModels. The search page also asks the buyer's browser for their location on first
visit (if permission is granted) so it can show nearby listings by default, using the same
PostGIS radius query as an explicit search — see `Index.cshtml`'s `Scripts` section.

## What's actually implemented vs. stubbed

- Search, filtering (price, distance via PostGIS in km, make/model/year/engine capacity/fuel/
  transmission/body type), and listing detail: implemented, both as JSON (`/api/listings`) and
  as the server-rendered pages (`/`, `/listings/{id}`).
- Creating, editing, and deleting a listing: implemented
  (`POST`/`PATCH`/`DELETE /api/listings/{id}`) — VIN format validation, per-seller
  duplicate-VIN rejection, suburb-based location, and an append-only `ListingStatusEvents` row
  on every status change. Delete is a soft delete (`status = removed`) so sold/dispatched
  reporting keeps working against archived listings.
- An admin UI at `/admin/login` → `/admin/listings`: log in with a Supabase-issued admin
  account, see all of your own listings regardless of status (`GET /api/listings/mine`),
  create/edit/mark-sold/delete, and upload images. This is the "admin upload flow" component
  from the design doc — before this, the only way to create a listing was a raw API call with a
  manually-fetched bearer token. It's plain vanilla JS calling the same API everything else
  uses (see `wwwroot/js/admin-auth.js`); no separate frontend framework, per Phase 0. The
  Supabase anon/publishable key is injected into the login page from `Supabase:AnonKey` config —
  safe to expose client-side, it's Supabase's public, rate-limited key, not a secret.
- The image upload pipeline: implemented and verified end-to-end against a real Supabase
  Storage bucket (`POST /api/listings/{id}/images/upload-url` and `/confirm`) — pre-signed
  direct upload, then a server-side re-check of the uploaded object's actual size and content
  type on confirm, rejecting unsupported content types and storage keys that don't belong to
  the listing being confirmed. Needs `Supabase:ServiceRoleKey` set and a `listing-images`
  bucket created in the target project (Storage → New bucket), set **public** — car photos are
  meant to be publicly visible, so both the public listing detail page and the admin's image
  list render `{Supabase:Url}/storage/v1/object/public/{Supabase:StorageBucket}/{storageKey}`
  directly rather than through a signed URL.
- The first admin account: no signup UI. Create the Supabase Auth user by hand (Authentication
  → Users → Add user, in the Supabase dashboard), then insert a matching row into `Users` with
  `Role = 'Admin'` and the same `Id` (the Supabase user's UUID) — see Bootstrapping the first
  admin below.
- Structured logging via Serilog, writing to the console (Render's own log stream captures
  stdout at Phase 0, no separate logging service needed) — every request via
  `UseSerilogRequestLogging()`, every `AdminOnly` authorization decision, and every listing
  state change (create, status transition, soft delete), all called out by name in the design
  doc's own "what's missing" review.
- A generated OpenAPI spec at `/swagger/v1/swagger.json` (interactive UI at `/swagger` in
  Development), via Swashbuckle — the doc's own review flags "no API contract" as a real gap
  for a backend meant to eventually serve a mobile client too.
- Optimistic concurrency control on listing edits: `Listing.Version` maps to Postgres's own
  `xmin` system column (no extra column, no migration writes anything — Npgsql's migration
  generator recognizes this pattern and applies no real DDL). `PATCH /api/listings/{id}` requires
  the caller's last-known `version`; a write based on a stale version returns `409 Conflict`
  rather than silently clobbering someone else's edit. Closes the "two admins editing the same
  listing" gap the design doc's review calls out as cheap to close.
- A privacy policy page (`/Privacy`) with the Phase 1 baseline content from the design doc's Data
  Protection & Privacy section — a starting draft, not reviewed legal text.

## Authorization: DB-backed, not a JWT role claim

Supabase's own JWT `role` claim is always `"authenticated"` for any logged-in user — it's the
Postgres role Supabase uses for RLS, not an app-level permission. A policy built on
`RequireRole("admin")` would never succeed against a real Supabase token. `AdminAuthorizationHandler`
(`CarShell.Web/Auth/AdminRequirement.cs`) checks the caller's `sub` claim against the `Users`
table instead: the JWT proves who they are, the database decides what they're allowed to do.

## Bootstrapping the first admin

1. In the Supabase dashboard: **Authentication → Users → Add user → Create new user**. Set an
   email and password, and confirm the email automatically. Copy the new user's UUID.
2. Point the app at that project:
   ```bash
   dotnet user-secrets set "Supabase:Url" "https://your-project.supabase.co"
   ```
3. Insert the matching admin row (uses the UUID and email from step 1):
   ```sql
   INSERT INTO "Users" ("Id", "Email", "Role", "ContactEmail", "CreatedAt")
   VALUES ('<uuid-from-step-1>', '<email-from-step-1>', 'Admin', '<email-from-step-1>', now())
   ON CONFLICT ("Id") DO UPDATE SET "Role" = 'Admin';
   ```
4. To get a bearer token for testing the admin endpoints, call Supabase Auth's password grant
   directly (needs the project's anon/publishable key, from Settings → API):
   ```bash
   curl -s "https://your-project.supabase.co/auth/v1/token?grant_type=password" \
     -H "apikey: <anon-key>" -H "Content-Type: application/json" \
     -d '{"email": "<email-from-step-1>", "password": "<the password you set>"}'
   ```
   Use the `access_token` from the response as `Authorization: Bearer <token>` against
   `/api/listings`.

## Tests

`CarShell.Web.Tests` covers VIN validation, and the search/write paths as integration tests
against a real Postgres+PostGIS database (an in-memory provider can't reproduce the PostGIS
radius filter) — the design doc calls this out explicitly as a Phase 0 must-have.
`HttpSerializationTests` goes through the real HTTP/JSON pipeline via `WebApplicationFactory`
rather than calling controllers directly, specifically to catch response-serialization and
routing bugs the other tests can't see.

```bash
createdb -U postgres -h localhost -p 5432 carshell_test
dotnet ef database update --project CarShell.Web/CarShell.Web.csproj --connection "Host=localhost;Port=5432;Database=carshell_test;Username=postgres;Password=<yours>"
dotnet test
```

Set `CARSHELL_TEST_DB` to point tests at a different connection string; it defaults to
`Host=localhost;Port=5432;Database=carshell_test;Username=postgres;Password=123passed`.
`.github/workflows/ci.yml` runs the same suite against a `postgis/postgis` service container on
every push and PR to `main`. xUnit's parallel test-collection execution is disabled
(`xunit.runner.json`) since the HTTP-level tests commit real rows to the shared test database
rather than using the other tests' rollback-transaction isolation.

## Bootstrapping reference data

Both scripts are idempotent, safe to re-run:

```bash
psql -U postgres -h localhost -p 5432 -d carshell -f CarShell.Web/Data/Seed/seed-makes-models.sql
psql -U postgres -h localhost -p 5432 -d carshell -f CarShell.Web/Data/Seed/seed-suburbs.sql
```

- `seed-makes-models.sql`: 26 common makes, ~140 models.
- `seed-suburbs.sql`: Zimbabwe's major cities and a selection of Harare/Bulawayo suburbs, each
  with an approximate centre-point lat/lng — needed before a listing can be created.
