# Xpense zero-knowledge vault, passkeys, and recovery

Date: 2026-08-08
Status: written for user review
Scope: browser-side encryption, key hierarchy, encrypted sync, recovery, revocation, and legacy claim
Consumed by: `2026-08-08-identity-groups-and-sharing-design.md` and
`2026-08-08-web-shell-and-filtering-design.md`

## Outcome and threat model

Xpense protects private financial content from inspection of the database and its backups by an
honest-but-curious server operator. The API stores and synchronises ciphertext but cannot decrypt
amounts, balances, account numbers, transaction details, labels, budgets, merchants, categories, or
tags.

This phase does not protect against an operator who changes the JavaScript served to the user, a
compromised browser or extension, malware on the device, screenshots, or a person who was allowed
to view data and saved it. A browser application cannot defend against its own origin deliberately
serving hostile code. Native clients may reduce that risk later, but are not part of this work.

## Server-visible boundary

The server may see:

- User email, account state, passkey public data, sessions, and delivery status.
- Record UUID, owner UUID, record type, encrypted byte length, revision, tombstone, and server
  timestamps.
- Opaque parent-resource identifiers required to enforce Editor permissions.
- Group membership, roles, invitations, resource grants, and Viewer/Editor permission.
- Sync cursor and device-independent idempotency keys.

The server must not receive or log:

- Decrypted financial payloads or display labels.
- User master keys, record keys, group keys, recovery secrets, or exported private keys.
- WebAuthn PRF results.
- Client-side search terms or selected encrypted taxonomy values.

The visible relationships leak that records belong together and when they changed. This is accepted
metadata leakage for authorization and sync. Currency and transaction occurrence time stay inside
the encrypted payload because the server does not need them.

## Cryptographic protocol

The protocol is versioned independently from the API. Version 1 uses:

- 256-bit random user master keys, group keys, and record data-encryption keys.
- AES-256-GCM with a fresh random 96-bit nonce for payload and symmetric key-envelope encryption.
- HKDF-SHA-256 for domain-separated key derivation.
- The WebAuthn Level 3 PRF extension for passkey wrapping keys.
- Argon2id for an optional recovery-password wrapper.
- HPKE base mode from RFC 9180 with X25519, HKDF-SHA-256, and AES-256-GCM for user-to-user group-key
  envelopes.

