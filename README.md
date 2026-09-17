# CarShell — Phase 0

The lean, single-service build described in the [architecture doc](https://claude.ai/artifact/f24e2e3e-7249-40e3-8c30-a7ea07052296):
one ASP.NET Core app serving both the API and server-rendered public pages, backed by
PostgreSQL + PostGIS on Supabase. No separate frontend, no Redis, no background job queue yet —
see the doc's "Path to Real-World Ready" section for when each of those gets added.

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
dotnet ef database update
dotnet run
```

`appsettings.Development.json` already has placeholder values so the app starts without secrets
configured, but replace them with `dotnet user-secrets` for anything real — that file is
gitignored on purpose.

## What's actually implemented vs. stubbed

- Search, filtering (price, distance via PostGIS, make/model/year/engine capacity/fuel/
  transmission/body type), and listing detail: implemented, both as JSON (`/api/listings`) and
  as the server-rendered pages (`/`, `/listings/{id}`).
- Creating, editing, and deleting a listing: implemented
  (`POST`/`PATCH`/`DELETE /api/listings/{id}`) — VIN format validation, per-seller
  duplicate-VIN rejection, postcode
  geocoding via postcodes.io (cached in `PostcodeGeocodes`), and an append-only
  `ListingStatusEvents` row on every status change. Delete is a soft delete (`status = removed`)
  so sold/dispatched reporting keeps working against archived listings.
- The image upload pipeline: implemented (`POST /api/listings/{id}/images/upload-url` and
  `/confirm`) against Supabase Storage's REST API — pre-signed direct upload, then a
  server-side re-check of the uploaded object's actual size and content type on confirm. This
  needs a real Supabase project's `Supabase:ServiceRoleKey` to work end-to-end; it hasn't been
  exercised against a live bucket yet.
- The first admin account: no signup UI. Create the Supabase Auth user by hand (Authentication
  → Users → Add user, in the Supabase dashboard), then insert a matching row into `Users` with
  `Role = 'Admin'` and the same `Id` (the Supabase user's UUID) — see Bootstrapping the first
  admin below.
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
   directly (needs the project's anon key, from Settings → API):
   ```bash
   curl -s "https://your-project.supabase.co/auth/v1/token?grant_type=password"      -H "apikey: <anon-key>" -H "Content-Type: application/json"      -d '{"email": "<email-from-step-1>", "password": "<the password you set>"}'
   ```
   Use the `access_token` from the response as `Authorization: Bearer <token>` against
   `/api/listings`.

## Tests

`CarShell.Web.Tests` covers VIN validation, the geocoding cache, and the search/write paths as
integration tests against a real Postgres+PostGIS database (an in-memory provider can't
reproduce the PostGIS radius filter) — the design doc calls this out explicitly as a Phase 0
must-have. One test hits the real postcodes.io API.

```bash
createdb -U postgres -h localhost -p 5432 carshell_test
dotnet ef database update --project CarShell.Web/CarShell.Web.csproj --connection "Host=localhost;Port=5432;Database=carshell_test;Username=postgres;Password=<yours>"
dotnet test
```

Set `CARSHELL_TEST_DB` to point tests at a different connection string; it defaults to
`Host=localhost;Port=5432;Database=carshell_test;Username=postgres;Password=123passed`.
`.github/workflows/ci.yml` runs the same suite against a `postgis/postgis` service container on
every push and PR to `main`.

## Bootstrapping reference data

`makes` and `models` need seeding before search is useful. Run the seed script (26 common
UK-market makes, ~140 models) against your database — it's idempotent, safe to re-run:

```bash
psql -U postgres -h localhost -p 5432 -d carshell -f CarShell.Web/Data/Seed/seed-makes-models.sql
```
