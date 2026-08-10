# Identity, Groups, Invitations and Sharing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn a single-tenant plaintext ledger into a server that can host unrelated users and
families: real accounts with passkeys, server sessions, groups, invitations, and explicit resource
grants — without the server ever holding a key that opens financial content.

**Architecture:** ASP.NET Core Identity on .NET 10 supplies user storage, the passkey ceremonies and
the cookie session. Everything Xpense-specific (vault wrappers, encryption identity, groups,
memberships, invitations, grants) is ordinary EF entities in the same `XpenseDbContext`.
Authorization is an explicit query per slice, not an EF global filter, because access is no longer
`UserId == me`.

**Tech Stack:** .NET 10, EF Core 10, Npgsql, ASP.NET Core Identity 10 (passkeys), FluentValidation,
NUnit + FluentAssertions 6 + Testcontainers.

## Global constraints

- Source spec: `docs/superpowers/specs/2026-08-08-identity-groups-and-sharing-design.md`.
  Key-envelope vocabulary comes from `docs/superpowers/specs/2026-08-08-zero-knowledge-vault-and-recovery-design.md`.
- **No comments in any file.** C#, YAML, Dockerfiles, SQL. The only exception is `<summary>` on
  `*Request` and `*Response` records, which Swagger renders into the OpenAPI document ADR 0003 makes
  authoritative.
- **One endpoint per file**, holding its route, request, validator and handler. Slices never
  reference each other. Slices never catch domain exceptions.
- Explicit names: `cancellationToken`, `dbContext`, `httpContext`, `currentUser`. No `ct`, `db`, `req`.
- `const` and `static readonly` at the top of every type.
- Validation messages are prose sentences ending in a full stop.
- `TypedResults` only. Creates return an absolute `Location` via `HttpContext.ResourceUri(path)`.
- Deletes are soft. Timestamps are `DateTime.UtcNow`. Validation is FluentValidation only.
- **Migrations are a deployment step** (ADR 0004). Nothing in this plan calls `Database.Migrate()`
  from application code, and nothing seeds at startup (ADR 0005).
- **The Events table is the queue** (ADR 0008). The email adapter is a consumer of that table, not a
  second queue.
- **All schema work here is EXPAND-only.** Every migration adds tables and columns. No existing
  plaintext row is read, rewritten, or dropped.
- Do not add a `Co-Authored-By` trailer to any commit.

## Scope boundary: what this plan does NOT do

| Out of scope | Owned by |
|---|---|
| Dropping plaintext financial columns or tables | the zero-knowledge vault plan (contract migration, its release gate) |
| Adding `OwnerUserId` to `Accounts`, `Transactions`, `Budgets` and backfilling it | the zero-knowledge vault plan |
| The `/api/v1/sync/*` encrypted-record surface and its 409-stale-revision behaviour | the zero-knowledge vault plan |
| The one-time legacy claim flow and claim mode | the zero-knowledge vault plan |
| Browser WebAuthn/PRF code, crypto worker, IndexedDB vault | the web vault plan |
| Billing, plans, operator dashboards | not planned |

The legacy plaintext slices (`Accounts`, `Transactions`, `Budgets`, `Categories`, `Tags`,
`Merchants`, `Analytics`, `Notifications`, `Priorities`) are left alone by this plan except that
Task 10 makes them require a session. They keep serving the same global dataset to any authenticated
user until the vault plan claims and encrypts it. That gap is deliberate and is written down in
Task 19's ADR.

## No new projects

The work fits the five existing projects, so **no `.csproj` is added and no `docker/*/Dockerfile`
changes**. This matters: every Dockerfile restores the whole solution by copying each `.csproj`
explicitly, so a new project that is added to only two of the three breaks all three builds
(`AGENTS.md`). Placement:

| Work | Project | New package |
|---|---|---|
| Identity + group entities, enums, exceptions | `Xpense.Domain` | `Microsoft.Extensions.Identity.Stores` 10.0.10 |
| DbContext wiring, type configuration, migrations | `Xpense.Persistence` | `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.10 |
| Endpoints, auth configuration, authorization service | `Xpense.API` | none — `Microsoft.AspNetCore.Identity` is in the shared framework |
| Invitation email delivery | `Xpense.Notifications` | none — `System.Net.Mail.SmtpClient` is in the BCL |
| Tests | `Xpense.Tests` | none |

`SmtpClient` is the lazy choice with a known ceiling: it is fine for a local SMTP relay and cannot do
modern OAuth. If a hosted installation ever needs that, swap the one adapter class for MailKit.

## What already exists — verified, do not re-derive

- `XpenseDbContext` derives from `DbContext`, applies configurations from its own assembly, forces
  UTC on every `DateTime`, sets decimal precision, then applies a global soft-delete filter to every
  `BaseEntity`. It calls `base.OnModelCreating(modelBuilder)` **last**.
- `BaseEntity` has `int Id`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `MarkAsDeleted()`, `Touch()`.
- All tables live in the `Xpense` schema, set per configuration via `BaseEntityTypeConfiguration.XpenseSchema`.
- `IEndpoint` discovery is a startup scan over the API assembly (`EndpointExtensions.MapEndpoints`).
  There is no registration step. `SliceIsolationTests` flags any type under `Xpense.API.Features.*`
  that references a type in a *different* `Xpense.API.Features.*` folder. It says nothing about
  `Xpense.API.Infrastructure.*` or `Xpense.API.Contracts.*`.
- `ValidationEndpointFilter` runs any `IValidator<T>` registered for a handler argument; `.Validated()`
  attaches it.
- `ExceptionHandlers/` maps domain exceptions to RFC 7807. `NotFoundExceptionHandler` already maps
  `NotFoundException` to 404 through `ProblemDetailsWriter`.
- `IEventBus.Emit` inserts an `EventRecord` through the caller's `DbContext` and does **not** save.
- `EventProcessor.Process` **already skips an event type with no dispatcher** by calling
  `record.Succeeded()`. Adding a new event body cannot jam the pump.
- `EventProcessor` claims with `FOR UPDATE SKIP LOCKED` and marks `ProcessedAt` for every claimed row.
  There is exactly one consumer of the Events table. **Do not add a second pump** — it would race on
  `ProcessedAt`. New consumers are registered as `INotificationRule<TBody>`.
- `NotificationRuleIsolationTests` forbids a rule referencing another rule and forbids a timestamp in
  a rule payload.
- Tests are **NUnit**, not xUnit. `PostgresFixture` builds a template database by running the real
  migrations once, then clones it per test. `WebApiTestFactory` swaps the DbContext registration.
- `ApiEndpointTests.cs` is 1708 lines of anonymous HTTP calls. Task 10 is where that meets
  authentication.

### Verified against the installed framework (10.0.9 shared framework)

These types and methods exist; the executor still checks exact overloads at the call site.

- `SignInManager<TUser>`: `MakePasskeyCreationOptionsAsync`, `MakePasskeyRequestOptionsAsync`,
  `PerformPasskeyAttestationAsync`, `PerformPasskeyAssertionAsync`, `PasskeySignInAsync`,
  `RetrievePasskeyAuthenticationInfoAsync`, `StorePasskeyAuthenticationInfoAsync`.
- `UserManager<TUser>`: `AddOrUpdatePasskeyAsync`, `FindByPasskeyIdAsync`, `GetPasskeysAsync`.
- `IPasskeyHandler<TUser>` — a DI seam. This is the test escape hatch in Task 5.
- `IdentityPasskeyOptions` with `ServerDomain` and `ValidateOrigin`.
- `PublicKeyCredentialCreationOptions`, `PublicKeyCredentialRequestOptions`,
  `PasskeyAttestationResult`, `PasskeyAssertionResult<TUser>`, `UserPasskeyInfo`,
  `IdentityUserPasskey<TKey>`, `IUserPasskeyStore<TUser>`.

---

### Task 1: ASP.NET Core Identity inside `XpenseDbContext`

The riskiest schema task, because it changes the base class of the context every existing test runs
against.

**Files:**
- Modify: `api/src/Xpense/Xpense.Domain/Xpense.Domain.csproj`
- Modify: `api/src/Xpense/Xpense.Persistence/Xpense.Persistence.csproj`
- Create: `api/src/Xpense/Xpense.Domain/Entities/XpenseUser.cs`
- Create: `api/src/Xpense/Xpense.Domain/Enums/AccountState.cs`
- Modify: `api/src/Xpense/Xpense.Persistence/XpenseDbContext.cs`
- Create: `api/src/Xpense/Xpense.Persistence/TypeConfiguration/IdentityEntityTypeConfiguration.cs`
- Create: `api/src/Xpense/Xpense.Persistence/Migrations/<stamp>_AddIdentity.cs` (generated)

**Interfaces:**
- Produces: `XpenseUser` with a `Guid` key. Every later task consumes it.

- [ ] **Step 1: Add the packages**

`Xpense.Domain.csproj` gets `Microsoft.Extensions.Identity.Stores` 10.0.10 — that is what
`IdentityUser<Guid>` lives in. `Xpense.Persistence.csproj` gets
`Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.10. Keep versions aligned with the pinned EF
10.0.10 line; do not let NuGet float them.