Implementations use platform cryptography or a maintained implementation of the named standard;
they do not create a new cipher or alter the algorithms. Web Crypto Level 2 defines AES-GCM, HKDF,
and X25519 operations and describes storing `CryptoKey` objects in IndexedDB. See the
[Web Cryptography specification](https://www.w3.org/TR/webcrypto/). HPKE follows
[RFC 9180](https://www.rfc-editor.org/rfc/rfc9180), not an ad-hoc ECDH envelope.

Every encrypted object includes an algorithm version. Authenticated additional data contains the
protocol version, record UUID, record type, owner UUID, and revision. Changing any of those fields
therefore invalidates decryption. A nonce is never reused with the same key.

## Key hierarchy

### Personal vault

Each user has one random User Master Key. It encrypts:

- The user's exported X25519 private encryption key.
- Every private record-key envelope owned by that user.
- User profile and preference records.

The public X25519 key is server-visible so another unlocked client can prepare a group-key envelope
for that user. The private key is exported only inside the crypto worker during initial creation,
immediately encrypted under the User Master Key, and thereafter imported as non-extractable when
the vault unlocks.

### Records

Every financial record has its own random Record Key. The payload is encrypted under that key. The
same Record Key may have several envelopes:

- A personal envelope encrypted under the owner's User Master Key.
- A group envelope encrypted under each group key receiving a resource grant.

This lets Xpense add or remove a group without exposing the owner's master key. Account sharing also
adds group envelopes to the transactions and taxonomy records needed to render that account.

### Groups

Each group has one random Group Key. Every active member receives a separate HPKE envelope of that
key to their public encryption key. Group membership alone supplies the group key; resource grants
still control which ciphertext the API will return.

After membership revocation, the next unlocked Owner client creates a new Group Key and new member
envelopes. Record keys for resources previously visible to the removed member are also rotated
before further edits are accepted. Old data cannot be made unseen and the UI says so.

## Passkey unlock

WebAuthn authenticates the account and its PRF output unlocks the vault. For each credential:

1. The server stores a random credential wrapping salt and the User Master Key ciphertext.
2. During an assertion, the browser evaluates the credential PRF with that salt.
3. The client derives a wrapping key using HKDF-SHA-256 with info
   `Xpense passkey vault wrap v1`.
4. The client decrypts the User Master Key envelope locally.
5. The PRF output and derived key are cleared after the master key is imported into the worker.

The PRF extension is optional and may be ignored by a browser or authenticator. Registration checks
its `enabled` result. If creation does not return a PRF value, the client immediately performs an
assertion to obtain it. Unsupported credentials may authenticate but cannot be the only vault
unlock method.

The WebAuthn specification explicitly describes PRF output as suitable for symmetric encryption
and warns that `PublicKeyCredential.toJSON()` includes the PRF result. Xpense therefore uses a
dedicated serializer that removes client extension results before sending the assertion to the
API. See the [WebAuthn Level 3 PRF extension](https://www.w3.org/TR/webauthn-3/#prf-extension).

## Recovery without server decryption

There is no operator master key and no support reset that can decrypt a vault. Recovery uses, in
order:

1. A synced passkey whose PRF produces the existing credential wrapping key.
2. Any already-unlocked device, which can add a new passkey wrapper.
3. A recovery password, when the user chose to configure one.
4. A downloadable recovery file containing independent random authentication and vault secrets.

The recovery password is submitted over TLS to ASP.NET Core Identity for fallback authentication
and independently derives a local wrapping key with Argon2id using a random 128-bit salt, 64 MiB of
memory, three iterations, four lanes, and a 256-bit result. It must contain at least 14 characters.
These are RFC 9106's second recommended settings for memory-constrained environments. Parameters
are stored beside the ciphertext so a future upgrade can add a new wrapper without changing the
master key. The server may observe the password during the sign-in request but database and backup
inspection reveals only its password hash; malicious runtime inspection is outside the agreed
threat model. See
[RFC 9106](https://www.rfc-editor.org/rfc/rfc9106).

The recovery file is optional but offered once after registration and again from an unlocked vault.
It contains no email or financial data. It holds a random 256-bit authentication token and a
different random 256-bit vault secret. The server stores only the authentication token's SHA-256
hash; the vault secret wraps the User Master Key and never crosses the network. Recovery-file login
submits and consumes the authentication token, while the client uses the separate vault secret to
unlock. An unlocked client immediately issues a replacement recovery file, making each file
single-use for authentication. A recovery password or file can be replaced only while the vault is
already unlocked.

If every usable passkey, unlocked device, recovery password, and recovery file is lost, the data is
unrecoverable. The product states this plainly during setup. A server-side bypass would contradict
the agreed database-privacy goal.

## Browser vault boundary

- IndexedDB stores ciphertext, key envelopes, sync metadata, and local indexes made from opaque
  record UUIDs.
- The unwrapped User Master Key, Group Keys, Record Keys, and decrypted projections exist only in a
  dedicated Web Worker's memory.
- The master key is never persisted as an unlocked `CryptoKey`. Reloading the page requires another
  unlock.
- The service worker may cache application assets and encrypted responses but never receives keys.
- Manual lock is always available. The vault also locks after 15 minutes without interaction and
  after 15 minutes continuously hidden.
- Lock terminates the crypto worker, clears decrypted React state, and removes sensitive error
  details from the screen.
- The production web app uses a restrictive Content Security Policy, no third-party runtime
  scripts, no inline script, Trusted Types where supported, and bundled dependencies.

Decrypted view models necessarily cross from the worker to React when rendered. This limits casual
exposure and reduces accidental key handling; it does not make browser JavaScript equivalent to a
native secure enclave.

## Encrypted record and sync contract

The plaintext resource endpoints cannot remain authoritative after migration. They are replaced by
a blind sync surface:

| Endpoint | Purpose |
|---|---|
| `GET /api/v1/sync/changes?cursor=<cursor>` | Return accessible ciphertext changes and tombstones |
| `POST /api/v1/sync/records` | Batch-create records with idempotency keys |
| `PUT /api/v1/sync/records/{id}` | Replace ciphertext at an expected revision |
| `DELETE /api/v1/sync/records/{id}` | Create an authorised tombstone |
| `POST /api/v1/sync/records/{id}/envelopes` | Add a group grant and key envelope |
| `DELETE /api/v1/sync/records/{id}/envelopes/{groupId}` | Revoke a group envelope |

An encrypted record contains UUID, type, owner, opaque authorization-resource ids, revision,
protocol version, nonce, ciphertext, envelopes, tombstone state, and server timestamps. The API
rejects unknown types, oversized values, invalid grant targets, stale revisions, and operations
outside the caller's permission. Nonce uniqueness is a client cryptography invariant; the server
cannot prove it without knowing which encrypted envelopes contain the same key.

Sync uses an opaque monotonically increasing cursor. Creates are idempotent. Updates use optimistic
concurrency; a stale update returns 409 and the latest ciphertext. Because the server cannot merge
encrypted fields, the unlocked client decrypts both revisions and asks the user to keep one or
reapply their change. Deletes remain soft through tombstones.

The client downloads accessible changes, verifies authenticated data during decryption, updates its
local projection, and then performs category, merchant, tag, account, amount, and date filtering
locally. Corrupt or unauthentic ciphertext is quarantined and never partially rendered.

## Domain logic and notifications

The server can no longer calculate balances, validate currency matches, calculate budgets, produce
analytics, or inspect notification rules. Equivalent domain rules move into a small client domain
module shared by forms, projections, and tests. The API still enforces authorization, revisions,
sizes, and record graph constraints.

Budget alerts and analytics run only while an unlocked trusted client is open. A generated
notification is encrypted and synced like any other record. There are no server-generated budget
emails in this phase. This limitation is deliberate and visible in product copy.

## One-time legacy claim

Existing global plaintext data is not assigned to the first registrant. It is claimed by a
designated test user through an operator-authorised maintenance flow:

1. An expand migration adds identity, encrypted-record, envelope, grant, invitation, and sync tables
   without changing existing plaintext rows.
2. The operator creates the test user normally, places the installation in claim mode, and issues a
   single-use claim token with a 30-minute expiry.
3. The authenticated test user's unlocked browser presents that token and downloads the existing
   plaintext dataset once. Normal financial writes are blocked during claim mode.
4. The browser validates domain rules, encrypts every record, uploads the encrypted set, and checks
   record counts plus a canonical manifest hash.
5. The API marks the token consumed only after encrypted verification succeeds. Failure leaves the
   plaintext source untouched and the installation in claim mode.
6. The operator reviews the verification report and runs a separate contract migration that removes
   plaintext financial columns/tables and disables the legacy endpoints.

No migration or startup seeder creates users or rewrites ownership. Migrations remain an explicit
deployment step under ADR 0004, and the destructive contract step is never automatic.

## Failure handling

- Unsupported PRF: offer recovery-password setup; do not create a passkey-only vault that cannot
  unlock.
- Valid sign-in but failed unwrap: stay authenticated and locked; offer another wrapper.
- Ciphertext authentication failure: quarantine the record, preserve the last valid local revision,
  and show a sync-integrity error.
- Offline mutation: queue encrypted operations locally and retain their idempotency keys.
- Revision conflict: do not overwrite silently.
- Interrupted grant or revocation rotation: mark the resource `Key update required` and refuse new
  shared edits until the client completes the operation.
- Lost all recovery methods: allow account deletion, but never pretend support can restore data.

## Testing and security gates

- Known-answer tests for every algorithm, envelope type, AAD layout, and protocol version.
- Round-trip and tamper tests for payloads, personal envelopes, group envelopes, passkey wrappers,
  password wrappers, and recovery files.
- WebDriver virtual-authenticator tests for supported PRF, ignored PRF, registration without an
  immediate PRF result, replay, and multiple credentials.
- A network test proves that PRF results and unwrapped keys never appear in requests, logs, error
  telemetry, or persisted browser storage.
- Database inspection tests assert that prohibited plaintext fields and representative values do
  not exist after the contract migration.
- Two-user and two-group fixtures prove private records remain undecryptable without the correct
  envelope.
- Revocation tests prove old ciphertext may remain readable but new revisions use rotated keys.
- Sync tests cover idempotency, cursor order, tombstones, offline replay, conflicts, corrupt
  ciphertext, and cross-user authorization.
- A claim rehearsal runs on a scratch copy, verifies counts and manifest hash, and proves failure is
  restartable before the production contract migration is approved.
- A cryptography-focused external review is required before removing the plaintext schema. The
  contract migration is the release gate, not an early implementation step.

## Acceptance criteria

- A database and backup contain no readable financial content after the contract migration.
- A normal server session without client keys cannot decrypt any financial record.
- Passkey PRF output unlocks locally and never crosses the network.
- At least one non-operator recovery route is configured or explicitly declined with a warning.
- Group sharing uses envelopes; it never copies a user's master key to the server or another user.
- Revocation blocks future access and rotates future ciphertext while acknowledging saved history.
- Transaction filters, balances, budgets, and analytics work from the unlocked local projection.
- The legacy dataset can be claimed once by the designated test user and remains recoverable until
  the operator explicitly removes plaintext storage.

## Out of scope

- Protection from malicious JavaScript served by the operator.
- Native clients and hardware-backed native key storage.
- Background server analytics, budget alerts, or financial email content.
- Searchable encryption or server-side filtering of private fields.
- Operator-assisted decryption or key escrow.
