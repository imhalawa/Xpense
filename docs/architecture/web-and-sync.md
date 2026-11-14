# Web and sync

## Local data

The client presents financial data through a projection. The local projection stores records in IndexedDB and uses a durable outbox to synchronize changes. A lifecycle coordinator manages sync as connectivity and client state change.

## Sync

- Sync payloads are plaintext JSON on the server.
- Creates use idempotency keys; updates use revisions for optimistic concurrency.
- A conflict returns the current record for the client to resolve.
- Deletions use tombstones so they can propagate to other clients.

See the generated [API contract](../development/api-reference.md) for limits and request shapes.

## Planned removal and current limitation

The remaining client-side record encryption, vault, and legacy claim code is planned for removal. Passkey authentication is separate from this planned removal.

The client currently requests `/api/v1/claim/status` to choose a projection, but the API no longer exposes that endpoint. The unlock gate cannot select a projection against the current API. The old claim client also targets removed endpoints.

## Notifications

The local projection and the server's financial tables are distinct paths. Client budget alerts are derived from locally projected budgets; the server worker uses its own events and financial data. See [notifications](../features/notifications.md) and [security](security.md).
