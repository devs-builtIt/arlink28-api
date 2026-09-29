# arlink28-api — Docs

Living documentation for the C# REST API backend. The frontend lives in `arlink28-nextjs`.

## Contents

| Document | Description |
|----------|-------------|
| [architecture.md](architecture.md) | Stack, feature folder layout, entity model, key patterns |
| [auth-plan.md](auth-plan.md) | Phase 2 auth design — entities, endpoints, flows, security requirements |
| [instructions.md](instructions.md) | Local dev setup, running the API, running migrations |
| [security.md](security.md) | Open findings, non-findings, what to audit per phase |
| [memory.md](memory.md) | Running project log — decisions, gotchas, current state |
| [adr/](adr/) | Architecture Decision Records |
| [migrations/](migrations/) | Idempotent SQL scripts, one per EF Core migration |

## ADR index

| ADR | Decision | Status |
|-----|----------|--------|
| [0001](adr/0001-csharp-postgres-stack.md) | .NET 10 / ASP.NET Core + PostgreSQL stack | Accepted |
| [0002](adr/0002-feature-folders-architecture.md) | Feature-folders vertical-slice architecture | Accepted |
| [0003](adr/0003-auth-approach.md) | Custom Staff table, no ASP.NET Core Identity | Accepted |

## Key conventions

| Convention | Rule |
|------------|------|
| **IDs** | `Guid` in C# → `uuid` in PostgreSQL; generated app-side with `Guid.NewGuid()` |
| **Money** | `long` minor units + `string` ISO-4217 code. USD 8,488 = `848800, "USD"`. Never floats. |
| **Pricing** | Stateless `PricingEngine.Quote(package, request)` in `Helpers/PricingEngine.cs`. No DB access. |
| **Cursor pagination** | Keyset on `(Featured DESC, SortOrder ASC, Id ASC)`. Base64-encoded opaque string. |
| **DI lifetime** | Implement `ITransient`, `IScoped`, or `ISingleton`; Scrutor auto-registers at startup. |
| **Error shape** | `{ success, message, data }` for all responses; 422 + `code` field for quote errors. |
| **Audit trail** | Every admin write appended to `audit_logs` table (actor, action, entity, before/after JSON). |
| **Migrations** | Generate SQL with `dotnet ef migrations script --idempotent`; apply via node runner against Supabase pooler. |
