# Security — arlink28-api

**Standard:** ISO/IEC 27001:2022
**Scope:** arlink28-api REST backend — source code, configuration, build pipeline, and runtime on VPS.
**First review:** 2026-09-29 (Phase 1 — read-only public catalogue; no auth, no admin writes, no payments).
**Next scheduled review:** before Phase 2 (auth) is deployed to production.

---

## 1. Information Classification (A.5.12)

| Class | Examples | Storage / Transit |
|-------|----------|-------------------|
| **Public** | Package names, destinations, published prices, partner names | API responses, cached by CDN |
| **Internal** | Draft packages, unpublished rates, admin configuration | Database only; not exposed via Phase 1 routes |
| **Confidential** | Customer PII, booking details, staff credentials | Phase 2/4/5 — not yet stored |
| **Restricted** | Database password, JWT signing secret, payment keys | Environment variables only — never in code |

All data currently served by Phase 1 routes is **Public** class. No Confidential or Restricted data is returned by any endpoint.

---

## 2. Asset Inventory (A.5.9)

| Asset | Owner | Class | Location |
|-------|-------|-------|----------|
| Source code | Development team | Internal | GitHub (`Dametiqer/arlink28-api`) |
| Supabase PostgreSQL database | Development team | Internal / Confidential (future) | Supabase eu-west-1 |
| JWT signing secret | Development team | Restricted | dotnet user-secrets (dev) · Docker env (production) |
| Supabase connection string + password | Development team | Restricted | dotnet user-secrets (dev) · Docker env (production) |
| API container image | Development team | Internal | VPS Docker registry |
| Swagger OpenAPI spec | Development team | Internal | `/swagger` — runtime only, disabled in production |
| Serilog log files | Development team | Internal | VPS `/logs/` |
| Audit log table | Development team | Internal / Confidential (future) | Supabase PostgreSQL `audit_logs` |

---

## 3. Risk Register (ISO 27001:2022 Clause 6.1.2)

Risk score = Likelihood (1–3) × Impact (1–4). **Low** ≤ 3 · **Medium** 4–6 · **High** 7–9 · **Critical** 10–12.

| ID | Asset / Threat | Vulnerability | L | I | Score | ISO Control | Treatment | Status |
|----|----------------|--------------|---|---|-------|-------------|-----------|--------|
| R-01 | JWT secret · disclosure via committed config | Dev secret was in `appsettings.Development.json` (git-ignored; never committed, checked 2026-09-29) | 2 | 4 | **8 High** | A.8.24, A.5.17 | Dev: moved to dotnet user-secrets (done). Prod: Docker env var at VPS deploy | **Mitigated (dev)** |
| R-02 | DB password · disclosure via committed config | Supabase password was in `appsettings.Development.json` (git-ignored; never committed, checked 2026-09-29) | 2 | 4 | **8 High** | A.8.24, A.5.17 | Dev: moved to dotnet user-secrets (done). Prod: Docker env var at VPS deploy | **Mitigated (dev)** |
| R-03 | API · DoS / resource exhaustion | No rate limiting on any route | 2 | 2 | **4 Medium** | A.8.26, A.8.6 | Add global fixed-window limiter before Phase 2 | **Open** |
| R-04 | Swagger spec · information disclosure | `/swagger` exposed with no auth in non-production envs | 2 | 1 | **2 Low** | A.8.3, A.8.26 | Disabled in production (`ASPNETCORE_ENVIRONMENT=Production`) | **Accepted — Low** |
| R-05 | CORS · cross-origin data access | Dev origins (`localhost`) not overridden to prod domains | 1 | 3 | **3 Low** | A.8.26, A.8.20 | Set production domain env vars before VPS deploy | **Open** |
| R-06 | Staff credentials · brute force (Phase 2) | No login endpoint exists yet | 1 | 3 | **3 Low** | A.8.5, A.5.17 | Implement account lockout + PBKDF2 in Phase 2 | **Future** |
| R-07 | Admin data · unauthorised write (Phase 3) | No write endpoints exist yet | 1 | 4 | **4 Medium** | A.5.15, A.8.2 | Enforce `[Authorize(Roles)]` on every write; audit log all mutations | **Future** |
| R-08 | Customer PII · unauthorised access (Phase 4/5) | Not collected yet | 1 | 4 | **4 Medium** | A.5.12, A.8.11 | Apply RLS on customer tables; mask PII in logs | **Future** |
| R-09 | Payment data · interception or leakage (Phase 4) | Not handled yet | 1 | 4 | **4 Medium** | A.8.24, A.5.19 | TLS only; Paystack/Stripe tokenisation; webhook signature verification | **Future** |
| R-10 | Dependencies · known vulnerabilities | NuGet packages may lag behind patched versions | 2 | 2 | **4 Medium** | A.8.8 | Run `dotnet list package --vulnerable` on every PR; update within 30 days | **Open** |
| R-11 | Source code · supply-chain compromise | No dependency pinning policy | 1 | 3 | **3 Low** | A.5.21, A.8.8 | Pin NuGet package versions; lock `Arlink28.Api.csproj`; review changelogs on update | **Open** |
| R-12 | Logs · sensitive data exposure | Log verbosity misconfigured could expose request bodies | 1 | 2 | **2 Low** | A.8.15, A.8.12 | Serilog minimum level `Warning` in production; never log auth headers or request bodies | **Implemented** |
| R-13 | Database · connection pool exhaustion (Supabase pooler) | Shared pooler has a 60-connection cap per project | 1 | 2 | **2 Low** | A.8.6 | Configure Npgsql max pool size; monitor Supabase dashboard | **Open** |

