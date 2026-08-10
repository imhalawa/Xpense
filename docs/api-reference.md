# API reference

Every route is under `/api/v1`. The generated OpenAPI document is the contract — see
[ADR 0003](../api/docs/adr/0003-generated-openapi-is-the-contract.md). With the stack running,
Swagger UI is served by the API on `http://localhost:4000`.

## Conventions

- **Money** is always a pair: `{ "minorUnits": 1250, "currency": "EUR" }`.
- **Errors** are `application/problem+json`.
- **Reads** need the `xpense.session` cookie. **Mutations** additionally need the
  `X-Xpense-Antiforgery` header paired with the `xpense.antiforgery` cookie.
- **Private resources return 404, never 403**, when you are not allowed to know they exist.
- Date ranges are half-open: `from` inclusive, `to` exclusive.

## Authentication

| Method | Route | Anonymous |
| --- | --- | --- |
| GET | `/auth/antiforgery` | yes |
| GET | `/auth/me` | no |
| POST | `/auth/register/options` | yes |
| POST | `/auth/register` | yes |
| POST | `/auth/passkey/options` | yes |
| POST | `/auth/passkey/sign-in` | yes |
| POST | `/auth/recovery/sign-in` | yes |
| POST | `/auth/password/sign-in` | yes |
| POST | `/auth/logout` | no |

`/auth/register` takes the pending registration id, the passkey credential, the encryption public
key, the encrypted private key and its nonce, and a vault wrapper.

The vault wrapper carries its **own identifier, chosen by the browser**. That identifier is bound
into the master key's associated data before the ciphertext is sent, so a server-assigned id would
produce a wrapper that no later sign-in could ever unwrap.

The credential JSON must include `clientExtensionResults`, since the API deserializes a full
WebAuthn credential. The client sends it with the PRF result removed — that value derives the vault
key and must never reach the server.

## Passkey management

| Method | Route |
| --- | --- |
| POST | `/users/me/passkeys/options` |
| POST | `/users/me/passkeys` |
| GET | `/users/me/passkeys` |
| DELETE | `/users/me/passkeys/{credentialId}` |

## Accounts

| Method | Route |
| --- | --- |
| POST | `/accounts` |
| GET | `/accounts` |
| GET | `/accounts/{accountNumber}` |
| PUT | `/accounts/{accountNumber}` |
| DELETE | `/accounts/{accountNumber}` |

Routes key on the public account number, not the database id. `PUT` accepts only the label and the
default flag — the currency is fixed at creation.

## Transactions

| Method | Route |
| --- | --- |
| POST | `/transactions` |
| GET | `/transactions` |
| GET | `/transactions/{id}` |
| PUT | `/transactions/{id}` |
| DELETE | `/transactions/{id}` |

`GET /transactions` accepts `page` (default 1), `pageSize` (default 25), `from` and `to`, and
returns `{ items, page, pageSize, totalItems, totalPages }` newest first.

## Categories and the necessity scale

| Method | Route |
| --- | --- |
| POST | `/categories` |
| GET | `/categories` |
| GET | `/categories/{id}` |
| PUT | `/categories/{id}` |
| DELETE | `/categories/{id}` |
| GET | `/priorities` |

`/priorities` is read-only — the necessity scale ships in a migration.

## Tags

| Method | Route |
| --- | --- |
| POST | `/tags` · GET `/tags` |
| GET | `/tags/{id}` · PUT `/tags/{id}` · DELETE `/tags/{id}` |

## Merchants

| Method | Route |
| --- | --- |
| POST | `/merchants` |
| GET | `/merchants` — accepts `search` and `limit` (default 20, max 100) |
| GET | `/merchants/{id}` · PUT `/merchants/{id}` · DELETE `/merchants/{id}` |

## Budgets

| Method | Route |
| --- | --- |
| POST | `/budgets` |
| GET | `/budgets` — accepts `on` to pick the measuring date |
| GET | `/budgets/{id}` · PUT `/budgets/{id}` · DELETE `/budgets/{id}` |

Each budget reports a period with `spent`, `remaining`, `exceeded` and `uncounted[]`. `PUT` does not
accept a category.

## Analytics

| Method | Route |
| --- | --- |
| GET | `/analytics/spending/by-category` |

One endpoint, and it reports **today** only. Results are grouped by category *and* currency, so a
category spent in two currencies appears twice and the totals hold one entry per currency.

## Notifications

| Method | Route |
| --- | --- |
| GET | `/notifications` — `page`, `pageSize` (max 100), `unread` |
| GET | `/notifications/unread-count` |
| PATCH | `/notifications/{id}/read` |
| POST | `/notifications/read-all` |

## Groups and sharing

| Method | Route |
| --- | --- |
| POST | `/groups` · GET `/groups` |
| GET | `/groups/{groupId}` · PUT `/groups/{groupId}` · DELETE `/groups/{groupId}` |
| POST | `/groups/{groupId}/leave` |
| POST | `/groups/{groupId}/ownership` |
| GET | `/groups/{groupId}/members` · DELETE `/groups/{groupId}/members/{userId}` |
| POST | `/groups/{groupId}/grants` · GET `/groups/{groupId}/grants` |
| DELETE | `/groups/{groupId}/grants/{grantId}` |

Group names are sent and stored as ciphertext with a nonce.

## Invitations

| Method | Route |
| --- | --- |
| POST | `/invitations` · GET `/invitations` |
| GET | `/invitations/{token}` |
| GET | `/invitations/{invitationId}/approval-context` |
| POST | `/invitations/{invitationId}/approve` |
| POST | `/invitations/accept` |
| DELETE | `/invitations/{invitationId}` |

## Encrypted sync

| Method | Route |
| --- | --- |
| GET | `/sync/changes` |
| POST | `/sync/records` |
| PUT | `/sync/records/{id}` |
| DELETE | `/sync/records/{id}` |
| POST | `/sync/records/{id}/envelopes` |
| DELETE | `/sync/records/{id}/envelopes/{groupId}` |

Batches are capped at 100 records; ciphertext at 65536 bytes, envelopes and nonces at 4096.
Creates are idempotent; updates use optimistic concurrency and answer **409** with the current
ciphertext when a revision is stale.

## Legacy claim

| Method | Route |
| --- | --- |
| GET | `/claim/status` |
| POST | `/claim/start` |
| POST | `/claim/dataset` |
| POST | `/claim/complete` |

`/claim/status` answers `legacy`, `claiming` or `encrypted`, and the client uses it to decide which
storage mode to run in.

## Health

`GET /health` sits outside the feature slices — the one deliberate exception, because it is
infrastructure rather than a feature.