- [ ] **Step 2: The user entity**

```csharp
using Microsoft.AspNetCore.Identity;
using Xpense.Domain.Enums;

namespace Xpense.Domain.Entities;

public class XpenseUser : IdentityUser<Guid>
{
    public AccountState State { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public void Touch() => UpdatedAt = DateTime.UtcNow;
}
```

`AccountState` is `Pending`, `Active`, `Suspended`. The recovery-password hash is Identity's own
nullable `PasswordHash` — do not add a second column for it.

- [ ] **Step 3: Rebase the context**

`XpenseDbContext` becomes
`IdentityDbContext<XpenseUser, IdentityRole<Guid>, Guid>`. Both constructors stay.

**Move `base.OnModelCreating(modelBuilder)` to the first line of `OnModelCreating`.** It is currently
last. `IdentityDbContext.OnModelCreating` is what adds the Identity entity types to the model, and
the three loops below it (`ConfigureDecimalColumnsStore`, `ConfigureUtcDateTimes`,
`ApplyGlobalQueryFilter`) enumerate `modelBuilder.Model.GetEntityTypes()`. Left last, the Identity
tables never get the UTC value converter. This is a real defect the base-class change introduces, not
a style preference.

`ApplyGlobalQueryFilter` only touches types assignable to `BaseEntity`, so Identity tables are
untouched by soft delete. Correct — a user is deactivated through `State`, not hidden.

- [ ] **Step 4: Put the Identity tables in the `Xpense` schema**

One configuration file, not eight `ToTable` calls scattered around:

```csharp
public class IdentityEntityTypeConfiguration :
    IEntityTypeConfiguration<XpenseUser>,
    IEntityTypeConfiguration<IdentityRole<Guid>>
```

Set `builder.ToTable("Users", XpenseSchema)` and so on for `Roles`, `UserRoles`, `UserClaims`,
`UserLogins`, `UserTokens`, `RoleClaims`, `UserPasskeys`. Constants for the names at the top of the
type. Roles are unused by this plan but the store demands the tables; do not try to trim them.

- [ ] **Step 5: Generate the migration**

```bash
cd api/src/Xpense
dotnet dotnet-ef migrations add AddIdentity --project Xpense.Persistence --startup-project Xpense.Persistence
```

Read the generated `Up`. It must contain only `CreateTable` and `CreateIndex`. **If it contains a
single `DropColumn`, `DropTable`, `AlterColumn` or `RenameTable`, stop and report** — this plan is
expand-only and something else changed the model.

- [ ] **Step 6: Verify**

```bash
cd api
dotnet build src/Xpense/Xpense.sln
dotnet test src/Xpense/Xpense.sln
```

The whole existing suite must pass unchanged. `PostgresFixture` runs the new migration when it builds
the template database, so a broken migration fails every integration test at once — that is the
signal you want here.

- [ ] **Step 7: Commit**

```bash
git add api/src/Xpense
git commit -m "feat: put ASP.NET Core Identity users in the Xpense schema"
```

---

### Task 2: Vault-side identity tables

Server-visible ciphertext holders. The server stores these and can open none of them.

**Files:**
- Create: `api/src/Xpense/Xpense.Domain/Entities/UserProfile.cs`
- Create: `api/src/Xpense/Xpense.Domain/Entities/UserEncryptionIdentity.cs`
- Create: `api/src/Xpense/Xpense.Domain/Entities/VaultWrapper.cs`
- Create: `api/src/Xpense/Xpense.Domain/Entities/PendingRegistration.cs`
- Create: `api/src/Xpense/Xpense.Domain/Enums/VaultWrapperKind.cs`
- Create: four files under `api/src/Xpense/Xpense.Persistence/TypeConfiguration/`
- Modify: `api/src/Xpense/Xpense.Persistence/XpenseDbContext.cs` (four `DbSet`s)
- Create: `api/src/Xpense/Xpense.Persistence/Migrations/<stamp>_AddVaultIdentity.cs` (generated)

**Interfaces:**
- Produces: `VaultWrapper`, consumed by Tasks 5, 7, 8 and 9.

- [ ] **Step 1: The entities**

All four use `Guid Id` primary keys (`Guid.CreateVersion7()` at creation, matching how `Event` already
mints ids) and do **not** extend `BaseEntity`. They are lifecycle-by-state, not soft-deleted, and
`BaseEntity` would give them an `int` key that must never cross the new contract.

`UserProfile`: `Id`, `UserId`, `Ciphertext` (`byte[]`), `Nonce`, `ProtocolVersion`, `Revision`,
`CreatedAt`, `UpdatedAt`. One row per user.

`UserEncryptionIdentity`: `Id`, `UserId` (unique), `PublicKey`, `EncryptedPrivateKey`, `Nonce`,
`ProtocolVersion`, `CreatedAt`. The public X25519 key is what another unlocked client encrypts a
group key to.

`VaultWrapper`: `Id`, `UserId`, `Kind`, `CredentialId` (`byte[]?`, set for `Passkey`), `Salt`,
`Ciphertext`, `Nonce`, `Parameters` (`string?`, jsonb, Argon2id parameters for the password wrapper),
`AuthenticationTokenHash` (`byte[]?`, SHA-256, set for `RecoveryFile`), `ConsumedAt`,
`ProtocolVersion`, `Label` (`string?`), `LastUsedAt`, `CreatedAt`, `UpdatedAt`.
`VaultWrapperKind` is `Passkey`, `RecoveryPassword`, `RecoveryFile`. One table for all three wrapper
kinds — three tables with the same five columns would buy nothing.

`PendingRegistration`: `Id`, `NormalizedEmail`, `AttestationState` (`string`, the opaque state
Identity hands back with the creation options), `ExpiresAt`, `ConsumedAt`, `CreatedAt`.

- [ ] **Step 2: Type configuration**

Every table `ToTable(name, XpenseSchema)`. Byte arrays get a `HasMaxLength` ceiling so an oversized
body is a database error, not a disk-filling one: 4096 for keys, nonces and salts, 65536 for profile
ciphertext. Unique index on `UserEncryptionIdentity.UserId`, on
`VaultWrapper (UserId, Kind, CredentialId)`, on `VaultWrapper.AuthenticationTokenHash` (filtered to
non-null), and on `PendingRegistration.NormalizedEmail` (filtered to unconsumed).

- [ ] **Step 3: Migration and verification**

```bash
cd api/src/Xpense
dotnet dotnet-ef migrations add AddVaultIdentity --project Xpense.Persistence --startup-project Xpense.Persistence
cd .. && cd ..
dotnet build src/Xpense/Xpense.sln && dotnet test src/Xpense/Xpense.sln
```

Same check as Task 1 step 5: `CreateTable` and `CreateIndex` only.

- [ ] **Step 4: Commit**

```bash
git add api/src/Xpense
git commit -m "feat: store vault wrappers and encryption identities as opaque ciphertext"
```

---

### Task 3: Groups, memberships, invitations, grants

**Files:**
- Create: `api/src/Xpense/Xpense.Domain/Entities/Group.cs`
- Create: `api/src/Xpense/Xpense.Domain/Entities/GroupMembership.cs`
- Create: `api/src/Xpense/Xpense.Domain/Entities/GroupInvitation.cs`
- Create: `api/src/Xpense/Xpense.Domain/Entities/ResourceGrant.cs`
- Create: `api/src/Xpense/Xpense.Domain/Entities/SharedResource.cs`
- Create: `api/src/Xpense/Xpense.Domain/Entities/InvitationDelivery.cs`
- Create: `api/src/Xpense/Xpense.Domain/Enums/MembershipRole.cs`, `MembershipState.cs`,
  `InvitationState.cs`, `GrantPermission.cs`, `GrantState.cs`, `SharedResourceType.cs`,
  `DeliveryStatus.cs`
- Create: `api/src/Xpense/Xpense.Domain/Exceptions/GroupExceptions.cs`, `InvitationExceptions.cs`
- Create: six files under `api/src/Xpense/Xpense.Persistence/TypeConfiguration/`
- Modify: `api/src/Xpense/Xpense.Persistence/XpenseDbContext.cs`
- Create: `api/src/Xpense/Xpense.Persistence/Migrations/<stamp>_AddGroupsAndSharing.cs` (generated)

**Interfaces:**
- Produces: the whole group model. Tasks 11 through 17 consume it.

- [ ] **Step 1: The entities**

Guid keys throughout, same reasoning as Task 2.

`Group`: `Id`, `OwnerUserId`, `NameCiphertext`, `NameNonce`, `ProtocolVersion`, `IsDeleted`,
`CreatedAt`, `UpdatedAt`. `IsDeleted` here is a plain column — `Group` does not extend `BaseEntity`,
so the global filter does not apply and every query must exclude it explicitly. Task 11's
`AccessRules` is the single place that does it.

`GroupMembership`: `Id`, `GroupId`, `UserId`, `Role` (`Owner`/`Member`), `State`
(`AwaitingOwnerApproval`/`Active`/`Revoked`), `GroupKeyEnvelope` (`byte[]?`, null until an Owner
client creates it), `EnvelopeProtocolVersion`, `CreatedAt`, `UpdatedAt`, `RevokedAt`.
Unique on `(GroupId, UserId)`.