---

## 4. Annex A Controls Assessment

### A.5 — Organisational Controls

| Control | Title | Status | Notes |
|---------|-------|--------|-------|
| A.5.1 | Policies for information security | **Partial** | This document is the policy. Formal ISMS policy document to be approved by owner before Phase 2 deploy. |
| A.5.9 | Inventory of information assets | **Implemented** | See Asset Inventory (§2). |
| A.5.12 | Classification of information | **Implemented** | See Information Classification (§1). |
| A.5.14 | Information transfer | **Partial** | All API traffic over HTTPS (enforced by Caddy). Supabase connection uses TLS. Internal VPS Docker network is unencrypted (accepted — private network). |
| A.5.15 | Access control | **Partial** | Phase 1 routes are intentionally public. Role-based access (Admin, Staff, Customer) designed but not yet implemented (Phase 2). |
| A.5.16 | Identity management | **Not started** | No user identities managed yet. Phase 2 scope. |
| A.5.17 | Authentication information | **Partial** | JWT secret and DB password are in dotnet user-secrets for dev (R-01, R-02); production env vars pending VPS deploy. |
| A.5.18 | Access rights | **Not started** | No access rights to provision yet. Phase 2/3 scope. |
| A.5.19 | Information security in supplier relationships | **Partial** | Supabase is the primary supplier. See §5 (Supabase). No formal SLA reviewed yet. |
| A.5.23 | Information security for use of cloud services | **Partial** | Supabase PostgreSQL in eu-west-1. Data residency confirmed. Supabase SOC 2 Type II available. |
| A.5.24 | Incident management planning | **Not started** | No formal incident response procedure. Define before Phase 2. |
| A.5.26 | Response to information security incidents | **Not started** | No incident response runbook. See §6 (Incident Response). |
| A.5.37 | Documented operating procedures | **Partial** | `docs/instructions.md` covers dev setup and migration procedure. Production runbook needed before VPS deploy. |

### A.6 — People Controls

| Control | Title | Status | Notes |
|---------|-------|--------|-------|
| A.6.1 | Screening | **Not started** | Background check policy needed for any staff with production access. |
| A.6.2 | Terms and conditions of employment | **Not started** | Confidentiality obligations for contributors handling Restricted data. |
| A.6.8 | Information security event reporting | **Not started** | Define a channel (e.g. email to owner) for reporting suspected incidents before Phase 2. |

### A.7 — Physical Controls

| Control | Title | Status | Notes |
|---------|-------|--------|-------|
| A.7.1–A.7.14 | Physical and environmental security | **Delegated** | Fully delegated to VPS provider and Supabase data centre. Review provider's physical security attestation annually. |

