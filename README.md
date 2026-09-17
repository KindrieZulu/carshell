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
- Creating/editing/deleting a listing, and the image upload flow: stubbed (`501 Not Implemented`)
  with `TODO` comments — they depend on a geocoding call and Supabase Storage integration that
  aren't wired up in this scaffold yet.
- The first admin account: no signup UI. Create the Supabase Auth user by hand, then insert the
  matching row into `users` with `role = 'admin'` directly — see "Bootstrapping the first admin"
  in the design doc.

## Bootstrapping reference data

`makes` and `models` need seeding before search is useful. No seed script yet — insert rows
directly for now.
