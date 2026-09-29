# ADR 0003 — Authentication Approach: Custom Staff Table, No ASP.NET Core Identity

**Date:** 2026-09-29
**Status:** Accepted

## Context

Phase 2 requires username + password login, invite-only registration, password reset by email, and two roles (SuperAdmin, Operator). We need to decide how to manage user identity and credentials.

The main options are:

1. **ASP.NET Core Identity** — full framework with `AspNetUsers`, `AspNetRoles`, `AspNetUserClaims`, `AspNetUserTokens`, `AspNetRoleClaims` tables, `UserManager<T>`, `SignInManager<T>`, `RoleManager<T>`.
2. **Custom `Staff` table** — one table, `PasswordHasher<Staff>` for hashing (same PBKDF2 algorithm Identity uses internally), custom JWT issuance via the existing `JwtBearerOptionsSetup`.

## Decision

Use a custom `Staff` table (option 2).

## Rationale

- **Two roles, invite-only.** ASP.NET Core Identity is designed for self-service registration, role claims, external OAuth providers, and two-factor auth. None of those are in scope. The overhead — 5+ generated tables, a thick API surface, EF Core conventions that conflict with our existing schema style — adds complexity without value.
- **`PasswordHasher<Staff>` gives the same cryptographic guarantees.** It is the same PBKDF2-HMAC-SHA256 implementation Identity uses internally, available as a standalone class with no additional packages.
- **Full control over lockout and audit.** `FailedLoginAttempts` and `LockedUntil` columns on `Staff` are simpler to reason about and audit than Identity's distributed lockout store.
- **Schema consistency.** All other tables in this project use `Guid` IDs with `gen_random_uuid()` defaults and `timestamptz` timestamps. Identity's generated schema uses `nvarchar(450)` string PKs and different conventions.

## Consequences

- No `UserManager`, `SignInManager`, or `RoleManager` anywhere in the codebase — only `PasswordHasher<Staff>` for verify/hash.
- JWT issuance handled by `JwtService` in `Helpers/`. Token validation already wired via `JwtBearerOptionsSetup`.
- If MFA or external OAuth is ever needed (not currently planned), Identity can be introduced then. The `Staff` table can be migrated to an Identity-compatible schema at that point.
- Password reset and invite tokens managed in a single `StaffToken` table (type discriminator: `Invite` / `PasswordReset`).