### A.8 — Technological Controls

| Control | Title | Status | Notes |
|---------|-------|--------|-------|
| A.8.2 | Privileged access rights | **Not started** | No admin roles provisioned yet. Phase 3: `[Authorize(Roles = "Admin")]` on every write endpoint. |
| A.8.3 | Information access restriction | **Implemented (Phase 1)** | `Status == Published` filter in `CatalogueService`; draft/archived packages never returned. |
| A.8.4 | Access to source code | **Partial** | GitHub repo is private. Branch protection on `main` recommended (no direct push). |
| A.8.5 | Secure authentication | **Partial** | JWT Bearer token validation implemented (`JwtBearerOptionsSetup`). No token issuance yet (Phase 2). `RequireHttpsMetadata = false` in dev — must be `true` in production. |
| A.8.6 | Capacity management | **Open** | Supabase shared pooler limited to 60 connections. Monitor and set `Max Pool Size` in Npgsql. |
| A.8.8 | Management of technical vulnerabilities | **Open** | No formal dependency scanning. Run `dotnet list package --vulnerable` on every PR. AutoMapper removed due to known CVE (GHSA-rvv3-g6hj-g44x). |
| A.8.9 | Configuration management | **Partial** | Dev secrets in dotnet user-secrets; `appsettings.json` holds placeholders only (R-01, R-02). Production: Docker env vars at VPS deploy. |
| A.8.11 | Data masking | **Not started** | No PII collected yet. Mask in logs and error responses when Phase 4/5 lands. |
| A.8.12 | Data leakage prevention | **Implemented** | `ExceptionHandlingMiddleware` strips stack traces from all error responses. Serilog set to `Warning` minimum; no request bodies logged. |
| A.8.15 | Logging | **Partial** | Serilog structured JSON to rolling file and console. `audit_logs` table records all admin writes (future). Log retention policy not yet defined. |
| A.8.16 | Monitoring activities | **Not started** | No alerting or anomaly detection. Define before VPS deploy: at minimum, alert on 5xx rate and DB connection failures. |
| A.8.20 | Networks security | **Partial** | Caddy terminates TLS. API-to-Supabase over TLS (pooler). Internal Docker network between containers (not encrypted — accepted, private). |
| A.8.24 | Use of cryptography | **Open** | JWT uses HMAC-SHA256 (HS256) with a shared symmetric secret. **Recommendation:** migrate to RS256 (asymmetric) before Phase 2 so the public key can be shared with the frontend without exposing the signing key. HTTPS enforced at Caddy layer. |
| A.8.25 | Secure development life cycle | **Partial** | Feature branches and PRs in use. No formal security review gate on PRs yet. Add a security checklist item to the PR template. |
| A.8.26 | Application security requirements | **Partial** | CORS locked to known origins (`ConfigureCorsOptions`). Rate limiting not yet implemented (R-03). No CAPTCHA on public endpoints. |
| A.8.27 | Secure system architecture | **Implemented** | Defence in depth: `ExceptionHandlingMiddleware` → JWT validation → controller → service → EF Core parameterized queries. No direct database access from controllers. |
| A.8.28 | Secure coding | **Implemented** | Nullable reference types enabled (`<Nullable>enable</Nullable>`). No raw SQL. EF Core parameterized queries prevent injection. `AppException` / `QuoteException` used for expected failures — no unhandled exceptions reach the client. |
| A.8.29 | Security testing in development and acceptance | **Not started** | No penetration test or DAST scan. Required before Phase 4 (Bookings / Payments). OWASP ZAP or Burp Suite scan recommended before production launch. |
| A.8.31 | Separation of development, test and production environments | **Partial** | Dev uses local / Supabase dev project. No dedicated staging environment yet. Define staging before Phase 3. |
| A.8.32 | Change management | **Partial** | Migrations versioned in `docs/migrations/`. No formal change advisory board (not required at this team size). Document breaking API changes in `memory.md`. |

---

## 5. Third-party / Cloud Services (A.5.19, A.5.23)

