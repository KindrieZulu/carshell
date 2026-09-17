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
- The first admin account: no signup UI. Create the Supabase Auth user by hand, then insert the
  matching row into `users` with `role = 'admin'` directly — see "Bootstrapping the first admin"
  in the design doc.
- A privacy policy page (`/Privacy`) with the Phase 1 baseline content from the design doc's Data
  Protection & Privacy section — a starting draft, not reviewed legal text.

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
