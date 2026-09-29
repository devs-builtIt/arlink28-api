# ADR 0002 — Feature-Folders Vertical-Slice Architecture

**Date:** 2026-09-29
**Status:** Accepted

## Context

ASP.NET Core projects commonly organise code in horizontal layers (Controllers/, Services/, Models/). For a growing travel-API codebase with distinct domains (Catalogue, Auth, Admin, Bookings), we need clear ownership boundaries.

## Decision

Use **vertical slices** via `Grinderofl.FeatureFolders`:

```
Features/
  Catalogue/
    Controllers/     — HTTP surface (PackagesController, ReferenceController)
    Services/        — Business logic (CatalogueService)
    RequestModels/   — Input DTOs
    ResponseModels/  — Output DTOs
  Auth/              — Phase 2 (login, token refresh)
  Admin/             — Phase 3 (CRUD, media upload)
  Bookings/          — Phase 4 (seat hold, payment)
```

Cross-cutting concerns live in top-level folders:
- `Data/` — DbContext, entities, EF configurations
- `Helpers/` — ApiResponse, Result, Money, PricingEngine, settings
- `Middleware/` — ExceptionHandlingMiddleware

**DI registration** uses Scrutor assembly scanning via `ITransient`, `IScoped`, `ISingleton` marker interfaces — no manual registration per service.

## Consequences

- Adding a feature means adding a folder under `Features/` with no changes to shared infrastructure.
- Services remain cohesive; a developer working on Catalogue never needs to touch Auth files.
- The horizontal-slice anti-pattern (editing Controllers/, Services/, Models/ separately for each change) is avoided.
