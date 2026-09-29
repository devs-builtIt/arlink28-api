# Architecture — arlink28-api

The C# REST API for ARLink28. Serves the Next.js frontend (`arlink28-nextjs`) and will serve the admin dashboard. Backed by Supabase PostgreSQL.

## Stack

| Component | Technology |
|-----------|-----------|
| Runtime | .NET 10 / ASP.NET Core |
| ORM | Entity Framework Core 10 + Npgsql (PostgreSQL) |
| Database | Supabase PostgreSQL 16 (eu-west-1) |
| Auth | JWT Bearer — `Microsoft.AspNetCore.Authentication.JwtBearer` |
| API docs | Swashbuckle / Swagger UI at `/swagger` |
| API versioning | URL segment (`/api/v1/...`) via `Asp.Versioning` |
| DI | Scrutor — auto-registers services by `ITransient` / `IScoped` / `ISingleton` marker interface |
| Logging | Serilog — structured JSON to console + rolling file |
| Validation | FluentValidation |
| Health | `/health` — checks Supabase connectivity (NpgSql + DbContext) |

## Feature folder layout

```
Features/
  Catalogue/
    Controllers/        PackagesController · ReferenceController
    Services/           CatalogueService (implements ICatalogueService)
    RequestModels/      PackageListRequest · QuoteRequest
    ResponseModels/     PackageCardResponse · PackageDetailResponse · QuoteResponse
                        DestinationResponse · PartnerResponse · ...
  Auth/                 Phase 2 scaffold (login, token refresh)
  Admin/                Phase 3 (package CRUD, media upload) — not yet built
  Bookings/             Phase 4 (seat hold, payment) — not yet built
```

Cross-cutting concerns:

```
Data/
  Entities/             15 entity classes (Guid IDs, BaseEntity / BaseAuditableEntity)
  Configuration/        IEntityTypeConfiguration<T> per entity (max lengths, indexes, FKs)
  ApplicationDbContext  15 DbSet properties
  ApplicationDbContextFactory  IDesignTimeDbContextFactory for migrations

Helpers/
  Problems.cs           ErrorCodes + ApiProblem(): RFC 9457 Problem Details errors
  Result.cs             Result<T> — explicit success/failure pattern
  AppException.cs       Throws 400 from middleware
  Money.cs              ToMinor / FromMinor / Format (8 currencies, never floats)
  PricingEngine.cs      Stateless Quote() — port of the TypeScript pricing logic
  Settings/AppSettings  JWT secret, issuer, audience, frontend URL
  OptionsSetup/         JwtBearerOptionsSetup · ConfigureSwaggerOptions · ConfigureCorsOptions

Middleware/
  ExceptionHandlingMiddleware   AppException → 400 · QuoteException → 422 · unhandled → 500
```

## Entity model (15 tables)

All primary keys are `Guid` → `uuid`. All timestamps are `DateTime` (UTC). Money is `long` minor units + `string` ISO currency.

```
Destinations ──< Properties ──< PackageStays >── Packages
Partners     ──< Properties                          │
Partners     ──< Seasons                    Packages ──< PackageStays
                    └──< SeasonRanges       Packages ──< PackageRates >── Seasons
                                            Packages ──< PackageFeatures >── Features
                                            Packages ──< PackageAddOns
                                            Packages ──< PackageMedia
                                Properties ──< PropertyMedia
                                AuditLog (append-only)
```

## Catalogue service — Phase 1 routes

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/v1/packages` | Cursor-paginated list of published packages, with filters |
| GET | `/api/v1/packages/:slug` | Full package detail with stays, features, rates, media |
| GET | `/api/v1/packages/:slug/quote` | Price a stay for a check-in date, nights, currency, add-ons |
| GET | `/api/v1/destinations` | All destinations (filter options for the catalogue) |
| GET | `/api/v1/partners` | All partners / co-branded lodges |
| GET | `/health` | Health check (NpgSql + DbContext) |

## Pricing engine

`Helpers/PricingEngine.cs` is a direct port of the TypeScript `quote()` function from `packages/shared`. It takes a fully-loaded `Package` entity (with rates, seasons, add-ons) and a `PricingRequest` and returns a `QuoteResult` with a line-item breakdown. It has no database access and is unit-testable in isolation.

Errors throw `QuoteException` (not `AppException`), which `ExceptionHandlingMiddleware` maps to `422 Unprocessable Entity` with a `code` field (`CheckInInPast`, `BelowMinNights`, `NoRateForDate`, etc.).

## Migration strategy

EF Core migrations are generated locally with `dotnet ef migrations add <Name>`. The SQL script is produced with `dotnet ef migrations script --idempotent` and committed to `docs/migrations/`. It is applied to Supabase using a one-shot node runner (the `pg` package, single connection) because the EF CLI loses its connection mid-migration against the shared pooler (PgBouncer transaction mode drops idle connections between statements).

For migrations, the `ApplicationDbContextFactory` prefers `DirectConnection` (Supabase direct on port 5432) over `DefaultConnection` (pooler on port 6543). If the direct host isn't reachable from a given network, pass `--connection` to `dotnet ef` or use the node runner with the pooler string.
