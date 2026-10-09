# Project Memory

A running log of what this codebase is and the decisions behind it. Append new entries with a date; don't rewrite history — if something changes, add a new entry noting what superseded what.

## 2026-09-29 — Initial scaffold: C# API created from arlink28-nextjs ADRs 0004 + 0005

**Why this repo exists:** the NestJS API inside `arlink28-nextjs` was removed on 2026-09-29 per ADRs 0004 (VPS + Docker hosting) and 0005 (C# / ASP.NET Core / EF Core on PostgreSQL). This repo is the C# implementation of that decision.

**Architecture reference:** the project structure mirrors `bslAuction-backend` (`C:\WCBS_Projects\Experiments\Claude\bsl\bslAuction-backend`) — feature-folders vertical slices, Scrutor DI, `Result<T>` / `ApiResponse<T>` helpers, JWT Bearer via `IConfigureNamedOptions<JwtBearerOptions>`.

**What was built in this session:**

- `.NET 10` / `ASP.NET Core` project (`Arlink28.Api.csproj`, `net10.0`). No .NET 9 runtime was installed on the dev machine; .NET 10 SDK (10.0.400) was used throughout.
- **EF Core 10 + Npgsql 10** targeting Supabase PostgreSQL. All 13 Prisma tables from `arlink28-nextjs/packages/db/prisma/schema.prisma` ported to C# entity classes. IDs changed from `String @db.Char(36)` (UUIDv7 strings) to `Guid` — PostgreSQL `uuid` type, generated with `Guid.NewGuid()`.
- **Initial migration** (`20260929080838_InitialCreate`) — 15 tables (14 domain + `__EFMigrationsHistory`), 437-line SQL script.
- **Feature folders:** `Features/Catalogue/` with `PackagesController`, `ReferenceController`, `CatalogueService`, request/response models. `Features/Auth/` placeholder.
- **Pricing engine** (`Helpers/PricingEngine.cs`) — direct port of the TypeScript `quote()` function. Input type renamed `PricingRequest` (to avoid collision with `Features/Catalogue/RequestModels/QuoteRequest`).
- **Phase 1 public routes:** `GET /api/v1/packages`, `/:slug`, `/:slug/quote`, `/api/v1/destinations`, `/api/v1/partners`, `/health`.

**Gotchas found:**

- **.NET 9 not installed.** Targeting `net9.0` (to match bslAuction-backend) fails because only .NET 10 SDK is present. Switched to `net10.0`. Package versions bumped accordingly (EF Core 10.0.0, Npgsql 10.0.0, Microsoft.AspNetCore.* 10.0.0, Swashbuckle 7.3.1, Serilog 9.0.0, health check packages 9.0.0).
- **AutoMapper 13/14 both have a known high-severity vulnerability** (GHSA-rvv3-g6hj-g44x). Since mappings are written manually in `CatalogueService.cs`, AutoMapper was removed from the csproj entirely.
- **`QuoteRequest` name collision.** `Helpers/PricingEngine.cs` originally defined `QuoteRequest`; so did `Features/Catalogue/RequestModels/QuoteRequest.cs`. C# emitted CS0104 ambiguous reference. Fixed by renaming the engine's input type to `PricingRequest`.
- **`Microsoft.Extensions.Diagnostics.HealthChecks` warning.** This package is part of the .NET 10 framework and shouldn't be listed as a `PackageReference` — removed from the csproj.
- **Duplicate `using Arlink28.Api.Helpers`** in `CatalogueService.cs` after the name-collision fix. Removed the duplicate.

**Migration gotchas (Supabase pooler):**

The EF CLI (`dotnet ef database update`) connects, reads the migration history table, catches a "table not found" error, then opens a **second connection** to verify the database exists. This second connection times out against the Supabase shared pooler (PgBouncer transaction mode drops idle connections between statement groups). Root causes investigated and ruled out in order:
- `No Prepare=true` — not a valid Npgsql 10 connection string key (removed from `appsettings.Development.json`).
- `SSL Mode=Require` — made no difference.
- Direct connection `db.zpyacljkgrgadwgnsqyc.supabase.co:5432` — DNS not resolvable from this network (port 5432 is blocked).

**Resolution:** generated the idempotent SQL script (`dotnet ef migrations script --idempotent`) and applied it via a temporary node.js runner using the `pg` package in a single connection. The script had a UTF-8 BOM (`﻿`) prepended by `dotnet ef` — stripped with `.replace(/^﻿/, '')` before sending to PostgreSQL.

**Migration verification:** `information_schema.tables` confirms 15 tables in the `public` schema; `__EFMigrationsHistory` has one row (`20260929080838_InitialCreate`, `10.0.0`).

**Still open:**
- No tests exist yet (`tests/` directory not scaffolded).
- `Features/Auth/` is a placeholder — no login endpoint, no token issuance.
- TypeScript client for `arlink28-nextjs` has not been generated from the OpenAPI spec.
- `appsettings.Development.json` contains the real Supabase password — should be moved to dotnet user-secrets (see `docs/security.md`).
- The `.migration-runner/` temp directory may still exist on disk (locked by a process during cleanup); it is in `.gitignore`.

## 2026-10-05 — Supabase Storage provider for uploaded photos

`SupabaseMediaStorage` (`Features/Shared/Services`) is selected by `MediaStorage:Provider=Supabase`; `LocalDiskMediaStorage` stays the default and is now registered explicitly in `Program.cs` (it no longer carries the `ISingleton` marker, which would have registered both). With Supabase on, `/media/{**path}` redirects to the public bucket instead of using `UseStaticFiles`. Reason: Render's container disk is wiped on every deploy. Written on a machine without the .NET 10 SDK, so Render's build was its first compile; no unit tests yet. `IMediaStorage.Delete` is synchronous, so the Supabase version blocks on the HTTP call (rare admin action). Staging setup: `arlink28-nextjs/docs/staging.md`.

## 2026-10-09 — Destination profiles and admin endpoints

Destinations are now a two-level tree (country, place) with Draft/Published status, a hero photo, and ordered attractions. Public: `GET /destinations[?country&kind]`, `GET /destinations/{slug}`. Staff: `Features/AdminDestinations` (`/admin/destinations`: list, get, create, patch, `PUT attractions`, `POST hero`, `POST attractions/{id}/photo`, publish, unpublish, delete). Decisions: Kind and parent are fixed at creation; the slug locks once `PublishedAt` is set (even after unpublish); publish needs a hero, a summary, and an attraction (place) or a published place (country); delete is a 409 while places, packages or properties use it (new `ConflictException`, mapped to 409 in `ExceptionHandlingMiddleware`). Photo credits (`HeroCredit`, `PhotoCredit`) are stored for Unsplash attribution. Schema: `docs/migrations/004_destination_profiles.sql`, applied by hand like 002 and 003. Not yet applied to the dev database when this was written.

## 2026-10-09 - Destination content seeder

`dotnet run -- seed-destinations [--photos=<folder>] [--dry-run] [--allow-production]` loads the first write-ups for the African destinations (`Data/Seed/destination-content.json`, embedded in the assembly) through `DestinationContentSeeder`. It only fills empty fields, empty attraction lists and missing hero photos, so staff edits survive a re-run; it never changes Status or PublishedAt (staff review and publish in the admin). Photos are read from `--photos` and saved through the configured `IMediaStorage`. The command prints a hint when migration 004 has not been applied (Postgres 42703/42P01). Tests: `DestinationContentTests` checks the shipped text against the column limits; `DestinationContentSeederTests` covers fill, idempotence, never-overwrite, dry run and bad photos. Not run against the dev database when written: the owner applies 004 and runs it. The copy was written without a source check; `arlink28-nextjs/docs/destinations-content.md` lists what to verify.
