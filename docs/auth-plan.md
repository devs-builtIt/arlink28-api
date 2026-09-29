# Auth Feature Plan (Phase 2)

**Roles:** SuperAdmin (full access), Operator (create / read / edit packages).
**Registration:** invite-only — no public sign-up.
**Credentials:** username + password (email used only for invite delivery and password reset).

See also: [ADR 0003](adr/0003-auth-approach.md) — why no ASP.NET Core Identity.

---

## New tables

### `Staff`

| Column | Type | Notes |
|--------|------|-------|
| `Id` | `uuid` PK | `gen_random_uuid()` |
| `Username` | `varchar(50)` | Unique; login credential |
| `Email` | `varchar(200)` | Unique; invite + reset delivery |
| `PasswordHash` | `text` | PBKDF2 via `PasswordHasher<Staff>` |
| `Role` | `varchar(20)` | `SuperAdmin` \| `Operator` |
| `IsActive` | `bool` | `true` on accept-invite |
| `FailedLoginAttempts` | `int` | Reset to 0 on success |
| `LockedUntil` | `timestamptz?` | Set to `now + 15 min` after 5 failures |
| `LastLoginAt` | `timestamptz?` | Updated on every successful login |
| `InvitedById` | `uuid?` FK→Staff | Nullable for seed SuperAdmin |
| `CreatedAt` | `timestamptz` | |
| `UpdatedAt` | `timestamptz` | |

### `StaffToken`

| Column | Type | Notes |
|--------|------|-------|
| `Id` | `uuid` PK | |
| `Type` | `varchar(20)` | `Invite` \| `PasswordReset` |
| `TokenHash` | `varchar(64)` | SHA-256 hex — never store plain |
| `Email` | `varchar(200)` | Invite: recipient; Reset: account email |
| `RoleToAssign` | `varchar(20)?` | Invite only |
| `StaffId` | `uuid?` FK→Staff | Null for Invite (staff not yet created) |
| `ExpiresAt` | `timestamptz` | Invite: +48 h; Reset: +1 h |
| `UsedAt` | `timestamptz?` | Set on use; prevents reuse |
| `CreatedAt` | `timestamptz` | |

---

## API endpoints

### Auth (`Features/Auth/`)

```
POST   /api/v1/auth/login
GET    /api/v1/auth/me                          [Authorize]  current staff; 401 if deactivated
POST   /api/v1/auth/logout                      [Authorize]
PATCH  /api/v1/auth/change-password             [Authorize]
POST   /api/v1/auth/reset-password/request
POST   /api/v1/auth/reset-password/confirm
```

### User management (`Features/UserManagement/`)

```
GET    /api/v1/users                            [Authorize(Roles = "SuperAdmin")]
POST   /api/v1/users/invite                     [Authorize(Roles = "SuperAdmin")]
POST   /api/v1/users/invite/accept
PATCH  /api/v1/users/:id/role                   [Authorize(Roles = "SuperAdmin")]
PATCH  /api/v1/users/:id/deactivate             [Authorize(Roles = "SuperAdmin")]
```

**Login response shape:**
```json
{ "accessToken": "eyJ...", "role": "Operator", "username": "jane", "expiresAt": "2026-09-30T07:00:00Z" }
```

**Token lifetime:** 8 hours. No refresh tokens in Phase 2.

---

## Flows

### Login
1. Look up `Staff` by username. Return 401 (generic) if not found.
2. Check `IsActive` — 401 if false.
3. Check `LockedUntil` — 401 with remaining seconds if locked.
4. `PasswordHasher.VerifyHashedPassword`. On failure: increment `FailedLoginAttempts`; lock on ≥ 5.
5. On success: reset counter, update `LastLoginAt`, issue JWT, write `audit_logs`.

### Invite user (SuperAdmin)
1. Validate email not already a staff account or pending invite.
2. `RandomNumberGenerator.GetBytes(32)` → Base64Url (plain token). Store `SHA-256` hex.
3. Send invite email with link containing plain token. Token expires in 48 h.
4. Write `audit_logs`.

### Accept invite
1. Hash token with SHA-256. Find valid, unused, unexpired `Invite` token.
2. Validate username (unique, 3–50 chars) and password complexity.
3. Create `Staff`, mark token used — in one transaction.
4. Issue JWT. User is logged in immediately on acceptance.

### Password reset
1. **Request:** always return 204 — never confirm email existence. If found and active: generate token (1 h), send email, invalidate prior pending tokens.
2. **Confirm:** find valid token, validate new password, update hash, mark token used. Write `audit_logs`.

---

## Security requirements (ISO 27001:2022)

| Requirement | Implementation | Control |
|-------------|---------------|---------|
| Password hashing | `PasswordHasher<Staff>` — PBKDF2-HMAC-SHA256, 100k iterations (V3) | A.5.17, A.8.24 |
| Password complexity | Min 8 chars, 1 uppercase, 1 lowercase, 1 digit — FluentValidation | A.5.17 |
| Account lockout | 5 failures → 15 min lockout | A.8.5 |
| Token entropy | 32-byte CSPRNG (`RandomNumberGenerator`) | A.8.5 |
| Token storage | SHA-256 hex in DB; plain token in email only | A.5.17 |
| Email enumeration prevention | Generic responses on login fail and reset-request | A.8.5 |
| HTTPS in production | `RequireHttpsMetadata = !isDevelopment` | A.8.5, A.8.24 |
| Audit logging | `audit_logs` row for every auth event | A.8.15 |
| Privilege separation | `[Authorize(Roles = "SuperAdmin")]` on user-management | A.5.15, A.8.2 |

---

## New packages

| Package | Use |
|---------|-----|
| `MailKit` + `MimeKit` | Send invite and password-reset emails |

`PasswordHasher<Staff>` is already available via `Microsoft.AspNetCore.Identity` (already in the csproj). No Identity schema or services needed.

---

## Feature folder structure

```
Features/
  Auth/
    Controllers/AuthController.cs
    Services/Interfaces/IAuthService.cs
    Services/AuthService.cs
    RequestModels/LoginRequest.cs
    RequestModels/ChangePasswordRequest.cs
    RequestModels/ResetPasswordRequest.cs
    RequestModels/ConfirmResetPasswordRequest.cs
    ResponseModels/AuthResponse.cs

  UserManagement/
    Controllers/UserController.cs
    Services/Interfaces/IUserService.cs
    Services/UserService.cs
    RequestModels/InviteUserRequest.cs
    RequestModels/AcceptInviteRequest.cs
    RequestModels/AssignRoleRequest.cs
    ResponseModels/UserResponse.cs

Helpers/
  JwtService.cs           IssueToken(Staff) → string
  EmailService.cs         SendInviteAsync / SendPasswordResetAsync (MailKit)
  Settings/EmailSettings.cs
```

---

## Seed SuperAdmin

On first deploy, `DataContextInitializer.SeedSuperAdminAsync()` runs if `AdminBootstrap:Enabled = true` in config. It creates one SuperAdmin account with the username and password from env vars. The method is a no-op if any `Staff` row already exists, so it is safe to leave enabled.

---

## Out of scope (Phase 2)

- Refresh tokens
- MFA / TOTP
- OAuth / OIDC (Google, Microsoft)
- Customer-facing auth (Phase 5 — Supabase Auth)
- Session revocation list
- API keys
