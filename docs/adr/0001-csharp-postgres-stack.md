# ADR 0001 — C# / ASP.NET Core + PostgreSQL Stack

**Date:** 2026-09-28
**Status:** Accepted
**Supersedes:** arlink28-nextjs ADR 0005 (same decision, recorded here for the new repo)

## Context

The original API was scaffolded as NestJS + MySQL inside a pnpm monorepo (arlink28-nextjs). ADR 0004 and 0005 in that repo pivoted to a VPS / Docker deployment and a C# / PostgreSQL backend respectively.

This repo (`arlink28-api`) is the C# implementation of that decision.

## Decision

- **Runtime:** .NET 10 / ASP.NET Core
- **ORM:** Entity Framework Core 10 + Npgsql
- **Database:** PostgreSQL 16+
- **Auth:** JWT Bearer (Microsoft.AspNetCore.Authentication.JwtBearer)
- **API docs:** Swashbuckle / Swagger UI at `/swagger`
- **Versioning:** URL-segment (`/api/v1/...`) via Asp.Versioning

## Consequences

- OpenAPI spec is generated at build time; the Next.js front-end will consume a TypeScript client generated from it.
- MySQL-specific Prisma migrations are discarded; EF Core migrations targeting PostgreSQL replace them.
- The `packages/shared` pricing logic (TypeScript) is ported to `Helpers/PricingEngine.cs`.