`GroupInvitation`: `Id`, `GroupId`, `InvitedByUserId`, `TargetNormalizedEmail` (`string?`),
`TokenHash` (`byte[]`, unique), `State` (`Pending`/`AwaitingOwnerApproval`/`Accepted`/`Revoked`),
`ExpiresAt`, `AcceptedByUserId`, `GroupKeyEnvelope` (`byte[]?`, pre-encrypted when the invitee is a
known user), `CreatedAt`, `UpdatedAt`. There is no `Expired` state — expiry is
`ExpiresAt <= DateTime.UtcNow`, so nothing has to run to make an invitation stale.

`ResourceGrant`: `Id`, `GroupId`, `ResourceType`, `ResourceId` (`Guid`, the client's record UUID),
`Permission` (`Viewer`/`Editor`), `State` (`Active`/`Revoked`), `GrantedByUserId`,
`KeyEnvelopeReference` (`Guid?`), `CreatedAt`, `UpdatedAt`, `RevokedAt`. Unique index on
`(GroupId, ResourceType, ResourceId)` filtered to `State = Active`.

`SharedResource`: `Id` (`Guid`, the record UUID itself), `Type`, `OwnerUserId`, `CreatedAt`. This is
how the server knows who may grant a resource before the encrypted-record table exists. See the
assumption at the end of this plan.

`InvitationDelivery`: `Id`, `InvitationId` (unique), `EmailAddress`, `ProtectedPayload` (`string?`),
`Status` (`Pending`/`Sent`/`Failed`/`Disabled`), `Attempts`, `LastError`, `SentAt`, `CreatedAt`,
`UpdatedAt`.

Exceptions extend the existing `XpenseException` family. `GroupNotFoundException`,
`GroupOwnerCannotLeaveException`, `LastVaultWrapperException` go in `GroupExceptions.cs`;
`InvitationInvalidException` and `InvitationStateConflictException` in `InvitationExceptions.cs`.

- [ ] **Step 2: Migration and verification**

```bash
cd api/src/Xpense
dotnet dotnet-ef migrations add AddGroupsAndSharing --project Xpense.Persistence --startup-project Xpense.Persistence
cd ../..
dotnet build src/Xpense/Xpense.sln && dotnet test src/Xpense/Xpense.sln
```

- [ ] **Step 3: Commit**

```bash
git add api/src/Xpense
git commit -m "feat: add groups, memberships, invitations and resource grants"
```

---

### Task 4: Authentication configuration

No endpoints yet. This is the wiring that every auth endpoint depends on, and it is where the
security defaults are decided once instead of per slice.

**Files:**
- Create: `api/src/Xpense/Xpense.API/Infrastructure/Authentication/XpenseAuthenticationOptions.cs`
- Modify: `api/src/Xpense/Xpense.API/Extensions/IoC.cs`
- Modify: `api/src/Xpense/Xpense.API/Program.cs`
- Modify: `api/src/Xpense/Xpense.API/appsettings.json`, `appsettings.Development.json`
- Modify: `api/docker-compose.yml`
- Create: `api/src/Xpense/Xpense.Tests/Integration/AuthenticationConfigurationTests.cs`

- [ ] **Step 1: The options record**

```csharp
public sealed class XpenseAuthenticationOptions
{
    public const string SectionName = "Authentication";

    public string RelyingPartyDomain { get; set; } = "localhost";

    public string RelyingPartyName { get; set; } = "Xpense";

    public string[] AllowedOrigins { get; set; } = [];

    public RegistrationPolicy Registration { get; set; } = RegistrationPolicy.Open;

    public string PublicUrl { get; set; } = "http://localhost:5173";
}
```

`RegistrationPolicy` is `Open`, `InviteOnly`, `Closed`. Validate on start with
`AddOptions<XpenseAuthenticationOptions>().Bind(...).ValidateOnStart()`: outside Development,
`RelyingPartyDomain` may not be `localhost` and `PublicUrl` must be `https`. **The relying-party
domain is never read from the `Host` header.**

- [ ] **Step 2: Identity, cookie, passkeys**

In `IoC.cs`, one new `AddXpenseAuthentication(this IServiceCollection, IConfiguration)`:

- `AddIdentityCore<XpenseUser>()` with `.AddEntityFrameworkStores<XpenseDbContext>()`,
  `.AddSignInManager()`, `.AddDefaultTokenProviders()`.
- `options.User.RequireUniqueEmail = true`; `options.Password.RequiredLength = 14` (the recovery
  password minimum from the vault spec); `options.SignIn.RequireConfirmedAccount = false`.
- `Configure<IdentityPasskeyOptions>`: `ServerDomain` from configuration, and `ValidateOrigin` set to
  a delegate that accepts only `AllowedOrigins`. User verification is required.
- `AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies()`, then
  `ConfigureApplicationCookie`: `Cookie.Name = "xpense.session"`, `HttpOnly = true`,
  `SameSite = SameSiteMode.Lax`, `SecurePolicy = CookieSecurePolicy.Always` outside Development,
  `ExpireTimeSpan = 7 days`, `SlidingExpiration = true`.
- **`Events.OnRedirectToLogin` and `OnRedirectToAccessDenied` must write the status code and return.**
  The default 302 to a login page is wrong for an API and would turn every missing-session case into
  a 200-shaped redirect. The spec says 401.
- `AddAntiforgery(options => options.HeaderName = "X-Xpense-Antiforgery")`.
- `AddRateLimiter` with a fixed-window policy named `auth`: 10 permits per minute partitioned by
  remote IP, `QueueLimit = 0`, rejection status 429.
- `AddDataProtection().SetApplicationName("Xpense").PersistKeysToFileSystem(new DirectoryInfo(path))`
  where `path` comes from configuration. **The notifications worker must use the same application
  name and key directory**, because Task 17 unprotects there what the API protected here. Add a
  shared `xpense-dataprotection` volume mounted at `/keys` on both the `api` and `notifications`
  services in `docker-compose.yml`.

In `Program.cs`, add `app.UseAuthentication();` and `app.UseAuthorization();` after `app.UseRouting()`
and before `app.MapEndpoints()`, plus `app.UseRateLimiter();`. Do not add
`app.UseAntiforgery()` — minimal APIs only auto-validate antiforgery for form binding, and Task 10
adds an explicit filter instead.

- [ ] **Step 3: Tests**

`AuthenticationConfigurationTests.cs`:

```
An_unauthenticated_request_to_a_protected_route_returns_401_not_a_redirect
The_session_cookie_is_http_only_secure_and_same_site_lax
The_relying_party_domain_comes_from_configuration_not_the_host_header
Starting_outside_development_with_a_localhost_relying_party_domain_fails_fast
```

The first one needs a route that already requires authorization — map a throwaway one in the test
factory, or reorder this step after Task 10 and assert against `GET /api/v1/auth/me`. Prefer the
second: fewer moving parts.

- [ ] **Step 4: Verify**

```bash
cd api && dotnet build src/Xpense/Xpense.sln && dotnet test src/Xpense/Xpense.sln --filter "FullyQualifiedName~AuthenticationConfiguration"
```

- [ ] **Step 5: Commit**

```bash
git add api
git commit -m "feat: configure identity cookies, passkey options, rate limits and data protection"
```

---

### Task 5: Passkey registration ceremony

The riskiest endpoint task. Registration must not create a usable account until the client has proved
it can unlock the vault it just created.

**Files:**
- Create: `api/src/Xpense/Xpense.API/Features/Auth/CreateRegistrationOptions.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Auth/RegisterUser.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Auth/CurrentIdentityResponse.cs`
- Create: `api/src/Xpense/Xpense.Tests/Infrastructure/SoftwareAuthenticator.cs`
- Create: `api/src/Xpense/Xpense.Tests/Integration/AuthenticationEndpointTests.cs`

**Interfaces:**
- Produces: `POST /api/v1/auth/register/options`, `POST /api/v1/auth/register`,
  `CurrentIdentityResponse` (feature-shared, also returned by `GET /auth/me` in Task 8).

- [ ] **Step 1: The test authenticator**

`SoftwareAuthenticator` mints an ECDSA P-256 key, builds `clientDataJSON` and `authenticatorData`,
signs, and produces the attestation and assertion JSON that Identity's handler expects. Roughly 120
lines using `System.Security.Cryptography` only. Nothing else in the suite can exercise a real
ceremony.

**Escape hatch, if this stalls for more than one working session:** register a fake
`IPasskeyHandler<XpenseUser>` in `WebApiTestFactory` that returns a canned
`PasskeyAttestationResult`/`PasskeyAssertionResult`. That still tests everything this plan owns —
challenge expiry, single use, wrapper requirements, neutral failures, state transitions — and leaves
only the WebAuthn byte format to browser testing. Take the escape hatch rather than sinking days
into CBOR. Report which route was taken.

- [ ] **Step 2: Write the failing tests**

```
Requesting_registration_options_returns_a_challenge_and_a_pending_registration
Requesting_registration_options_for_a_taken_email_looks_the_same_as_for_a_free_one
Registration_creates_the_user_the_encryption_identity_and_the_vault_wrapper_together
Registration_without_a_vault_wrapper_creates_no_user
Registration_without_an_encryption_public_key_creates_no_user
Registration_with_an_expired_pending_registration_creates_no_user
Registration_replaying_a_consumed_challenge_creates_no_user
Registration_is_refused_when_the_policy_is_closed
```

Every negative test asserts twice: the status code, and that `Users` has no row for that email. A
half-created account that can authenticate but never unlock is the failure this endpoint exists to
prevent.

- [ ] **Step 3: `CreateRegistrationOptions`**

Route: `POST /api/v1/auth/register/options`, `.Validated()`, `.RequireRateLimiting("auth")`,
`.AllowAnonymous()`.

Request is `(string Email)`. Validator: not empty, valid email shape, max 256, prose messages.

Handler: normalize the email through `UserManager.NormalizeEmail`, call
`SignInManager.MakePasskeyCreationOptionsAsync` for a `PasskeyUserEntity` built from a fresh
`Guid.CreateVersion7()` and that email, insert a `PendingRegistration` holding the returned state
with `ExpiresAt = DateTime.UtcNow.AddMinutes(5)`, save, and return
`TypedResults.Ok(new Response(optionsJson, pendingRegistrationId))`.

**The response is identical whether or not the email is already registered.** Do not check for an
existing user here; the collision is detected at commit in step 4 and reported the same way as any
other failure. This is the "authentication failures do not reveal whether an email exists" rule.

- [ ] **Step 4: `RegisterUser`**

Route: `POST /api/v1/auth/register`, same filters.

Request:

```csharp
public sealed record Request(
    Guid PendingRegistrationId,
    string CredentialJson,
    string EncryptionPublicKey,
    string EncryptedPrivateKey,
    string EncryptedPrivateKeyNonce,
    VaultWrapperRequest VaultWrapper,
    int ProtocolVersion);
```

Base64 strings on the wire, `byte[]` in the entity. Validator enforces: every field present, each
decoded length inside the configured ceiling, `ProtocolVersion == 1`.

Handler, inside one `BeginTransactionAsync(IsolationLevel.Serializable)` — the same shape
`CreateTransaction` uses:

1. Load the `PendingRegistration`; missing, consumed or expired means throw
   `InvitationInvalidException`'s sibling `RegistrationChallengeInvalidException`. One neutral
   message for all three.
2. Refuse when `Registration` policy is `Closed`, or is `InviteOnly` and the request carries no valid
   invitation token.
3. `SignInManager.PerformPasskeyAttestationAsync` with the credential JSON and the stored state.
   A failed attestation throws.
4. Create the `XpenseUser` (`State = Active`, `CreatedAt`), then the `UserEncryptionIdentity`, then
   the `VaultWrapper`, then `UserManager.AddOrUpdatePasskeyAsync`.
5. Mark the pending registration consumed. Save. Commit.
6. `SignInManager.SignInAsync` to set the cookie, and return
   `TypedResults.Created(httpContext.ResourceUri("/api/v1/auth/me"), CurrentIdentityResponse.Of(...))`.

Steps 4 and 5 are one transaction on purpose: no user row exists unless the wrapper exists.

- [ ] **Step 5: Verify**

```bash
cd api && dotnet test src/Xpense/Xpense.sln --filter "FullyQualifiedName~AuthenticationEndpointTests"
```

- [ ] **Step 6: Commit**

```bash
git add api/src/Xpense
git commit -m "feat: register a user only once its vault wrapper exists"
```

---

### Task 6: Passkey sign-in ceremony

**Files:**
- Create: `api/src/Xpense/Xpense.API/Features/Auth/CreatePasskeyRequestOptions.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Auth/SignInWithPasskey.cs`
- Modify: `api/src/Xpense/Xpense.Tests/Integration/AuthenticationEndpointTests.cs`

- [ ] **Step 1: Write the failing tests**

```
Signing_in_with_a_registered_passkey_sets_the_session_cookie
Signing_in_returns_the_users_vault_wrapper_so_the_client_can_unlock
Signing_in_with_a_second_registered_passkey_also_works
Signing_in_with_an_unknown_credential_returns_the_same_response_as_a_wrong_one
Signing_in_with_an_expired_challenge_is_refused
Replaying_a_consumed_assertion_is_refused
Signing_in_when_no_vault_wrapper_matches_the_credential_still_authenticates
Eleven_sign_in_attempts_in_a_minute_returns_429
```

The seventh is the `Vault locked` rule: authentication succeeded, unlock did not, and the response
says so rather than failing the sign-in.

- [ ] **Step 2: Implement**

`CreatePasskeyRequestOptions`: `POST /api/v1/auth/passkey/options`, anonymous, rate-limited. Optional
`(string? Email)` request. Calls `SignInManager.MakePasskeyRequestOptionsAsync` and returns the
options JSON. A discoverable-credential sign-in sends no email at all.

`SignInWithPasskey`: `POST /api/v1/auth/passkey/sign-in`, anonymous, rate-limited. Calls
`SignInManager.PerformPasskeyAssertionAsync`, then `SignInManager.SignInAsync` on success. Loads the
user's `VaultWrapper` rows and returns them inside `CurrentIdentityResponse` alongside
`VaultState` (`Unlockable` when a wrapper matches the asserted credential, `Locked` otherwise).
Updates `VaultWrapper.LastUsedAt`.

Every failure path — unknown credential, bad signature, expired challenge, unknown user — throws the
same exception and produces the same ProblemDetails body and 401. Write it as one throw site so the
paths cannot drift apart.

- [ ] **Step 3: Verify and commit**

```bash
cd api && dotnet test src/Xpense/Xpense.sln --filter "FullyQualifiedName~AuthenticationEndpointTests"
git add api/src/Xpense
git commit -m "feat: sign in with a passkey and report whether the vault can unlock"
```

---

### Task 7: Recovery-password and recovery-file sign-in

**Files:**
- Create: `api/src/Xpense/Xpense.API/Features/Auth/SignInWithRecoveryPassword.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Auth/SignInWithRecoveryFile.cs`
- Modify: `api/src/Xpense/Xpense.Tests/Integration/AuthenticationEndpointTests.cs`

- [ ] **Step 1: Write the failing tests**

```
Recovery_password_sign_in_works_when_the_user_configured_one
Recovery_password_sign_in_returns_the_password_wrapper_with_its_argon2_parameters
Recovery_password_sign_in_for_an_unknown_email_looks_identical_to_a_wrong_password
Recovery_password_sign_in_is_refused_when_no_recovery_password_is_configured
Recovery_file_sign_in_consumes_the_token_and_a_second_attempt_is_refused
Recovery_file_sign_in_with_an_unknown_token_is_refused_neutrally
```

The third one is the important one. Assert the two responses are byte-identical, not merely both 401.

- [ ] **Step 2: Implement**

`SignInWithRecoveryPassword`: `POST /api/v1/auth/password/sign-in`, anonymous, rate-limited. Request
`(string Email, string Password)`. `SignInManager.PasswordSignInAsync` with `lockoutOnFailure: true`.
On success return the `RecoveryPassword` wrapper, including its stored Argon2id `Parameters` — the
client needs the salt and cost parameters to derive its local wrapping key.

`SignInWithRecoveryFile`: `POST /api/v1/auth/recovery/sign-in`, anonymous, rate-limited. Request
`(string AuthenticationToken)`. Hash it with SHA-256 and look up
`VaultWrapper.AuthenticationTokenHash` where `ConsumedAt is null`. On match: set `ConsumedAt`, sign
the user in, return the wrapper. **The vault secret is not in this table and never crosses the
network** — the client already holds it, in the file.

Failures throw the same exception as Task 6.

- [ ] **Step 3: Verify and commit**

```bash
cd api && dotnet test src/Xpense/Xpense.sln --filter "FullyQualifiedName~Recovery"
git add api/src/Xpense
git commit -m "feat: sign in with a recovery password or a single-use recovery file"
```

---

### Task 8: Session endpoints

**Files:**
- Create: `api/src/Xpense/Xpense.API/Features/Auth/SignOutSession.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Auth/GetCurrentIdentity.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Auth/GetAntiforgeryToken.cs`
- Modify: `api/src/Xpense/Xpense.Tests/Integration/AuthenticationEndpointTests.cs`

- [ ] **Step 1: Write the failing tests**

```
Logging_out_clears_the_cookie_and_a_later_request_returns_401
Logging_out_rotates_the_security_stamp_so_an_old_cookie_is_dead
Me_returns_the_email_the_group_summaries_and_the_vault_wrapper_availability
Me_without_a_session_returns_401
Me_never_returns_a_decrypted_display_name
```

- [ ] **Step 2: Implement**

`SignOutSession`: `POST /api/v1/auth/logout`. Requires authentication. Calls
`UserManager.UpdateSecurityStampAsync` then `SignInManager.SignOutAsync`. The stamp update is what
kills sessions on other devices; without it, logout only clears one cookie.

`GetCurrentIdentity`: `GET /api/v1/auth/me`. Returns `CurrentIdentityResponse`: user id, email,
account state, `VaultWrapperKind[]` available, and a `GroupSummaryResponse[]` of active memberships
(group id, role, whether a key envelope is present). It returns the group's **ciphertext** name; it
never returns anything decrypted, because the server cannot decrypt anything.

`GetAntiforgeryToken`: `GET /api/v1/auth/antiforgery`. Returns
`IAntiforgery.GetAndStoreTokens(httpContext).RequestToken` and sets the paired cookie. Anonymous, so
the login form can obtain a token before a session exists.

- [ ] **Step 3: Verify and commit**

```bash
cd api && dotnet test src/Xpense/Xpense.sln --filter "FullyQualifiedName~AuthenticationEndpointTests"
git add api/src/Xpense
git commit -m "feat: expose the current identity, logout and an antiforgery token"
```

---

### Task 9: Passkey management

**Files:**
- Create: `api/src/Xpense/Xpense.API/Features/Users/ListPasskeys.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Users/CreatePasskeyRegistrationOptions.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Users/AddPasskey.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Users/DeletePasskey.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Users/PasskeyResponse.cs`
- Create: `api/src/Xpense/Xpense.Tests/Integration/PasskeyManagementTests.cs`

- [ ] **Step 1: Write the failing tests**

```
Listing_passkeys_returns_labels_and_last_used_times_for_the_current_user_only
Adding_a_second_passkey_requires_a_wrapper_for_it
Deleting_a_passkey_leaves_the_other_one_usable
Deleting_the_last_passkey_is_refused_when_no_recovery_wrapper_exists
Deleting_the_last_passkey_is_allowed_when_a_recovery_wrapper_exists
Deleting_another_users_passkey_returns_404
```

The fourth is the rule that keeps a vault openable. The sixth is 404-not-403, which the whole plan
follows.

- [ ] **Step 2: Implement**

All four require authentication.

- `GET /api/v1/users/me/passkeys` — `UserManager.GetPasskeysAsync`, joined to `VaultWrapper` for the
  label and `LastUsedAt`.
- `POST /api/v1/users/me/passkeys/options` — creation options for an already-signed-in user.
- `POST /api/v1/users/me/passkeys` — attestation plus its `VaultWrapper`, in one serializable
  transaction, exactly like Task 5 step 4.
- `DELETE /api/v1/users/me/passkeys/{credentialId}` — deletes the credential and its wrapper. Before
  deleting, count the user's remaining wrappers. If the delete would leave zero, throw
  `LastVaultWrapperException`, which maps to 409. Return 404 when the credential belongs to another
  user — the same response as when it does not exist.

- [ ] **Step 3: Verify and commit**

```bash
cd api && dotnet test src/Xpense/Xpense.sln --filter "FullyQualifiedName~PasskeyManagement"
git add api/src/Xpense
git commit -m "feat: manage additional passkeys without stranding the vault"
```

---

### Task 10: Turn on the boundary

The task most likely to produce a red suite. Everything before it was additive; this one changes what
every existing endpoint requires.

**Files:**
- Create: `api/src/Xpense/Xpense.API/Infrastructure/Authentication/AntiforgeryEndpointFilter.cs`
- Modify: `api/src/Xpense/Xpense.API/Extensions/IoC.cs`
- Modify: `api/src/Xpense/Xpense.API/Infrastructure/EndpointExtensions.cs`
- Modify: `api/src/Xpense/Xpense.API/Features/Auth/*.cs` (add `.AllowAnonymous()` where needed)
- Create: `api/src/Xpense/Xpense.Tests/Infrastructure/AuthenticatedClient.cs`
- Modify: `api/src/Xpense/Xpense.Tests/Infrastructure/WebApiTestFactory.cs`
- Modify: `api/src/Xpense/Xpense.Tests/Integration/ApiEndpointTests.cs` (setup only)

- [ ] **Step 1: The fallback policy**

`AddAuthorizationBuilder().SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())`.
Every endpoint now needs a session unless it says `.AllowAnonymous()`. The anonymous set is exactly:
`/health`, the six `/api/v1/auth/*` sign-in and registration routes, `/api/v1/auth/antiforgery`, and
`GET /api/v1/invitations/{token}` from Task 15.

The legacy plaintext slices now require a session and still serve the same global dataset to any
authenticated user. That is not per-user isolation and is not meant to be — see the scope table.

- [ ] **Step 2: The antiforgery filter**

`AntiforgeryEndpointFilter` calls `IAntiforgery.ValidateRequestAsync` and lets a failure throw.
Attach it in `MapEndpoints` to every route whose method is POST, PUT, PATCH or DELETE, by inspecting
the endpoint metadata after `Map` has run. One place, so a new slice cannot forget it. Skip it for
requests with no authentication cookie — an unauthenticated request has no session to ride, so
there is nothing to forge.

- [ ] **Step 3: Keep the existing suite green**

`WebApiTestFactory` gains a `CreateAuthenticatedClient()` that seeds an `XpenseUser` directly through
the DbContext, signs it in, and returns an `HttpClient` whose handler carries the session cookie and
fetches an antiforgery token once, replaying it as `X-Xpense-Antiforgery` on every mutating request.
Put that handler in `AuthenticatedClient.cs`.

Then change **one line** in `ApiEndpointTests.SetUp`: `client = factory.CreateAuthenticatedClient()`.
Do not touch the 1708 lines of test bodies. If a test body needs editing, the filter or the policy is
wrong, not the test.

- [ ] **Step 4: New tests**

Append to `AuthenticationEndpointTests.cs`:

```
A_mutating_request_without_an_antiforgery_token_is_refused
A_mutating_request_with_a_stale_antiforgery_token_is_refused
A_read_request_needs_no_antiforgery_token
Every_route_except_the_documented_anonymous_set_requires_authentication
```

The last one enumerates the endpoint data source and asserts the anonymous set against a hard-coded
list. It is the test that catches a future slice shipped without a session requirement.

- [ ] **Step 5: Verify**

```bash
cd api && dotnet build src/Xpense/Xpense.sln && dotnet test src/Xpense/Xpense.sln
```

The full suite. This is the gate for the whole plan.

- [ ] **Step 6: Commit**

```bash
git add api/src/Xpense
git commit -m "feat: require a session and an antiforgery token on every mutating route"
```

---

### Task 11: The authorization boundary

**Files:**
- Create: `api/src/Xpense/Xpense.API/Infrastructure/Authorization/CurrentUser.cs`
- Create: `api/src/Xpense/Xpense.API/Infrastructure/Authorization/AccessRules.cs`
- Modify: `api/src/Xpense/Xpense.API/Extensions/IoC.cs`
- Create: `api/src/Xpense/Xpense.Tests/Integration/AccessRulesTests.cs`

**Interfaces:**
- Produces: `ICurrentUser` and `AccessRules`. Tasks 12 through 16 consume both.

**Why this does not violate slice isolation.** `SliceIsolationTests` forbids a type under
`Xpense.API.Features.<A>` from referencing a type under `Xpense.API.Features.<B>`. It says nothing
about `Xpense.API.Infrastructure.*`, which every slice already reaches for `IEndpoint`,
`ResourceUri` and `.Validated()`. The isolation rule exists so two features cannot couple their
request shapes, handlers and responses — and those stay inside their slices here.

"Duplication is deliberate" also carves out an exception, and this is squarely inside it: whether a
revoked member may still read a shared account is a domain invariant that holds regardless of the
caller, not a coincidental similarity between two EF queries. Duplicating it per slice means the
twelfth slice is the one that forgets to check `State = Active` and quietly serves a removed family
member. One place, one test suite, one thing to audit.

What stays out of `AccessRules`: request records, response records, validators, and any HTTP concern.
It returns booleans and id sets, never `IResult`.

- [ ] **Step 1: `ICurrentUser`**

```csharp
public interface ICurrentUser
{
    Guid Id { get; }

    bool IsAuthenticated { get; }
}
```

Implementation reads `IHttpContextAccessor.HttpContext.User` and parses
`ClaimTypes.NameIdentifier`. Registered scoped. Requires `AddHttpContextAccessor()`.

- [ ] **Step 2: `AccessRules`**

Scoped, taking `XpenseDbContext` and `ICurrentUser`. Public surface:

```csharp
Task<bool> IsActiveMember(Guid groupId, CancellationToken cancellationToken);
Task<bool> IsGroupOwner(Guid groupId, CancellationToken cancellationToken);
Task<bool> CanRead(SharedResourceType type, Guid resourceId, CancellationToken cancellationToken);
Task<bool> CanEdit(SharedResourceType type, Guid resourceId, CancellationToken cancellationToken);
Task<bool> CanReadAny(SharedResourceType type, IReadOnlyCollection<Guid> resourceIds, CancellationToken cancellationToken);
Task<bool> CanEditAll(SharedResourceType type, IReadOnlyCollection<Guid> resourceIds, CancellationToken cancellationToken);
Task<Guid[]> ReadableResourceIds(SharedResourceType type, CancellationToken cancellationToken);
```

Each is one EF query starting from `currentUser.Id`. `CanRead` is:

```
SharedResource.OwnerUserId == currentUser.Id
||
(GroupMembership.UserId == currentUser.Id
 && GroupMembership.State == Active
 && ResourceGrant.GroupId == GroupMembership.GroupId
 && ResourceGrant.State == Active
 && ResourceGrant.ResourceType == type
 && ResourceGrant.ResourceId == resourceId
 && !Group.IsDeleted)
```

`CanEdit` adds `Permission == Editor`. `CanReadAny`/`CanEditAll` are the transfer rules: a transfer is
readable when at least one involved account is readable, and writable only when every involved
account is Editor-writable.

**No EF global filter is added for any of this.** A global filter is a single predicate over one
entity; this is a three-way join whose shape depends on the operation, and a filter that silently
hid rows would make an authorization bug look like an empty result. Slices call these methods and
return `TypedResults.NotFound()` on false.

- [ ] **Step 3: The permission matrix test**

`AccessRulesTests.cs` seeds two users, two groups and one shared account, then asserts the full
matrix from the spec:

```
The_private_owner_can_read_and_edit
A_group_viewer_can_read_and_cannot_edit
A_group_editor_can_read_and_edit
A_group_owner_without_a_grant_can_do_neither
A_revoked_member_can_do_neither
An_unrelated_user_can_do_neither
A_member_of_two_groups_sees_nothing_from_the_group_without_a_grant
A_transfer_is_readable_when_one_side_is_readable
A_transfer_is_not_editable_when_only_one_side_is_editable
A_deleted_group_grants_nothing
```

- [ ] **Step 4: Verify and commit**

```bash
cd api && dotnet test src/Xpense/Xpense.sln --filter "FullyQualifiedName~AccessRules"
git add api/src/Xpense
git commit -m "feat: authorize by explicit ownership, membership and grant queries"
```

---

### Task 12: Groups

**Files:**
- Create: `api/src/Xpense/Xpense.API/Features/Groups/CreateGroup.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Groups/ListGroups.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Groups/GetGroupById.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Groups/UpdateGroup.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Groups/DeleteGroup.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Groups/GroupResponse.cs`
- Create: `api/src/Xpense/Xpense.Tests/Integration/GroupEndpointTests.cs`

- [ ] **Step 1: Write the failing tests**

```
Creating_a_group_makes_the_creator_its_owner_with_an_active_membership
Creating_a_group_stores_only_ciphertext_for_its_name
Listing_groups_returns_only_the_callers_groups
Getting_another_users_group_returns_404
Renaming_a_group_as_a_member_returns_404
Deleting_a_group_with_other_members_is_refused
Deleting_an_empty_group_soft_deletes_it_and_it_disappears_from_the_list
```

- [ ] **Step 2: Implement**

Routes: `POST /api/v1/groups`, `GET /api/v1/groups`, `GET /api/v1/groups/{groupId:guid}`,
`PUT /api/v1/groups/{groupId:guid}`, `DELETE /api/v1/groups/{groupId:guid}`.

`CreateGroup` takes `(string NameCiphertext, string NameNonce, int ProtocolVersion, string OwnerKeyEnvelope)`
and, in one serializable transaction, inserts the `Group` and the owner's `GroupMembership` with
`Role = Owner`, `State = Active` and the supplied envelope. A group with no owner membership is not a
state this API can produce.

`DeleteGroup` throws when any other active membership exists, then `IsDeleted = true` and `Touch()`.

Every handler calls `AccessRules` first and returns `TypedResults.NotFound()` on false. **No handler
writes a 403 branch.**

- [ ] **Step 3: Verify and commit**

```bash
cd api && dotnet test src/Xpense/Xpense.sln --filter "FullyQualifiedName~GroupEndpointTests"
git add api/src/Xpense
git commit -m "feat: create and manage groups with encrypted names"
```

---

### Task 13: Memberships and ownership

**Files:**
- Create: `api/src/Xpense/Xpense.API/Features/Groups/ListGroupMembers.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Groups/RemoveGroupMember.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Groups/LeaveGroup.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Groups/TransferGroupOwnership.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Groups/GroupMemberResponse.cs`
- Modify: `api/src/Xpense/Xpense.Tests/Integration/GroupEndpointTests.cs`

- [ ] **Step 1: Write the failing tests**

```
Members_see_each_others_emails_and_roles
A_member_cannot_remove_another_member
Removing_a_member_revokes_the_membership_and_blocks_reads_immediately
A_removed_member_keeps_no_access_to_previously_shared_resources
The_owner_cannot_leave_before_transferring_ownership
Transferring_ownership_swaps_both_roles_in_one_step
Transferring_ownership_to_a_non_member_returns_404
A_member_can_leave_and_loses_access_immediately
```

"Immediately" means: revoke, then call a read endpoint in the same test and assert 404. No cache
exists to invalidate, and this test is what proves it.

- [ ] **Step 2: Implement**

Routes: `GET /api/v1/groups/{groupId:guid}/members`,
`DELETE /api/v1/groups/{groupId:guid}/members/{userId:guid}`,
`POST /api/v1/groups/{groupId:guid}/leave`,
`POST /api/v1/groups/{groupId:guid}/ownership`.

Removal sets `State = Revoked`, `RevokedAt`, clears `GroupKeyEnvelope` and touches. Clearing the
envelope is not security — the member's browser already has the key — it stops the server handing the
key back after revocation. The response carries a `KeyRotationRequired` flag so the Owner's client
knows to rotate; rotation itself is client work owned by the vault plan.

Ownership transfer moves `Role` on two memberships and updates `Group.OwnerUserId` in one serializable
transaction.

- [ ] **Step 3: Verify and commit**

```bash
cd api && dotnet test src/Xpense/Xpense.sln --filter "FullyQualifiedName~GroupEndpointTests"
git add api/src/Xpense
git commit -m "feat: manage group membership, revocation and ownership transfer"
```

---

### Task 14: Creating and revoking invitations

**Files:**
- Create: `api/src/Xpense/Xpense.API/Features/Invitations/CreateInvitation.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Invitations/ListInvitations.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Invitations/RevokeInvitation.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Invitations/InvitationResponse.cs`
- Create: `api/src/Xpense/Xpense.Tests/Integration/InvitationEndpointTests.cs`

- [ ] **Step 1: Write the failing tests**

```
Creating_an_invitation_returns_a_copyable_link_and_stores_only_a_hash_of_its_token
Creating_an_invitation_for_a_known_email_accepts_a_pre_encrypted_group_key_envelope
Creating_an_invitation_as_a_member_returns_404
An_invitation_expires_seven_days_after_it_is_created
Listing_invitations_shows_pending_and_awaiting_approval_ones_for_the_owner_only
Revoking_an_invitation_makes_a_later_acceptance_fail_neutrally
The_bearer_token_never_appears_in_the_database
```

The last one queries `GroupInvitations` directly and asserts no column contains the token string.

- [ ] **Step 2: Implement**

`POST /api/v1/invitations` with `(Guid GroupId, string? TargetEmail, string? GroupKeyEnvelope, int ProtocolVersion)`.
Owner only. Mints 32 random bytes with `RandomNumberGenerator.GetBytes(32)`, stores
`SHA256.HashData(token)`, sets `ExpiresAt = DateTime.UtcNow.AddDays(7)`, and returns the link
`{PublicUrl}/invitations/{base64UrlToken}` **once**. There is no endpoint that returns it again;
losing it means revoking and reissuing.

An envelope may only be supplied together with a `TargetEmail` that matches an existing user. Without
one, the invitation is an open link and Task 15 forces owner approval.

`GET /api/v1/invitations?groupId=` lists an owner's invitations, never the token.
`DELETE /api/v1/invitations/{invitationId:guid}` sets `State = Revoked`.

`CreateInvitation` also emits `GroupInvitationCreated(Guid InvitationId)` through `IEventBus` and
inserts the `InvitationDelivery` row — see Task 17. `Emit` does not save, so it commits with the
invitation or not at all.

- [ ] **Step 3: Verify and commit**

```bash
cd api && dotnet test src/Xpense/Xpense.sln --filter "FullyQualifiedName~InvitationEndpointTests"
git add api/src/Xpense
git commit -m "feat: issue single-use group invitation links without storing the token"
```

---

### Task 15: Accepting invitations and owner approval

**Files:**
- Create: `api/src/Xpense/Xpense.API/Features/Invitations/InspectInvitation.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Invitations/AcceptInvitation.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Invitations/ApproveInvitation.cs`
- Modify: `api/src/Xpense/Xpense.Tests/Integration/InvitationEndpointTests.cs`

- [ ] **Step 1: Write the failing tests**

```
Inspecting_a_pending_invitation_returns_its_state_without_the_groups_encrypted_name
Inspecting_a_consumed_an_expired_a_revoked_and_an_unknown_invitation_return_identical_responses
Accepting_a_bound_invitation_with_an_envelope_makes_the_member_active_at_once
Accepting_an_open_link_leaves_the_membership_awaiting_owner_approval
An_awaiting_member_cannot_read_any_shared_resource
Approving_a_request_stores_the_envelope_and_activates_the_membership
Approving_someone_elses_group_request_returns_404
Accepting_the_same_invitation_twice_returns_409
Accepting_an_invitation_bound_to_a_different_email_returns_the_neutral_invalid_response
```

The second one asserts identical status **and** identical body across all four cases.

- [ ] **Step 2: Implement**

`GET /api/v1/invitations/{token}` — anonymous, rate-limited. Hashes the token, looks the invitation
up, and returns either `(State: Pending, RequiresApproval: bool, InviterEmail)` or the single neutral
invalid response. It returns the group's encrypted name **only** once the caller holds a key envelope
for that group; before that, not even the ciphertext.

`POST /api/v1/invitations/accept` with `(string Token)` — requires authentication, so an unknown user
registers or signs in first. In one serializable transaction:

1. Hash and load. Invalid, expired, revoked or already accepted throws
   `InvitationInvalidException` (404, neutral) or `InvitationStateConflictException` (409) for the
   double-accept race specifically.
2. When the invitation is bound to an email that matches the caller **and** carries an envelope:
   create the membership `Active` with that envelope.
3. Otherwise create the membership `AwaitingOwnerApproval` with a null envelope.
4. Mark the invitation `Accepted` with `AcceptedByUserId`.

`POST /api/v1/invitations/{invitationId:guid}/approve` with `(string GroupKeyEnvelope, int ProtocolVersion)`
— Owner only. Moves the membership to `Active` and stores the envelope the Owner's unlocked client
just produced. **The server never generates an envelope**; it has nothing to generate one from, and
that is the property the approval step exists to preserve.

- [ ] **Step 3: Verify and commit**

```bash
cd api && dotnet test src/Xpense/Xpense.sln --filter "FullyQualifiedName~InvitationEndpointTests"
git add api/src/Xpense
git commit -m "feat: accept invitations, with owner approval for unbound links"
```

---

### Task 16: Resource grants

**Files:**
- Create: `api/src/Xpense/Xpense.API/Features/Groups/CreateResourceGrant.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Groups/ListResourceGrants.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Groups/RevokeResourceGrant.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Groups/ResourceGrantResponse.cs`
- Create: `api/src/Xpense/Xpense.Tests/Integration/ResourceGrantTests.cs`

- [ ] **Step 1: Write the failing tests**

```
The_resource_owner_can_grant_a_group_viewer_access
The_resource_owner_can_grant_a_group_editor_access
A_group_owner_who_does_not_own_the_resource_cannot_grant_it
A_second_grant_on_the_same_resource_and_group_returns_409
Revoking_a_grant_blocks_reads_on_the_next_request
Only_the_resource_owner_can_revoke_its_grant
Granting_to_a_group_the_caller_does_not_belong_to_returns_404
A_grant_to_one_group_is_invisible_to_another_group
```

- [ ] **Step 2: Implement**

`POST /api/v1/groups/{groupId:guid}/grants` with
`(SharedResourceType ResourceType, Guid ResourceId, GrantPermission Permission, Guid? KeyEnvelopeReference)`.

Handler, in one serializable transaction:

1. Look up `SharedResource` by `ResourceId`. If absent, insert it with
   `OwnerUserId = currentUser.Id` (see the assumption at the end of this plan). If present and owned
   by someone else, `TypedResults.NotFound()`.
2. Require an active membership in the target group.
3. Insert the grant `Active`. A duplicate active grant throws a 409 conflict.

`DELETE /api/v1/groups/{groupId:guid}/grants/{grantId:guid}` — resource owner only. Sets
`State = Revoked`, `RevokedAt`, clears `KeyEnvelopeReference`. The response carries
`KeyRotationRequired = true` and a `Warning` string stating that revocation cannot erase what a
former member already saved. That sentence is the product copy the spec requires and it belongs in
the response, not only in the UI.

- [ ] **Step 3: Verify and commit**

```bash
cd api && dotnet test src/Xpense/Xpense.sln --filter "FullyQualifiedName~ResourceGrant"
git add api/src/Xpense
git commit -m "feat: grant and revoke group access to one resource at a time"
```

---

### Task 17: Invitation email delivery on the Events queue

**Files:**
- Create: `api/src/Xpense/Xpense.Domain/Events/GroupInvitationCreated.cs`
- Create: `api/src/Xpense/Xpense.Notifications/Rules/InvitationEmailRule.cs`
- Create: `api/src/Xpense/Xpense.Notifications/EmailSender.cs`
- Modify: `api/src/Xpense/Xpense.Notifications/Program.cs`
- Modify: `api/src/Xpense/Xpense.Notifications/appsettings.json`
- Modify: `api/docker-compose.yml`
- Create: `api/src/Xpense/Xpense.Tests/Integration/InvitationDeliveryTests.cs`

**Interfaces:**
- Consumes: `IEventBus`, `InvitationDelivery` (Task 3), `CreateInvitation` (Task 14).

- [ ] **Step 1: The event**

```csharp
namespace Xpense.Domain.Events;

public sealed record GroupInvitationCreated(Guid InvitationId) : EventBody;
```

One field, by design. `EventContractTests` already enforces primitives-only bodies; an email address
in the body would put a server-visible identifier in an audit log that is never pruned.

- [ ] **Step 2: The delivery rule**

Do **not** add a second background service. `EventProcessor` marks every claimed row processed, so a
second pump would race it on `ProcessedAt`. Delivery rides the existing dispatcher as
`INotificationRule<GroupInvitationCreated>`:

1. Load the `InvitationDelivery` by invitation id. Missing, already `Sent`, or `Disabled` returns `[]`.
2. No email adapter configured: set `Status = Disabled`, clear `ProtectedPayload`, return `[]`. The
   invitation is unaffected — the owner already has the link.
3. Otherwise `IDataProtectionProvider.CreateProtector("Xpense.InvitationDelivery").Unprotect` the
   payload, send through `EmailSender`, set `Status = Sent`, `SentAt`, and clear `ProtectedPayload`.
4. On failure: increment `Attempts`, record `LastError`, and rethrow. The rethrow is what makes
   `EventRecord.Failed` retry it, and after `EventRecord.MaxAttempts` the row is its own dead letter.
   On the final attempt, set `Status = Failed` and clear `ProtectedPayload` — the plaintext link must
   not outlive delivery.

The rule returns `[]` always: an invitation email is not an in-app notification. Returning drafts
would put a delivery record in the `Notifications` table where nothing reads it.

`NotificationRuleIsolationTests` requires a public constructor and forbids referencing another rule.
Neither is a problem. There is no `*Payload` type here, so the timestamp rule does not apply.

- [ ] **Step 3: `EmailSender`**

One class over `System.Net.Mail.SmtpClient`, configured by host, port, credentials and from-address.
Absent configuration means "no adapter" and step 2 case 2 applies. The message body contains the
invitation link and nothing else — no group name, no amounts, no inviter's display name, since the
server has none of those in the clear anyway.

`CreateInvitation` (Task 14) protects the link with the same protector name and stores it on the
delivery row. Both processes need the same Data Protection application name and key directory, which
Task 4 step 2 set up.

- [ ] **Step 4: Write the tests**

`InvitationDeliveryTests.cs`, modelled on the existing `NotificationPipelineTests`:

```
Creating_an_invitation_writes_one_event_and_one_pending_delivery
The_rule_sends_the_link_and_marks_the_delivery_sent
The_protected_payload_is_deleted_once_delivery_succeeds
With_no_adapter_configured_the_delivery_is_disabled_and_the_invitation_still_stands
A_failing_adapter_records_the_error_and_the_invitation_still_stands
A_failing_adapter_clears_the_payload_after_the_final_attempt
The_delivery_row_never_contains_the_group_key_or_any_financial_field
An_unknown_event_type_still_leaves_the_pump_running
```

Use a stub `EmailSender` for success and failure. Do not open a socket in a test.

- [ ] **Step 5: Verify and commit**

```bash
cd api && dotnet test src/Xpense/Xpense.sln --filter "FullyQualifiedName~InvitationDelivery"
git add api/src/Xpense api/docker-compose.yml
git commit -m "feat: deliver invitation emails through the events queue"
```

---

### Task 18: Error behaviour sweep

**Files:**
- Create: `api/src/Xpense/Xpense.API/ExceptionHandlers/InvitationInvalidExceptionHandler.cs`
- Create: `api/src/Xpense/Xpense.API/ExceptionHandlers/StateConflictExceptionHandler.cs`
- Modify: `api/src/Xpense/Xpense.API/Extensions/IoC.cs`
- Create: `api/src/Xpense/Xpense.Tests/Integration/AuthorizationErrorTests.cs`
- Create: `api/src/Xpense/Xpense.Tests/Architecture/AuthorizationShapeTests.cs`

- [ ] **Step 1: The handlers**

`InvitationInvalidExceptionHandler` maps `InvitationInvalidException` to 404 with one fixed title and
detail: "This invitation is no longer valid." Every consumed, expired, revoked and unknown case
produces that exact body.

`StateConflictExceptionHandler` maps `InvitationStateConflictException` and
`LastVaultWrapperException` to 409, with a stable `errorCode` extension member so the client can
branch without parsing prose. Register both before `FallbackExceptionHandler`.

Stale-revision 409 with the latest ciphertext is not here — there is no revisioned record surface in
this plan. It belongs to the vault plan's `PUT /api/v1/sync/records/{id}`.

- [ ] **Step 2: The tests**

`AuthorizationErrorTests.cs`:

```
Every_unauthorized_read_returns_404_and_never_403
Every_unauthenticated_request_returns_401_and_never_302
A_409_carries_a_stable_error_code
All_problem_details_responses_use_application_problem_json
```

`AuthorizationShapeTests.cs` is an architecture test over the API assembly: no slice may construct a
`ForbidHttpResult` or return `StatusCodes.Status403Forbidden`. Model it on `SliceIsolationTests`'s
Mono.Cecil instruction scan. That is what stops the 404-not-403 rule decaying the first time someone
finds 403 more natural.

- [ ] **Step 3: Verify and commit**

```bash
cd api && dotnet test src/Xpense/Xpense.sln
git add api/src/Xpense
git commit -m "feat: make unauthorized look like missing and conflicts machine-readable"
```

---

### Task 19: The decision record and the operator documentation

**Files:**
- Create: `api/docs/adr/0009-authorization-is-an-explicit-query.md`
- Modify: `AGENTS.md`
- Modify: `api/README.md`
- Modify: `api/docs/docker.md`

- [ ] **Step 1: ADR 0009**

Follow the existing ADR format exactly: `status`/`date` front matter, then Why, Considered options,
Consequences. Record three decisions and their rejected alternatives:

- Authorization is an explicit per-slice query through `AccessRules`, not an EF global query filter.
  Rejected: a global filter (cannot express the three-way join, and turns an authorization bug into
  an empty list); an authorization policy per resource (`IAuthorizationHandler` needs the entity
  loaded first, which is the query you were trying to avoid writing).
- Unauthorized reads return 404, so record existence is not disclosed. Consequence: a genuine bug and
  a permission problem look the same to a client, so the server log has to carry the difference.
- The legacy plaintext slices require a session but have no per-user ownership until the vault plan's
  claim flow runs. Name that gap and name its owner.

- [ ] **Step 2: `AGENTS.md`**

Add a short Authorization section: every slice authorizes through `AccessRules`, never a 403, never a
global filter, and new tables outside `BaseEntity` do not get the soft-delete filter for free.

- [ ] **Step 3: Operator documentation**

`api/README.md` and `api/docs/docker.md` get the new configuration keys:
`Authentication:RelyingPartyDomain`, `Authentication:AllowedOrigins`, `Authentication:Registration`,
`Authentication:PublicUrl`, the Data Protection key path, and the SMTP block. Say plainly that
without SMTP, invitations still work and the owner copies the link.

- [ ] **Step 4: Verify and commit**

```bash
cd api && dotnet build src/Xpense/Xpense.sln && dotnet test src/Xpense/Xpense.sln
git add AGENTS.md api/docs api/README.md
git commit -m "docs: record why authorization is a query and not a filter"
```

---

## Testing checklist, mapped

Every bullet from the spec's Testing section, against the file that owns it.

| Spec item | File |
|---|---|
| Passkey registration, multiple credentials, challenge expiry, replay rejection | `Integration/AuthenticationEndpointTests.cs`, `Integration/PasskeyManagementTests.cs` |
| Passkey sign-in, logout | `Integration/AuthenticationEndpointTests.cs` |
| Recovery-password and recovery-file sign-in | `Integration/AuthenticationEndpointTests.cs` |
| Neutral unknown-user failures | `Integration/AuthenticationEndpointTests.cs` (identical-body assertions) |
| Cookie flags, antiforgery enforcement, rate limits, relying-party domain validation | `Integration/AuthenticationConfigurationTests.cs` |
| Permission matrix: owner, Viewer, Editor, group Owner without a grant, revoked member, unrelated user | `Integration/AccessRulesTests.cs` |
| Several groups with no cross-group leakage | `Integration/AccessRulesTests.cs`, `Integration/ResourceGrantTests.cs` |
| Transfer access and hidden private counterparties | `Integration/AccessRulesTests.cs` (`CanReadAny`/`CanEditAll`) |
| Shared-budget calculations excluding private accounts | **Not an API test.** The server cannot compute a budget over ciphertext. The API-side equivalent is `A_grant_to_one_group_is_invisible_to_another_group` in `Integration/ResourceGrantTests.cs`; the arithmetic is owned by the web vault plan. |
| Existing-user invitation, new-user pending approval, expiry, one-time use | `Integration/InvitationEndpointTests.cs` |
| Owner transfer and membership revocation | `Integration/GroupEndpointTests.cs` |
| Email adapter success, disabled and failure without invitation loss | `Integration/InvitationDeliveryTests.cs` |
| One endpoint per file and slice isolation still enforced | `Architecture/SliceIsolationTests.cs` (unchanged), plus `Architecture/AuthorizationShapeTests.cs` |

Final gate for the whole plan:

```bash
cd api
dotnet build src/Xpense/Xpense.sln
dotnet test src/Xpense/Xpense.sln
```

The build is warning-free. Keep it that way.

## Self-review

**Riskiest three, in order.**

1. **Task 5 and 6, the passkey ceremonies.** Producing valid WebAuthn attestation and assertion bytes
   in a test without a browser is the single hardest thing here. The escape hatch — substituting
   `IPasskeyHandler<XpenseUser>` — is written into the task on purpose. Take it early rather than
   late.
2. **Task 10, turning the boundary on.** It changes the requirements of 1708 lines of tests that
   currently call anonymously. The mitigation is that only `SetUp` may change; if a test body needs
   editing, the policy is wrong. That rule is what keeps a security change from quietly becoming a
   test rewrite.
3. **Task 1, rebasing `XpenseDbContext` on `IdentityDbContext`.** It changes the base class every
   existing integration test runs against, and the `base.OnModelCreating` reordering is easy to skip
   and produces a silent defect — Identity timestamps stored without the UTC converter — that no
   existing test would catch.

**Type consistency.** `XpenseUser.Id` is `Guid` and every new entity's foreign key to it is `Guid`.
Existing financial entities keep their `int` keys and are untouched. `SharedResource.Id` and
`ResourceGrant.ResourceId` are the client's record UUID, so the vault plan's encrypted-record table
can point at the same value without a translation step.

**Deliberately not here.** Per-user ownership of existing plaintext rows, the sync surface, the claim
flow, the contract migration, and all browser crypto. Each is named in the scope table with its
owner.

## Open questions resolved by assumption

The spec left these open. Each was decided the lazy way that a later plan can still change.

1. **Grant ownership before the encrypted-record table exists.** The spec says only a resource's
   personal owner may grant it, but there is no server-side record of financial ownership until the
   vault plan lands. **Assumption:** a small `SharedResource` table records `(recordId, type, owner)`,
   and the first grant on an unseen resource id claims ownership for the caller. A stranger who
   claims a UUID they do not own gains nothing readable — there is no envelope — and only blocks the
   true owner's later grant. **Upgrade path:** when `POST /api/v1/sync/records` exists, record
   creation writes the `SharedResource` row and grant creation stops claiming.
2. **Antiforgery token distribution.** The spec requires an antiforgery token on mutating requests but
   names no endpoint. **Assumption:** `GET /api/v1/auth/antiforgery`, anonymous, added to the surface.
3. **Creation options for an additional passkey.** The spec tables `GET/POST/DELETE /api/v1/users/me/passkeys`
   only, and a second passkey needs its own challenge. **Assumption:**
   `POST /api/v1/users/me/passkeys/options`, authenticated.
4. **The neutral invalid-invitation response.** **Assumption:** 404 with the fixed detail "This
   invitation is no longer valid." for consumed, expired, revoked and unknown alike. The
   double-accept race is the one exception, returning 409, because both callers know the invitation
   was real.
5. **Invitation route shape.** Nesting create under `/api/v1/groups/{groupId}/invitations` would tag
   it "Groups" in Swagger and split the feature across two folders. **Assumption:** every invitation
   route lives under `/api/v1/invitations`, with `groupId` in the body or query.
6. **Recovery-password storage.** **Assumption:** Identity's own nullable `PasswordHash` is the
   recovery-password hash. No second column.
7. **Recovery-file storage.** **Assumption:** a `VaultWrapper` row of kind `RecoveryFile` carrying
   `AuthenticationTokenHash` and `ConsumedAt`, rather than a separate table.
8. **Group deletion.** **Assumption:** soft delete, refused while any other active membership exists.
   The spec says the Owner must transfer or delete an empty group but does not say what deletion is.
9. **Session lifetime.** **Assumption:** a seven-day sliding cookie. The spec sets no number. It is
   one configuration line to change.
10. **Rate limits.** **Assumption:** 10 requests per minute per IP on registration, sign-in, recovery
    and invitation inspection. The spec says "rate-limited" without numbers.
11. **Role tables.** ASP.NET Core Identity's role store is created but unused. **Assumption:** keep
    the tables rather than write a custom store to avoid seven empty ones.
12. **Legacy slices under the new session requirement.** **Assumption:** they require authentication
    and still serve the global dataset to any authenticated user, until the vault plan's claim flow
    assigns ownership. Recorded in ADR 0009 so it cannot be mistaken for per-user isolation that
    already works.