### Supabase (PostgreSQL + future Auth + Storage)

| Item | Status |
|------|--------|
| Data residency | eu-west-1 (AWS Ireland) — confirmed |
| Encryption at rest | Enabled (Supabase default AES-256) |
| Encryption in transit | TLS 1.3 on pooler and direct connections |
| SOC 2 Type II | Available on request from Supabase |
| GDPR | Supabase DPA available — obtain before storing any customer PII |
| Shared pooler trust | Supabase pooler on port 5432 (session mode) for both runtime and migrations. Dev only; production moves to Postgres on the VPS |
| Formal SLA review | **Not yet done** — complete before VPS deploy |

### GitHub (source code hosting, A.5.19)

| Item | Status |
|------|--------|
| Repo visibility | Private |
| Branch protection on `main` | **Not configured** — enable before Phase 2 |
| Secrets scanning | Enable GitHub secret scanning to alert on committed credentials |
| Access review | Review contributor access list quarterly |

---

## 6. Incident Response (A.5.24, A.5.26)

A formal incident response procedure must be written before Phase 2. Until then, the following minimal procedure applies:

1. **Identify** — any suspected breach, credential exposure, or anomalous traffic is an incident.
2. **Contain** — rotate the affected secret immediately (`AppSettings__Secret`, Supabase password). If database compromise is suspected, revoke Supabase API keys from the dashboard.
3. **Eradicate** — remove the committed secret from git history (`git filter-repo`); force-rotate all derived tokens.
4. **Notify** — inform the project owner within 24 hours. If customer PII was involved (Phase 4+), GDPR Article 33 requires notifying the supervisory authority within 72 hours.
5. **Record** — add an entry to `memory.md` with the date, nature, and resolution of the incident.

---

## 7. Accepted Risks

| ID | Risk | Rationale |
|----|------|-----------|
| R-04 | Swagger exposed in non-production | Only enabled when `ASPNETCORE_ENVIRONMENT != Production`; no Restricted data in the spec |
| Internal Docker network unencrypted | TLS between containers on a private Docker network is standard practice; all external traffic is TLS via Caddy |

---

## 8. Open Findings Summary

| ID | Finding | Control | Priority |
|----|---------|---------|----------|
| R-01 | JWT secret in config | A.8.24, A.5.17 | Dev done (user-secrets); **set as env var at VPS deploy** |
| R-02 | DB password in config | A.8.24, A.5.17 | Dev done (user-secrets); **set as env var at VPS deploy** |
| R-03 | No rate limiting | A.8.26, A.8.6 | Medium — fix before Phase 2 |
| R-05 | CORS origins not set to production domain | A.8.26, A.8.20 | Medium — fix before VPS deploy |
| R-10 | No dependency vulnerability scanning | A.8.8 | Medium — add to PR checklist now |
| R-11 | No supply-chain pinning policy | A.5.21, A.8.8 | Low |
| R-13 | Supabase pool size not configured | A.8.6 | Low |
| A.8.24 | JWT uses HS256 (symmetric) | A.8.24 | Medium — migrate to RS256 before Phase 2 |
| A.8.5 | `RequireHttpsMetadata = false` in dev | A.8.5 | **High — must be `true` in production** |
| A.8.29 | No penetration test or DAST | A.8.29 | High — required before Phase 4 (Payments) |

---

## 9. Review Schedule

| Trigger | Action |
|---------|--------|
| Before VPS deploy | Production secrets as env vars (R-01, R-02); resolve R-05; set `RequireHttpsMetadata = true`; write production runbook |
| Before Phase 2 (Auth) | Full re-review of A.5.15–A.5.18, A.8.5; write incident response procedure; migrate JWT to RS256 |
| Before Phase 3 (Admin CRUD) | Audit `[Authorize]` coverage; verify audit log completeness; set up staging environment |
| Before Phase 4 (Payments) | Penetration test; PCI scope assessment; webhook signature verification; GDPR DPA with Supabase |
| Quarterly (ongoing) | `dotnet list package --vulnerable`; review GitHub contributor access; check Supabase changelog for breaking security changes |
