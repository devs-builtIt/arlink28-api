# Instructions — arlink28-api

## Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 10.0 (`dotnet --version`) |
| Node.js | 22+ (for migration runner only) |
| A Supabase project | — |

## Secrets and connection strings

Local secrets live in **dotnet user-secrets** (`UserSecretsId` `arlink28-api-secrets` in the csproj). They override the placeholders in `appsettings.json`, which points at a local Postgres (`localhost:5432/arlink28_dev`) and must never hold real values. Set them with:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<connection string>" --project Arlink28.Api.csproj
dotnet user-secrets set "ConnectionStrings:DirectConnection"  "<connection string>" --project Arlink28.Api.csproj
dotnet user-secrets set "AppSettings:Secret"                  "<JWT signing key>"   --project Arlink28.Api.csproj
dotnet user-secrets list --project Arlink28.Api.csproj   # check what's set
```

The dev database is Supabase. Both connection strings currently point at its pooler:

| Key | Host | Port | Use |
|-----|------|------|-----|
| `DefaultConnection` | `aws-1-eu-west-1.pooler.supabase.com` | 5432 | Runtime (EF Core queries) |
| `DirectConnection` | `aws-1-eu-west-1.pooler.supabase.com` | 5432 | EF Core migrations |

`ApplicationDbContextFactory` (used by `dotnet ef`) prefers `DirectConnection` and falls back to `DefaultConnection`. If you switch `DirectConnection` to Supabase's direct host (`db.<ref>.supabase.co`) and it's unreachable on your network, use the node runner (see below). Production moves to Postgres on the VPS (web repo ADR 0004), with secrets in environment variables.

## Run locally

```bash
cd arlink28-api
dotnet run --project Arlink28.Api.csproj            # http profile
dotnet run --project Arlink28.Api.csproj -lp https  # https profile
```

Ports come from `Properties/launchSettings.json`:

- API: http://localhost:5270 (https profile: https://localhost:7212)
- Swagger UI: http://localhost:5270/swagger (spec: `/swagger/v1/swagger.json`)
- Health check: http://localhost:5270/health

The web app (arlink28-nextjs) expects the http profile: its `apps/web/.env.local` has `API_URL=http://localhost:5270`. After changing endpoints, refresh its typed client with `pnpm --filter @arlink28/api-client sync` while the API is running.

## Responses and errors

- **Success:** the body is the resource itself (no wrapper). Endpoints with nothing to return answer `204 No Content`.
- **Errors:** every error is RFC 9457 Problem Details (`application/problem+json`) with a stable `code` and a `traceId`. Clients switch on `code`, never on `detail`.
  - Controllers return errors with `this.ApiProblem(status, detail, code?)`.
  - `ExceptionHandlingMiddleware` maps `AppException` → 400 and `QuoteException` → 422 (`NO_RATE_FOR_DATE`, …).
  - Codes live in `Helpers/Problems.cs`.
- **Declare every response** with `[ProducesResponseType]`, so the OpenAPI doc (and the web app's generated types) stay exact.

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
  connectionString: 'postgresql://postgres.<ref>:<password>@aws-1-eu-west-1.pooler.supabase.com:5432/postgres',
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
Program.cs                  Startup: EF Core, Scrutor, JWT, Problem Details, Swagger, health, CORS
appsettings.json            Placeholder values — never real secrets (those are in user-secrets)
Properties/launchSettings.json  Local ports: http 5270, https 7212

Data/
  Entities/Common/          BaseEntity (Guid) · BaseAuditableEntity (+ CreatedAt, UpdatedAt)
  Entities/                 16 entity classes + Enums.cs
  Configuration/            16 IEntityTypeConfiguration<T> files
  ApplicationDbContext.cs
  ApplicationDbContextFactory.cs
  DataContextInitializer.cs Seeds the bootstrap SuperAdmin at startup

Features/
  Shared/                   ITransient · IScoped · ISingleton; JwtService, EmailService
  Catalogue/                Public catalogue: packages, quote, destinations, partners
  Auth/                     Login, /me, logout, change and reset password
  UserManagement/           Staff list, invite, accept invite, role, deactivate (SuperAdmin)

Helpers/
  Problems.cs · Result.cs · AppException.cs · Money.cs · PricingEngine.cs · PasswordRules.cs
  Settings/                 AppSettings · EmailSettings · AdminBootstrapSettings
  OptionsSetup/             JwtBearerOptionsSetup · ConfigureSwaggerOptions · ConfigureCorsOptions

Middleware/
  ExceptionHandlingMiddleware.cs

Migrations/                 EF Core C# migration classes (created by the first `dotnet ef migrations add`; none yet)
docs/migrations/            Idempotent SQL scripts (committed, applied manually; none yet)
```
