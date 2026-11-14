---
status: accepted
date: 2026-08-11
---

# Local-first without end-to-end encryption

## Decision

Keep local storage and the durable sync outbox. Store sync payloads as plaintext on the server. Retain passkey authentication, record IDs, revisions, tombstones, idempotency keys, and the changes cursor.

## Context

Local-first supports access and writes without a connection. End-to-end encryption limits what the server can read. The latter prevented server-side budget processing and analytics, duplicated domain logic, and added vault and migration complexity for a single maintainer. The operator is trusted; database access controls, encryption at rest, and encrypted backups protect stored data.

## Alternatives considered

- **Keep end-to-end encryption:** rejected because of its ongoing complexity and effect on server-side features.
- **Make encryption optional:** rejected because both paths and their features would need maintenance and tests.
- **Move server features into the client:** rejected because some events exist only on the server and it preserves duplicated domain logic.
- **Encrypt selected columns with a server-held key:** rejected because it adds key management without the intended benefit over storage encryption.

## Consequences

The server can read financial data. The decision called for removing the encrypted vault and claim flow, with no production data migration at that time. Budget alerts may be derived on the client; server-known events remain server-side. Stale sync updates continue to use conflict resolution. Optional end-to-end encryption would require a new product decision and feature design.

The decision describes the intended transition. The current client still contains claim and vault code, and its projection selection calls a removed API endpoint; see [web and sync](../architecture/web-and-sync.md).

## Sources
- [Firefly III — Security](https://docs.firefly-iii.org/explanation/more-information/security/) — the
  database is not encrypted and its values are readable; disk encryption is the recommendation
- [Firefly III — End-to-End (Client-Side) Encryption, issue #2039](https://github.com/firefly-iii/firefly-iii/issues/2039)
- [Actual Budget — Syncing Across Devices](https://actualbudget.org/docs/getting-started/sync/) — end-to-end
  encryption is optional, per budget file, and off unless you turn it on
- [Actual Budget — Connecting Your Bank](https://actualbudget.org/docs/advanced/bank-sync/) — bank sync
  tokens live on the server and are outside the encryption, because the server must use them
