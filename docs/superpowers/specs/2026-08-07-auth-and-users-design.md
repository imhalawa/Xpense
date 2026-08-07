# Xpense authentication and users

Date: 2026-08-07
Status: agreed in principle, decisions below were made without review — see "Decisions taken alone"
Scope: a `User` concept, authentication, per-user data scoping, and per-user AI provider tokens.
Consumed by: `2026-08-07-data-entry-design.md`, which needs a per-user token for its AI adapters.

## Why

There is no user in this system today. Verified, not assumed: `Xpense.Domain/Entities` contains
Account, BaseEntity, Budget, Category, EventRecord, Merchant, Notification, Priority, Tag and
Transaction. No `User`. A search across the API, domain and persistence projects for
authentication, authorization, identity, JWT, user-id or tenant wiring returns only Postgres
`IdentityByDefaultColumn` auto-increment annotations, which are unrelated.

Every account, transaction, budget and category is global. Nothing distinguishes whose data it is.

This became necessary because the AI adapters store a provider token per user, and because
per-user tokens are meaningless without users.

## Decisions taken alone

These were decided without review, because the work was authorised for an unattended session.
Each is reversible; none is buried.

| Decision | Choice | Reasoning |
|---|---|---|
| Identity | Local email and password | OAuth requires a registered redirect URL. Xpense has no domain and no host |
| Password hashing | `PasswordHasher<T>` from `Microsoft.Extensions.Identity.Core` | Correct, maintained, iteration-count managed for us, without adopting full ASP.NET Core Identity and its table set |
| Session | HttpOnly cookie, `SameSite=Lax`, `Secure` when served over TLS | Same-origin app. Credentials never touch JavaScript. A JWT in `localStorage` is readable by any script on the page and is strictly weaker here |
| Registration | First run creates the owner. Further registration disabled unless explicitly enabled | It is one person's financial data on their own machine |
| Scoping | EF Core global query filters on `UserId` | One filter per entity, not a `Where` in every query. A forgotten `Where` is a data leak; a global filter cannot be forgotten |
| AI token | Column on `User`, encrypted with ASP.NET Core Data Protection, **write-only** | No endpoint returns it. Reads expose only `configured: true/false` |
| Existing rows | Expand/contract in three migrations | The project's standing rule: migrations run in CD, never on startup, and expand/contract is mandatory |

## The User entity

```
User : BaseEntity
  Email                  required, unique, stored lowercase
  PasswordHash           required
  DisplayName            optional
  AiProviderToken        optional, encrypted at rest, never serialised outward
  AiProvider             optional
```

`AiProviderToken` must never appear in any response DTO. The setting endpoint accepts it; the read
endpoint returns whether one is present. Writing this rule down is not enough — a test asserts
that no serialised user payload contains the token.

## What is per-user and what is shared

| Entity | Scope | Why |
|---|---|---|
| Account, Transaction, Budget, Notification | per-user | Obviously personal |
| Category, Merchant, Tag | per-user | They are the user's own taxonomy. Two users would otherwise fight over one category list |
| EventRecord | per-user | The notification worker derives per-user notifications from it and must know whose event it is |
| **Priority** | **global** | Seeded reference data — Extreme, High, Medium, Low, None. Shared vocabulary, not user content |

Priority staying global matters: it is referenced by `Category.PriorityId` as a required relation,
and the seeded rows have fixed ids that migrations depend on.

## Authentication surface

| Endpoint | Purpose |
|---|---|
| `POST /api/v1/auth/register` | First-run owner creation; refuses when registration is closed |
| `POST /api/v1/auth/login` | Sets the session cookie |
| `POST /api/v1/auth/logout` | Clears it |
| `GET /api/v1/auth/me` | Current user, without the token, plus `aiConfigured` |
| `PUT /api/v1/users/me/ai-token` | Sets or clears the provider token. Write-only |

Failed login returns one message for both an unknown email and a wrong password, so the response
does not reveal which addresses exist.

## Per-user scoping

A `CurrentUser` accessor reads the authenticated user id from the request context. The DbContext
applies a global query filter per per-user entity. Two consequences:

- Every existing query narrows automatically. No feature slice changes.
- Writes must set `UserId` explicitly. A global filter does not populate it on insert, so this is
  the one place a mistake is still possible, and it needs a test per write path.

The notification worker (`Xpense.Notifications`) runs outside a request and therefore has no
`CurrentUser`. It must read the owning user from the event record and act as that user explicitly,
rather than relying on the filter. This is the most likely place for a cross-user bug and deserves
its own tests.

## Migration plan — expand/contract, three steps

1. **Expand.** Add the `Users` table. Add a nullable `UserId` to every per-user entity. Deploy.
   Nothing breaks: existing rows carry null and existing queries ignore the column.
2. **Backfill.** Create the owner from configuration, assign every existing row to that owner,
   verify no nulls remain. This is the only step that touches existing data.
3. **Contract.** Make `UserId` non-nullable, add foreign keys and the per-user indexes. Deploy.

**Step 2 will not be run automatically.** It is authored, tested against a scratch database, and
left unapplied for review. Rewriting the ownership of every row in a financial database is the one
irreversible action in this backlog, and the standing rule is that migrations run in CD, never on
application startup.

## Testing

Written test-first:

- Password hashing round-trips; a wrong password fails; the hash is never equal to the input.
- Login sets an HttpOnly cookie; the cookie is absent after logout.
- Unknown email and wrong password produce identical responses.
- Registration is refused once an owner exists and registration is closed.
- A user cannot read another user's transactions, accounts, budgets, categories, merchants, tags or
  notifications. One test per entity — this is the suite that matters most.
- Writes stamp `UserId`. One test per write path.
- The notification worker attributes a notification to the event's owner, not to a default user.
- No serialised user payload contains `AiProviderToken`.
- The backfill migration leaves zero null `UserId` values, run against a scratch database seeded
  with pre-migration rows.

## Risks

- **This is the riskiest work in the backlog.** Every other spec adds screens or endpoints. This one
  rewrites the ownership of existing data and adds a filter that, if wrong in one place, shows one
  person another person's finances.
- **Multi-user without a host is unusual.** Xpense today has no VPS, no domain and no TLS. Users
  and auth only pay off once it is reachable by more than one person, and that is a hosting
  decision this spec does not make. Cookies marked `Secure` require TLS, so the cookie policy has
  to be environment-aware until then.
- **Scope.** This spec is larger than the three UI specs combined in risk, though not in visible
  output. It produces no new screens beyond login and an AI-token field in Manage.

## Out of scope

- Hosting, TLS, domains, deployment.
- Password reset by email — there is no mail infrastructure. An owner who forgets the password
  resets it through configuration.
- Roles and permissions. One user owns their data; there is no sharing model.
- OAuth or social login.
