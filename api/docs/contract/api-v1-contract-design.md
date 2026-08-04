# API v1 Contract Reset

## Goal

Replace the early singular, action-based HTTP API with a stable, MCP-friendly
resource API for a personal-finance product. This is a hard cutover: legacy
routes are removed rather than supported in parallel.

## Route and resource conventions

All endpoints use the `/api/v1` prefix and plural nouns. Resource URLs use the
database-backed `id`, not an account number. Successful responses return the
resource directly. Create operations return `201 Created` with a `Location`
header; deletion returns `204 No Content`; failures use RFC 7807 problem
details.

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

Money crosses the boundary as `{ "cents": 1234, "currency": "EUR" }`.
Dates use ISO 8601 UTC timestamps. Public request/response DTOs never expose
service-layer command or entity types.

## Resources

* `GET|POST /api/v1/accounts`, `GET|PUT|DELETE /api/v1/accounts/{id}`
* `GET|POST /api/v1/categories`, `GET|PUT|DELETE /api/v1/categories/{id}`
* `GET|POST /api/v1/tags`, `GET|PUT|DELETE /api/v1/tags/{id}`
* `GET /api/v1/merchants`
* `GET|POST /api/v1/transactions`, `GET /api/v1/transactions/{id}`
* `POST /api/v1/transfers`
* `GET /api/v1/analytics/spending/by-category`

Transactions use `type` (`income` or `expense`) and a single request shape.
Transfers are deliberately separate because they affect both source and target
accounts.

## Testing

Tests are NUnit. Unit tests exercise input validation and request-to-command
mapping without HTTP infrastructure. Integration tests use `WebApiTestFactory`
and a per-test SQLite in-memory database, replacing the production SQL Server
registration. The factory controls schema creation and seed data, so integration
tests neither require nor modify a developer database.

Every endpoint change follows red-green-refactor. Integration tests verify the
new response/status contracts, validation failures, pagination, and that legacy
routes return `404`.

## Scope boundaries

This change does not add receipt OCR, an MCP server, bank connections,
authentication, workspace tenancy, or web-client work. It creates the HTTP
contract and test foundation those capabilities will later use.
