# Instructions — arlink28-api

## Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 10.0 (`dotnet --version`) |
| Node.js | 22+ (for migration runner only) |
| A Supabase project | — |

## Connection strings

`appsettings.Development.json` has two connection strings:

| Key | Host | Port | Use |
|-----|------|------|-----|
| `DefaultConnection` | pooler (`aws-1-eu-west-1.pooler.supabase.com`) | 6543 | Runtime (EF Core queries) |
| `DirectConnection` | direct (`db.<ref>.supabase.co`) | 5432 | EF Core migrations |

The pooler uses PgBouncer transaction mode. The `ApplicationDbContextFactory` (used by `dotnet ef`) prefers `DirectConnection` for migrations. If the direct host is unreachable on your network, use the node runner (see below).

Secrets stay in `appsettings.Development.json` (dev only) or dotnet user-secrets for production. Never commit passwords to `appsettings.json`.

## Run locally

```bash
cd arlink28-api
dotnet run --project Arlink28.Api.csproj
```

- API: http://localhost:5001
- Swagger UI: http://localhost:5001/swagger
- Health check: http://localhost:5001/health

## Running migrations

### Step 1 — generate the migration class

```bash
dotnet ef migrations add <Name> --project Arlink28.Api.csproj
```

### Step 2 — generate the idempotent SQL script

```bash
dotnet ef migrations script --idempotent --project Arlink28.Api.csproj \
  -o docs/migrations/<NNN>_<name>.sql
```

Commit this file. The `--idempotent` flag wraps every statement in `IF NOT EXISTS` checks, so the script is safe to re-run.

### Step 3 — apply to Supabase

**Option A — Supabase SQL editor (recommended for most networks)**

1. Open `docs/migrations/<NNN>_<name>.sql` in VS Code
2. Copy all → paste into [Supabase SQL editor](https://supabase.com/dashboard/project/zpyacljkgrgadwgnsqyc/sql/new)
3. Run

**Option B — node runner (if you have Node 22+ locally)**

Create a temporary runner script:

```javascript
// run.mjs
import { Client } from 'pg';
import { readFileSync } from 'fs';

const sql = readFileSync('docs/migrations/<NNN>_<name>.sql', 'utf8')
  .replace(/^﻿/, ''); // strip UTF-8 BOM added by dotnet ef

const client = new Client({
  connectionString: 'postgresql://postgres.<ref>:<password>@aws-1-eu-west-1.pooler.supabase.com:6543/postgres',
  ssl: { rejectUnauthorized: false },
});

await client.connect();
await client.query(sql);
await client.end();
console.log('Done.');
```

```bash
npm install pg   # or pnpm add pg
node run.mjs
```

> The `%40` in the connection string encodes `@` in the password (URI encoding). In Npgsql key-value format the `@` is literal.

### Verify

```javascript
// verify.mjs — lists all public tables and the EF migration history
import { Client } from 'pg';
const client = new Client({ connectionString: '...', ssl: { rejectUnauthorized: false } });
await client.connect();
const { rows } = await client.query(
  `SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY 1`
);
console.log(rows.map(r => r.table_name));
const { rows: hist } = await client.query(`SELECT "MigrationId" FROM "__EFMigrationsHistory"`);
console.log(hist);
await client.end();
```

## Seeding the package catalogue

`Data/Seed/` holds the 13 packages transcribed from the partner posters (ported from the old TypeScript seed). Load them with:

```bash
dotnet run --project Arlink28.Api.csproj -- seed-catalogue                    # from-prices as of today
dotnet run --project Arlink28.Api.csproj -- seed-catalogue --today=2026-11-01 # as of another date
```

- It runs and exits; the server doesn't start. It refuses to run in Production without `--allow-production`.
- It's safe to re-run: rows are matched by slug, and each seeded package's stays, features, rates and add-ons are reset to the poster data. Packages not in the seed are untouched, and a hero image is only added where a package has none.
- It validates the data first (unknown references, stay nights, overlapping seasons) and writes nothing if a check fails.
- Three packages are seeded as `Draft` because their posters contradict each other; the command prints each reason. They stay off the public API until someone confirms the numbers.
- It's deliberately not run at startup: once packages are edited in the admin, a restart must not overwrite them.

## Media storage

Uploaded package photos are stored behind `IMediaStorage`; paths in the database are always `/media/<folder>/<name>`.

| `MediaStorage:Provider` | Where files go | Use |
|---|---|---|
| `LocalDisk` (default) | `MediaStorage:RootPath` (`local-storage/media`), served as static files | local development |
| `Supabase` | a **public** Supabase Storage bucket; `/media/*` answers 302 to the bucket | staging on Render (its disk is wiped on every deploy) |

Supabase settings (environment variable form for Render): `MediaStorage__Provider=Supabase`, `MediaStorage__SupabaseUrl=https://<project-ref>.supabase.co`, `MediaStorage__SupabaseBucket=<bucket>` (case-sensitive), `MediaStorage__SupabaseServiceKey=<service_role key>`. The key is a secret: environment or user-secrets only, never `appsettings.json`. Staging notes: `arlink28-nextjs/docs/staging.md`.

Moving existing local photos: upload `local-storage/media/**` to the bucket under the same relative paths and nothing in the database changes. Not covered by tests yet.

## Build and check

```bash
dotnet build Arlink28.Api.csproj
dotnet test                          # when tests are added
```

## Adding a new feature

1. Create `Features/<FeatureName>/` with `Controllers/`, `Services/`, `RequestModels/`, `ResponseModels/`
2. Make the service implement `IScoped` (or `ITransient` / `ISingleton`) — Scrutor auto-registers it
3. No changes needed to `Program.cs` unless you need a new middleware, hosted service, or settings section
4. Add EF entities to `Data/Entities/`, configurations to `Data/Configuration/`, and register `DbSet` in `ApplicationDbContext`
5. Run `dotnet ef migrations add <Name>` and follow the migration steps above

## Project structure

```
Arlink28.Api.csproj
Program.cs                  Startup: EF Core, Scrutor, JWT, Swagger, health, CORS
appsettings.json            Placeholder values — never real secrets
appsettings.Development.json  Dev secrets (Supabase connection, JWT key)

Data/
  Entities/Common/          BaseEntity (Guid) · BaseAuditableEntity (+ CreatedAt, UpdatedAt)
  Entities/                 15 entity classes
  Configuration/            14 IEntityTypeConfiguration<T> files
  ApplicationDbContext.cs
  ApplicationDbContextFactory.cs

Features/
  Shared/Interfaces/        ITransient · IScoped · ISingleton
  Catalogue/                Phase 1 — public catalogue routes
  Auth/                     Phase 2 scaffold
  Admin/                    Phase 3 (not yet built)

Helpers/
  Problems.cs · Result.cs · AppException.cs · Money.cs · PricingEngine.cs
  Settings/AppSettings.cs
  OptionsSetup/             JwtBearerOptionsSetup · ConfigureSwaggerOptions · ConfigureCorsOptions

Middleware/
  ExceptionHandlingMiddleware.cs

Migrations/                 EF Core C# migration classes (auto-generated)
docs/migrations/            Idempotent SQL scripts (committed, applied manually)
```
