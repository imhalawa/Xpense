# API v1 Contract Reset

## Goal

Replace the early singular, action-based HTTP API with a stable, MCP-friendly
resource API for a personal-finance product. This is a hard cutover: legacy
routes are removed rather than supported in parallel.

## Route and resource conventions

All endpoints use the `/api/v1` prefix and plural nouns. Successful responses return
the resource directly. Create operations return `201 Created` with a `Location`
header; deletion returns `204 No Content`; failures use RFC 7807 problem
details.

Resource URLs use the database-backed `id`, **except accounts**, which are addressed
by `accountNumber`. An account is the one resource a user names out loud, including
somebody else's, so it gets a public identifier and its database key is not exposed
at all. See [ADR 0002](../adr/0002-account-number-is-the-public-identifier.md).

`GET` collection responses that support paging return:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 25,
  "totalItems": 0,
  "totalPages": 0
}
```

Money crosses the boundary as `{ "minorUnits": 1234, "currency": "EUR" }` — not
"cents", which is true of EUR and USD and wrong for the first currency without them.

Every timestamp is an ISO 8601 UTC string with an `At` suffix: `createdAt`,
`updatedAt`, `occurredAt`. `occurredAt` is when the money moved, supplied by the
client; `createdAt` is when Xpense wrote the row and is never client-supplied.

Public request/response DTOs never expose service-layer command or entity types.

## Resources

* `GET|POST /api/v1/accounts`, `GET|PUT|DELETE /api/v1/accounts/{accountNumber}`
* `GET|POST /api/v1/categories`, `GET|PUT|DELETE /api/v1/categories/{id}`
* `GET|POST /api/v1/tags`, `GET|PUT|DELETE /api/v1/tags/{id}`
* `GET /api/v1/merchants`
* `GET|POST /api/v1/transactions`, `GET /api/v1/transactions/{id}`
* `GET /api/v1/analytics/spending/by-category`

## Transactions

One resource records all money movement. There is no `/transfers`: a transfer is a
transaction that names two accounts. The request carries no `type` field, because
which sides it names already says the kind:

| `kind` (response) | `sourceAccountNumber` | `destinationAccountNumber` |
| ----------------- | --------------------- | -------------------------- |
| `income`          | absent                | an account                 |
| `expense`         | an account            | absent                     |
| `transfer`        | an account            | another account            |

An absent side means the money crossed the system boundary, and `merchant` names who
was on that side. At least one side is required.

`categoryId` and `merchant` are **required** when exactly one side is an account and
**must be absent** when both are: a transfer between your own accounts has no shop
and no spending class. `kind` is derived from the stored columns rather than stored,
so a transaction cannot contradict itself.

Naming no account at all is a validation error. The default account is advisory —
something a client offers first — not a server-side fallback, because with no `type`
field there is nothing to say which side a fallback would stand in for.

## Testing

Tests are NUnit. Unit tests exercise domain invariants and input validation without
HTTP or persistence infrastructure. Integration tests use `WebApiTestFactory` and a
per-test Postgres database, cloned from a migrated template that `PostgresFixture`
builds once per run, so integration tests neither require nor modify a developer
database.

Every endpoint change follows red-green-refactor. Integration tests verify the
response/status contracts, validation failures, pagination, and that legacy
routes return `404`.

## Scope boundaries

This change does not add receipt OCR, an MCP server, bank connections,
authentication, workspace tenancy, or web-client work. It creates the HTTP
contract and test foundation those capabilities will later use.

The generated OpenAPI document at `/swagger/v1/swagger.json` is the machine-readable
description of this contract; there is no hand-maintained copy. See
[ADR 0003](../adr/0003-generated-openapi-is-the-contract.md).
