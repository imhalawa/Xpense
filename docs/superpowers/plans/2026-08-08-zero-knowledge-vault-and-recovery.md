# Zero-Knowledge Vault and Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Phase 5 is gated and must not be executed by an autonomous agent.** Read it before you start so you know where the line is.

**Goal:** The browser encrypts every financial payload before it leaves the device. The API stores and synchronises ciphertext it cannot read. Passkey PRF unlocks the vault, a recovery password and a recovery file are the fallbacks, and balances, budgets and analytics are calculated on the client.

**Architecture:** One versioned crypto module built on Web Crypto (AES-256-GCM, HKDF-SHA-256, X25519), `@noble/hashes/argon2.js` for Argon2id and `@hpke/core` for RFC 9180 envelopes. All unwrapped keys live inside one dedicated Web Worker and never cross back to the main thread. IndexedDB holds ciphertext only. The API gains six additive `sync` vertical slices over new tables that sit **beside** the existing plaintext tables — nothing is dropped in this plan.

**Tech Stack:** React 19.2.8, Fluent UI 9, Vite 8, TypeScript 7 (`strict`), Vitest 4 + jsdom, Node 26. API: .NET 10, EF Core 10, Npgsql, NUnit + FluentAssertions 6 + Testcontainers.

## Global Constraints

- Source spec: `docs/superpowers/specs/2026-08-08-zero-knowledge-vault-and-recovery-design.md`.
- Sibling spec for identity vocabulary: `docs/superpowers/specs/2026-08-08-identity-groups-and-sharing-design.md`.
- **No comments in any file.** The only exception is `<summary>` on C# `*Request` and `*Response` records, because Swagger renders them into the OpenAPI document that ADR 0003 makes authoritative.
- Spell names out. `cancellationToken`, not `ct`. `dbContext`, not `db`. `recordKey`, not `rk`. `additionalData`, not `aad` — except where it names a standard field, and then it is `additionalAuthenticatedData`.
- **Constants first.** `const` and `static readonly` at the top of the type; TypeScript module constants at the top of the file.
- Validation messages are prose: "The ciphertext is larger than a record may be", never "ciphertext exceeds MaxCiphertextBytes".
- **Migrations never run at startup** (ADR 0004). Every migration in this plan is applied by the `migrations` container or `dotnet ef database update` by hand.
- **All API schema work in Phases 0–4 is EXPAND-only.** New tables sit alongside `Accounts`, `Transactions`, `Categories`, `Merchants`, `Tags`, `Budgets`, `Notifications`. Nothing existing is dropped, renamed, or narrowed.
- **Never hand-roll a primitive.** No custom cipher, no ad-hoc ECDH envelope, no home-made KDF. If a standard algorithm is missing, stop and report.
- **A nonce is never reused with a key.** Every AES-GCM call takes a fresh 96-bit `crypto.getRandomValues` nonce.
- **No new `.csproj` in this plan.** New API code goes into `Xpense.Domain`, `Xpense.Persistence` and `Xpense.API`. If a later change does add a project, its `.csproj` must be added to **all three** build Dockerfiles — `api/docker/api/Dockerfile`, `api/docker/migrations/Dockerfile`, `api/docker/notifications/Dockerfile` — because each restores the whole solution. Miss one and all three builds fail.
- Do not add a `Co-Authored-By` trailer to commits.

## Prerequisite

The API slices in Phase 3 need an authenticated user UUID and the `Groups`, `GroupMemberships` tables from `2026-08-08-identity-groups-and-sharing-design.md`. That plan is not written yet. Task 16 defines the single seam this plan depends on — `ICurrentUser` returning the authenticated user's UUID — and a test double for it. When the identity plan lands, it supplies the real implementation reading the ASP.NET Core Identity cookie. **Do not build a second authentication system here.**

## Library choices

| Need | Package | Why |
|---|---|---|
| AES-256-GCM, HKDF-SHA-256, X25519, SHA-256, random bytes | Web Crypto (`crypto.subtle`) | Platform. Spec names it. Non-extractable `CryptoKey` is the whole point. |
| Argon2id | `@noble/hashes` `^2.3.0`, import `@noble/hashes/argon2.js` | Audited, zero-dependency, actively published (2026-08-06). Ships `argon2id` with `m`, `t`, `p` and `dkLen`. Pure JS, so it is slower than a WASM build; it runs in the crypto worker on a rare path, so the UI never blocks. **Ceiling:** if a real device measures over 3 seconds at 64 MiB / t=3, swap the implementation inside `web/src/crypto/argon2.ts` only — nothing else imports the library. |
| HPKE (RFC 9180) | `@hpke/core` `^1.9.0` | The hpke-js core. Exports exactly the spec's suite: `DhkemX25519HkdfSha256`, `HkdfSha256`, `Aes256Gcm`. One transitive dependency (`@hpke/common`), uses Web Crypto where the runtime has it. `@hpke/dhkem-x25519` is **not** needed — X25519 moved into core. |
| IndexedDB in tests | `fake-indexeddb` (dev) | jsdom has no IndexedDB. |
| Virtual authenticator + network gates | `@vitest/browser` + `playwright` (dev) | Keeps one test runner. Chromium's CDP `WebAuthn` domain gives the virtual authenticator with PRF support; Playwright's routing gives the request inspection the network gate needs. |

Rejected: `hash-wasm` for Argon2id — faster, but last published 2024-11-19 and the spec asks for a maintained implementation. Rejected: writing an ECDH+HKDF envelope by hand — the spec forbids it by name.

---

## Phase 0 — Crypto primitives

### Task 1: Crypto test harness and dependencies

**Files:**
- Modify: `web/package.json`
- Modify: `web/vitest.setup.ts`
- Create: `web/src/crypto/environment.test.ts`

jsdom exposes `crypto.getRandomValues` but not `crypto.subtle`, and has no IndexedDB. Both are shimmed in the existing setup file, next to the `matchMedia` and canvas stubs.

- [ ] **Step 1: Install**

```bash
cd web && npm install @noble/hashes@^2.3.0 @hpke/core@^1.9.0 && npm install --save-dev fake-indexeddb
```

- [ ] **Step 2: Write the failing test**

`environment.test.ts` asserts `crypto.subtle` exists, that `crypto.subtle.generateKey({ name: "X25519" }, ...)` resolves, that AES-GCM encrypt/decrypt round-trips, and that `indexedDB.open` resolves.

- [ ] **Step 3: Run it, confirm it fails.** `cd web && npx vitest run src/crypto/environment.test.ts`

- [ ] **Step 4: Implement.** In `vitest.setup.ts`, import `fake-indexeddb/auto`, and when `globalThis.crypto?.subtle` is missing install Node's `webcrypto` with `Object.defineProperty` — jsdom defines `crypto` as a getter, so plain assignment throws.

- [ ] **Step 5: Verify and commit.**

```bash
cd web && npx vitest run src/crypto/environment.test.ts && npm test
git add web/package.json web/package-lock.json web/vitest.setup.ts web/src/crypto/
git commit -m "test: give vitest web crypto and indexeddb"
```

The full `npm test` run matters here: the setup file is shared by all 268 existing tests.

---

### Task 2: Protocol version, algorithm identifiers, and the AAD layout

**Files:**
- Create: `web/src/crypto/protocol.ts`, `web/src/crypto/protocol.test.ts`

**Interfaces produced:**
- `PROTOCOL_VERSION = 1`
- `RecordType` — `"account" | "transaction" | "transfer" | "category" | "merchant" | "tag" | "budget" | "notification" | "userProfile" | "necessityScale"`
- `payloadAdditionalData({ recordId, recordType, ownerId, revision })`
- `envelopeAdditionalData({ recordId, ownerId, groupId })`
- `masterKeyAdditionalData({ userId, wrapperKind, wrapperId })`

The AAD is a canonical UTF-8 string with `|` separators, never JSON — JSON key order is not guaranteed and a re-serialised object would fail to decrypt:

```
v1|payload|<recordUuid>|<recordType>|<ownerUuid>|<revision>
v1|envelope|<recordUuid>|<ownerUuid>|<groupUuid or "personal">
v1|master|<userUuid>|<wrapperKind>|<wrapperId>
```

UUIDs are lowercase canonical form and record types are the literals above, so no field can contain the separator.

- [ ] **Step 1: Write the failing test.** Byte-for-byte known answers for all three layouts, one test per layout, plus a test that changing the revision changes the bytes and a test that an uppercase UUID normalises to lowercase.
- [ ] **Step 2: Run it, confirm it fails.** `cd web && npx vitest run src/crypto/protocol.test.ts`
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Verify and commit.** `cd web && npx vitest run src/crypto/protocol.test.ts`

---

### Task 3: Symmetric primitives — AES-256-GCM and HKDF-SHA-256

**Files:**
- Create: `web/src/crypto/primitives.ts`, `web/src/crypto/primitives.test.ts`

**Interfaces produced:**
- `generateSymmetricKey(): Promise<CryptoKey>` — 256-bit, `extractable: false`
- `importSymmetricKey(bytes, { extractable })`
- `encryptWithAdditionalData(key, plaintext, additionalData): Promise<{ nonce, ciphertext }>`
- `decryptWithAdditionalData(key, nonce, ciphertext, additionalData): Promise<Uint8Array>`
- `deriveWrappingKey(inputKeyMaterial, salt, info): Promise<CryptoKey>` — HKDF-SHA-256
- `randomBytes(length)`

- [ ] **Step 1: Write the failing tests.**
  - Known-answer: RFC 5869 test case 1 for HKDF-SHA-256 (exported extractable key compared to the RFC's OKM).
  - Known-answer: one NIST AES-GCM 256-bit vector with additional authenticated data.
  - Round-trip with AAD from Task 2.
  - **Tamper tests, one assertion each:** flip a ciphertext byte → rejects; flip a nonce byte → rejects; change the revision inside the AAD → rejects; truncate the tag → rejects.
  - Nonce is 12 bytes and two calls with the same key produce different nonces.
- [ ] **Step 2: Run them, confirm they fail.** `cd web && npx vitest run src/crypto/primitives.test.ts`
- [ ] **Step 3: Implement.** Thin wrappers over `crypto.subtle`. No branching on algorithm — version 1 is the only version and `protocol.ts` owns the constant.
- [ ] **Step 4: Verify and commit.** `cd web && npx vitest run src/crypto/protocol.test.ts src/crypto/primitives.test.ts`

---

### Task 4: Argon2id wrapper

**Files:**
- Create: `web/src/crypto/argon2.ts`, `web/src/crypto/argon2.test.ts`

Constants at the top, RFC 9106 second recommended settings as the spec states them: 64 MiB memory, 3 iterations, 4 lanes, 128-bit random salt, 256-bit output. Minimum password length 14.

**Interfaces produced:**
- `RECOVERY_PASSWORD_PARAMETERS` — the frozen parameter record persisted beside the ciphertext
- `deriveRecoveryKeyMaterial(password, salt, parameters): Promise<Uint8Array>`

- [ ] **Step 1: Write the failing tests.**
  - Known-answer: the RFC 9106 Argon2id test vector, exact bytes.
  - The exported parameter record equals `{ memoryKibibytes: 65536, iterations: 3, lanes: 4, outputBytes: 32 }`.
  - A stored parameter record with different values is honoured, so a future wrapper can upgrade without changing the master key.
  - A password shorter than 14 characters is rejected with a prose message.
- [ ] **Step 2: Run them, confirm they fail.** `cd web && npx vitest run src/crypto/argon2.test.ts`
- [ ] **Step 3: Implement** over `argon2id` from `@noble/hashes/argon2.js`. This file is the only importer of that package.
- [ ] **Step 4: Verify and commit.** Note the wall-clock time the KAT takes; if it exceeds 3 seconds, report it rather than lowering the parameters.

---

### Task 5: HPKE group-key envelopes

**Files:**
- Create: `web/src/crypto/hpke.ts`, `web/src/crypto/hpke.test.ts`

**Interfaces produced:**
- `generateEncryptionIdentity(): Promise<{ publicKey: CryptoKey, privateKey: CryptoKey }>` — X25519
- `sealToPublicKey(recipientPublicKey, plaintext, additionalData): Promise<{ encapsulatedKey, ciphertext }>`
- `openWithPrivateKey(recipientPrivateKey, encapsulatedKey, ciphertext, additionalData): Promise<Uint8Array>`

Suite is fixed: `DhkemX25519HkdfSha256` + `HkdfSha256` + `Aes256Gcm`, base mode.

- [ ] **Step 1: Write the failing tests.**
  - Known-answer: RFC 9180 Appendix A.1 (DHKEM(X25519, HKDF-SHA256), HKDF-SHA256, AES-128-GCM) run through the library to prove the wiring, then the project suite round-trips.
  - Round-trip: user A seals a 32-byte group key to user B's public key, B opens it.
  - Tamper: modified `encapsulatedKey` rejects; modified ciphertext rejects; wrong recipient private key rejects; changed AAD rejects.
- [ ] **Step 2: Run them, confirm they fail.** `cd web && npx vitest run src/crypto/hpke.test.ts`
- [ ] **Step 3: Implement.** This file is the only importer of `@hpke/core`.
- [ ] **Step 4: Verify and commit.**

---

### Task 6: The key hierarchy — master key, identity key, record keys, personal envelopes

**Files:**
- Create: `web/src/crypto/keyHierarchy.ts`, `web/src/crypto/keyHierarchy.test.ts`

**Interfaces produced:**
- `createUserMasterKey()`
- `createEncryptionIdentity(userMasterKey, userId)` — generates the X25519 pair, exports the private key **once**, immediately encrypts it under the master key with `masterKeyAdditionalData`, and returns `{ publicKeyBytes, encryptedPrivateKey }`. The extractable private key is never returned.
- `unlockEncryptionIdentity(userMasterKey, userId, encryptedPrivateKey)` — imports non-extractable.
- `createRecordKey()`
- `sealRecordPayload(recordKey, payload, descriptor)` / `openRecordPayload(recordKey, sealed, descriptor)`
- `wrapRecordKeyForOwner(userMasterKey, recordKey, descriptor)` / `unwrapRecordKeyForOwner(...)`
- `wrapMasterKey(wrappingKey, userMasterKey, wrapperDescriptor)` / `unwrapMasterKey(...)`

- [ ] **Step 1: Write the failing tests.**
  - A record payload round-trips through its record key and personal envelope.
  - `createEncryptionIdentity` returns no `CryptoKey` and no raw private bytes — assert the returned object's values are `Uint8Array` and that the encrypted private key does not contain the public key bytes.
  - The unlocked private key has `extractable === false`.
  - **Two-user fixture:** user B's master key cannot unwrap user A's record envelope, and the failure is a rejection, not a wrong plaintext.
  - Changing the record's `ownerId` between seal and open fails, because the owner is in the AAD.
- [ ] **Step 2: Run them, confirm they fail.** `cd web && npx vitest run src/crypto/keyHierarchy.test.ts`
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Verify and commit.**

---

### Task 7: Group keys, group envelopes, and rotation

**Files:**
- Create: `web/src/crypto/groupKeys.ts`, `web/src/crypto/groupKeys.test.ts`

**Interfaces produced:**
- `createGroupKey()`
- `sealGroupKeyForMember(memberPublicKey, groupKey, groupId)` — HPKE, one envelope per member
- `openGroupKeyEnvelope(memberPrivateKey, envelope, groupId)`
- `wrapRecordKeyForGroup(groupKey, recordKey, descriptor)` / `unwrapRecordKeyForGroup(...)`
- `rotateGroupKey(previousMembers, remainingMemberPublicKeys, groupId)` — returns a new group key plus fresh member envelopes
- `rotateRecordKeys(records, newGroupKey, ownerMasterKey)` — new record key, re-sealed payload, new personal and group envelopes

- [ ] **Step 1: Write the failing tests.**
  - Two members of one group both open the group key; a third user cannot.
  - **Two-group fixture:** a record granted to group A is not decryptable with group B's key.
  - **Revocation:** after `rotateGroupKey`, the removed member's old envelope still opens the *old* group key (history stays readable — the spec says so plainly), but the new record revision is sealed under the new key and the removed member cannot open it.
  - `rotateRecordKeys` produces a different record key and a payload that the old record key cannot open.
- [ ] **Step 2: Run them, confirm they fail.** `cd web && npx vitest run src/crypto/groupKeys.test.ts`
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Verify and commit.**

---

## Phase 1 — The worker and local storage

### Task 8: Worker command protocol and the pure handler

**Files:**
- Create: `web/src/crypto/worker/commands.ts`
- Create: `web/src/crypto/worker/handler.ts`, `web/src/crypto/worker/handler.test.ts`

`handler.ts` holds the whole worker brain as a plain module with no `self` reference, so it is testable in jsdom. It owns a module-private key set: the user master key, the unlocked identity private key, group keys, and a record-key cache. **Nothing in that set is ever placed on a response.**

Commands: `unlockWithMasterKey`, `lock`, `encryptRecord`, `decryptRecord`, `addGroupEnvelope`, `removeGroupEnvelope`, `importGroupKey`, `rotateGroup`, `wrapMasterKeyForNewWrapper`.

- [ ] **Step 1: Write the failing tests.**
  - Unlock then encrypt then decrypt round-trips a transaction payload.
  - `lock` clears the key set; a subsequent `decryptRecord` returns a `vault-locked` error rather than throwing an unhandled rejection.
  - **The escape test:** run every command and walk each response object recursively, asserting no value is a `CryptoKey` and no `Uint8Array` in the response equals any raw key generated by the fixture. This test is the rule, so give it a name that says so.
  - An unknown command returns an error result rather than throwing.
- [ ] **Step 2: Run them, confirm they fail.** `cd web && npx vitest run src/crypto/worker/handler.test.ts`
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Verify and commit.**

---

### Task 9: Worker entry and the main-thread proxy

**Files:**
- Create: `web/src/crypto/worker/cryptoWorker.ts` — the entry, `self.onmessage` delegating to `handler.ts`
- Create: `web/src/crypto/worker/vaultWorkerClient.ts`, `web/src/crypto/worker/vaultWorkerClient.test.ts`

The client constructs `new Worker(new URL("./cryptoWorker.ts", import.meta.url), { type: "module" })` so Vite bundles it, keeps a request-id map, and exposes `terminate()` used by lock. jsdom has no `Worker`, so the test injects a stub worker factory.

- [ ] **Step 1: Write the failing tests.** Requests and responses correlate by id under concurrency; `terminate()` calls the worker's `terminate` and rejects every in-flight request; a worker error rejects rather than hanging.
- [ ] **Step 2: Run it, confirm it fails.** `cd web && npx vitest run src/crypto/worker/vaultWorkerClient.test.ts`
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Verify, build, commit.** `cd web && npx vitest run src/crypto/worker && npm run build` — the build proves Vite emits the worker chunk.

---

### Task 10: IndexedDB ciphertext store

**Files:**
- Create: `web/src/vault/vaultDatabase.ts`, `web/src/vault/vaultDatabase.test.ts`

One database, `xpense-vault`, version 1, with object stores:

| Store | Key | Holds |
|---|---|---|
| `records` | record UUID | type, owner, revision, nonce, ciphertext, tombstone, server timestamps |
| `envelopes` | `[recordId, groupId]` | wrapped key, nonce, protocol version |
| `wrappers` | wrapper id | master-key wrappers: passkey, recovery password, recovery file |
| `syncState` | fixed key | cursor, last sync time |
| `outbox` | operation id | queued encrypted mutations and their idempotency keys |
| `quarantine` | record UUID | ciphertext that failed authentication, plus the reason |

- [ ] **Step 1: Write the failing tests.** Put/get/delete per store; the cursor survives a close and reopen; an upgrade from no database creates all six stores; **a blindness test** that walks every stored value and asserts no plaintext label from the fixture appears anywhere and no value is a `CryptoKey`.
- [ ] **Step 2: Run them, confirm they fail.** `cd web && npx vitest run src/vault/vaultDatabase.test.ts`
- [ ] **Step 3: Implement.** Plain `indexedDB` — no wrapper library; the surface is six stores and four verbs.
- [ ] **Step 4: Verify and commit.**

---

## Phase 2 — Unlock and lock

### Task 11: Passkey PRF registration, assertion, and the stripping serializer

**Files:**
- Create: `web/src/vault/passkey.ts`, `web/src/vault/passkey.test.ts`

**Interfaces produced:**
- `createPasskeyWithPrf(options)` — requests `extensions: { prf: { eval: { first: salt } } }` during creation, reads `getClientExtensionResults().prf`
- `requestPrfAssertion(options, salt)`
- `derivePasskeyWrappingKey(prfOutput)` — HKDF-SHA-256, info `Xpense passkey vault wrap v1`, from `protocol.ts`
- `serializeAttestationForApi(credential)` / `serializeAssertionForApi(credential)` — the dedicated serializers

`PublicKeyCredential.toJSON()` includes client extension results, and therefore the PRF output. **These serializers must never call `toJSON()`.** They build the API payload field by field from `id`, `rawId`, `type`, `response.clientDataJSON`, `response.authenticatorData`, `response.signature`, `response.userHandle`, `response.attestationObject`, `response.transports`.

Registration-without-immediate-PRF fallback: if creation returns `prf.enabled === true` but no `prf.results.first`, immediately perform an assertion with the same salt to obtain it. If `prf.enabled` is falsy, return `prfUnsupported` — the caller then requires a recovery wrapper and must not create a passkey-only vault.

- [ ] **Step 1: Write the failing tests**, with a stubbed `navigator.credentials`:
  - PRF returned at creation → wrapping key derived, no assertion performed.
  - `enabled: true` with no results → exactly one follow-up assertion, same salt, wrapping key derived.
  - `enabled` absent → `prfUnsupported`, and no wrapper is created.
  - Two credentials on one account each derive their own wrapping key from their own salt.
  - **The serializer test:** given a credential whose `getClientExtensionResults()` contains a PRF result, `JSON.stringify(serializeAssertionForApi(credential))` contains neither `prf` nor `clientExtensionResults` nor the PRF bytes in any encoding.
  - A replayed assertion with a stale challenge is rejected by the caller, not silently accepted.
- [ ] **Step 2: Run them, confirm they fail.** `cd web && npx vitest run src/vault/passkey.test.ts`
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Verify and commit.**

---

### Task 12: Recovery password wrapper

**Files:**
- Create: `web/src/vault/recoveryPassword.ts`, `web/src/vault/recoveryPassword.test.ts`

Creates a second, independent wrapper of the User Master Key. The password itself also goes to `POST /api/v1/auth/password/sign-in` over TLS for fallback authentication — that is the identity plan's endpoint; this module only produces the local wrapper and stores the parameters beside the ciphertext.

- [ ] **Step 1: Write the failing tests.** Configure then unwrap round-trips; the wrong password rejects; a stored wrapper carries its salt and full parameter record; a wrapper written with older parameters still unwraps; a password under 14 characters is refused with a prose message; the wrapper record contains no password and no derived key.
- [ ] **Step 2: Run them, confirm they fail.** `cd web && npx vitest run src/vault/recoveryPassword.test.ts`
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Verify and commit.**

---

### Task 13: Recovery file

**Files:**
- Create: `web/src/vault/recoveryFile.ts`, `web/src/vault/recoveryFile.test.ts`

The file holds two **independent** random 256-bit values: an authentication token and a vault secret. The server receives only `SHA-256(authenticationToken)`. The vault secret never crosses the network; it derives the wrapping key via HKDF-SHA-256 with info `Xpense recovery file vault wrap v1`.

- [ ] **Step 1: Write the failing tests.**
  - `createRecoveryFile` returns two different 32-byte values and a server payload containing only the token hash.
  - The file's text contains no email and no financial data.
  - The vault secret unwraps the master key; the authentication token does not.
  - Parsing a file with a corrupted line fails with a prose error rather than a partial unlock.
  - After a successful recovery unlock, `replaceRecoveryFile` produces a different token and a different vault secret, and the old vault secret no longer unwraps — this is what makes each file single-use.
- [ ] **Step 2: Run them, confirm they fail.** `cd web && npx vitest run src/vault/recoveryFile.test.ts`
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Verify and commit.**

---

### Task 14: Vault state machine and the lock rules

**Files:**
- Create: `web/src/vault/vaultState.ts`, `web/src/vault/vaultState.test.ts`
- Create: `web/src/vault/VaultProvider.tsx`, `web/src/vault/VaultProvider.test.tsx`
- Modify: `web/src/App.tsx`

States: `locked`, `unlocking`, `unlocked`, `unavailable`. Lock triggers, all three from the spec:

1. Manual lock, always available.
2. 15 minutes with no interaction — `pointerdown`, `keydown`, `visibilitychange` reset the timer.
3. 15 minutes continuously hidden — a separate timer started on `visibilitychange` to hidden and cleared on visible.

Lock terminates the crypto worker, clears decrypted React state, and clears sensitive error detail. `IDLE_LOCK_MILLISECONDS` and `HIDDEN_LOCK_MILLISECONDS` are constants at the top of `vaultState.ts`.

`VaultProvider` wraps the routes inside `FluentProvider` in `App.tsx`, above `LoadingContextProvider`.

- [ ] **Step 1: Write the failing tests** with `vi.useFakeTimers()`:
  - Idle for 14:59 stays unlocked; at 15:00 it locks.
  - An interaction at 14:00 pushes the lock to 29:00.
  - Hidden for 15 minutes locks even though the idle timer was reset just before hiding.
  - Manual lock terminates the worker and clears the decrypted projection.
  - A valid sign-in whose unwrap fails leaves the state `locked`, not `unavailable`, and offers another wrapper.
  - Reloading the page never restores an unlocked key — assert that no unlocked `CryptoKey` is read from `vaultDatabase`.
- [ ] **Step 2: Run them, confirm they fail.** `cd web && npx vitest run src/vault/vaultState.test.ts src/vault/VaultProvider.test.tsx`
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Verify and commit.** `cd web && npm test`

---

## Phase 3 — API blind sync surface (EXPAND only)

### Task 15: Encrypted-record schema and the expand migration

**Files:**
- Create: `api/src/Xpense/Xpense.Domain/Entities/EncryptedRecord.cs`
- Create: `api/src/Xpense/Xpense.Domain/Entities/RecordEnvelope.cs`
- Create: `api/src/Xpense/Xpense.Domain/Entities/SyncOperation.cs`
- Create: `api/src/Xpense/Xpense.Domain/Enums/EncryptedRecordType.cs`
- Create: `api/src/Xpense/Xpense.Persistence/TypeConfiguration/EncryptedRecordEntityTypeConfiguration.cs`, `RecordEnvelopeEntityTypeConfiguration.cs`, `SyncOperationEntityTypeConfiguration.cs`
- Modify: `api/src/Xpense/Xpense.Persistence/XpenseDbContext.cs`
- Create: `api/src/Xpense/Xpense.Persistence/Migrations/<timestamp>_AddEncryptedRecords.cs`
- Create: `api/src/Xpense/Xpense.Tests/Integration/EncryptedRecordSchemaTests.cs`

Columns that matter:

- `EncryptedRecords`: `Id` uuid PK, `RecordType` int, `OwnerUserId` uuid, `ParentResourceId` uuid null, `Revision` bigint, `ProtocolVersion` int, `Nonce` bytea, `Ciphertext` bytea, `IsDeleted` bool, `SequenceNumber` bigint from a Postgres sequence, `CreatedAt`, `UpdatedAt`.
- `RecordEnvelopes`: `Id` uuid, `EncryptedRecordId`, `GroupId` uuid **nullable — null means the owner's personal envelope**, `WrappedKey` bytea, `Nonce` bytea, `EncapsulatedKey` bytea null, `ProtocolVersion`, unique on `(EncryptedRecordId, GroupId)`.
- `ResourceGrants` is the canonical grant table created by Task 3 of the identity plan. The sync surface reuses it rather than creating a narrower duplicate.
- `SyncOperations`: `Id`, `UserId`, `IdempotencyKey` text, `EncryptedRecordId`, `CreatedAt`, unique on `(UserId, IdempotencyKey)`.

`SequenceNumber` is the sync cursor. A database sequence is monotonic without clock skew, which a timestamp cursor is not.

**This migration adds tables only.** It must contain no `DropColumn`, no `DropTable`, no `AlterColumn` against an existing table.

- [ ] **Step 1: Write the failing test.** `EncryptedRecordSchemaTests` applies migrations on the Testcontainers Postgres, then asserts: the four tables exist; the unique indexes exist; `SequenceNumber` increases across two inserts; and — the expand guard — `Accounts`, `Transactions`, `Categories`, `Merchants`, `Tags`, `Budgets`, `Notifications` still hold every column they held before.
- [ ] **Step 2: Run it, confirm it fails.** `cd api && dotnet test src/Xpense/Xpense.sln --filter FullyQualifiedName~EncryptedRecordSchemaTests`
- [ ] **Step 3: Implement**, then generate the migration:

```bash
cd api && dotnet dotnet-ef migrations add AddEncryptedRecords --project src/Xpense/Xpense.Persistence --startup-project src/Xpense/Xpense.Persistence
```

Read the generated `Up` before committing and confirm it only creates.

- [ ] **Step 4: Verify and commit.** `cd api && dotnet build src/Xpense/Xpense.sln && dotnet test src/Xpense/Xpense.sln`

---

### Task 16: The authorization seam

**Files:**
- Create: `api/src/Xpense/Xpense.API/Infrastructure/ICurrentUser.cs`
- Create: `api/src/Xpense/Xpense.API/Infrastructure/SyncAuthorization.cs`
- Modify: `api/src/Xpense/Xpense.API/Extensions/IoC.cs`
- Create: `api/src/Xpense/Xpense.Tests/Integration/SyncAuthorizationTests.cs`
- Modify: `api/src/Xpense/Xpense.Tests/Infrastructure/WebApiTestFactory.cs`

`ICurrentUser` exposes one property: the authenticated user's UUID. Until the identity plan lands, the registered implementation reads it from the ASP.NET Core Identity claim when present and the test factory registers a double. **This is the only authentication code this plan writes.**

`SyncAuthorization` is the shared predicate the spec permits in infrastructure: a record is readable when the caller owns it, or an active group membership holds an active Viewer or Editor grant on its `ParentResourceId`; writable when owned or Editor. Request and response types stay inside their slices — `SliceIsolationTests` still enforces that.

- [ ] **Step 1: Write the failing tests** — the permission matrix from the spec: private owner, group Viewer, group Editor, group Owner without a grant, revoked member, unrelated user. Unauthorized reads return **404, not 403**, so record existence is not disclosed.
- [ ] **Step 2: Run them, confirm they fail.** `cd api && dotnet test src/Xpense/Xpense.sln --filter FullyQualifiedName~SyncAuthorizationTests`
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Verify and commit.**

---

### Task 17: `GET /api/v1/sync/changes`

**Files:**
- Create: `api/src/Xpense/Xpense.API/Features/Sync/GetSyncChanges.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Sync/EncryptedRecordResponse.cs`
- Create: `api/src/Xpense/Xpense.Tests/Integration/SyncChangesTests.cs`
- Modify: `api/src/Xpense/Xpense.API/Infrastructure/SwaggerTags.cs`

Returns records with `SequenceNumber` greater than the decoded cursor, ordered by `SequenceNumber`, page size capped, with the next cursor and a `hasMore` flag. Tombstones are included — a client that never sees a delete never applies it. The cursor is base64url of the sequence number; an unparsable cursor is a 400 with a prose message.

- [ ] **Step 1: Write the failing tests.** Empty cursor returns everything accessible; the returned cursor resumes exactly where it stopped with no gap and no repeat; ordering is by sequence number, not by `UpdatedAt`; tombstones appear; another user's records never appear; a garbage cursor is 400.
- [ ] **Step 2: Run them, confirm they fail.** `cd api && dotnet test src/Xpense/Xpense.sln --filter FullyQualifiedName~SyncChangesTests`
- [ ] **Step 3: Implement.** `<summary>` on the response record only, because Swagger renders it.
- [ ] **Step 4: Verify and commit.**

---

### Task 18: `POST /api/v1/sync/records`

**Files:**
- Create: `api/src/Xpense/Xpense.API/Features/Sync/CreateSyncRecords.cs`
- Create: `api/src/Xpense/Xpense.Tests/Integration/SyncCreateTests.cs`

Batch create. Each item carries a client idempotency key, the record type, the parent resource id, the protocol version, nonce, ciphertext, and its personal envelope. Rejections, all prose: unknown record type, ciphertext over the cap, batch over the cap, a parent resource the caller cannot write, protocol version zero or unknown. Creates return `201` with an absolute `Location` via `HttpContext.ResourceUri`.

- [ ] **Step 1: Write the failing tests.** A repeated idempotency key returns the original record and creates nothing new; a batch is atomic — one invalid item rejects the whole batch; an unknown record type is 400; oversized ciphertext is 400; a parent resource belonging to someone else is 404; the created row's `SequenceNumber` is greater than every existing one.
- [ ] **Step 2: Run them, confirm they fail.** `cd api && dotnet test src/Xpense/Xpense.sln --filter FullyQualifiedName~SyncCreateTests`
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Verify and commit.**

---

### Task 19: `PUT /api/v1/sync/records/{id}`

**Files:**
- Create: `api/src/Xpense/Xpense.API/Features/Sync/ReplaceSyncRecord.cs`
- Create: `api/src/Xpense/Xpense.Tests/Integration/SyncReplaceTests.cs`

Replaces ciphertext at an expected revision. A mismatch returns **409 with the latest record in the body** — the client cannot resolve a conflict it cannot see. A success bumps `Revision` and takes a new `SequenceNumber`. The AAD binds the revision, so the client re-encrypts on every write; the server never rewrites ciphertext.

- [ ] **Step 1: Write the failing tests.** Matching revision succeeds and increments; stale revision returns 409 carrying the current ciphertext and revision; a concurrent double update lets exactly one win; a Viewer gets 404; the record type and owner cannot be changed by an update.
- [ ] **Step 2: Run them, confirm they fail.** `cd api && dotnet test src/Xpense/Xpense.sln --filter FullyQualifiedName~SyncReplaceTests`
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Verify and commit.**

---

### Task 20: `DELETE /api/v1/sync/records/{id}`

**Files:**
- Create: `api/src/Xpense/Xpense.API/Features/Sync/DeleteSyncRecord.cs`
- Create: `api/src/Xpense/Xpense.Tests/Integration/SyncDeleteTests.cs`

Soft delete, per house rule: `MarkAsDeleted()` + `Touch()`, a new `SequenceNumber`, ciphertext retained so a client mid-sync can still authenticate what it already holds.

- [ ] **Step 1: Write the failing tests.** Delete produces a tombstone visible in `/sync/changes`; deleting twice is idempotent; a Viewer gets 404; an Editor on the parent resource may delete; the tombstone keeps its revision so a later stale update still 409s.
- [ ] **Step 2: Run them, confirm they fail.** `cd api && dotnet test src/Xpense/Xpense.sln --filter FullyQualifiedName~SyncDeleteTests`
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Verify and commit.**

---

### Task 21: Envelope grant and revoke

**Files:**
- Create: `api/src/Xpense/Xpense.API/Features/Sync/AddRecordEnvelope.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Sync/RevokeRecordEnvelope.cs`
- Create: `api/src/Xpense/Xpense.Tests/Integration/SyncEnvelopeTests.cs`

Two endpoints, two files — the architecture rule. Only the record's **personal owner** may add or remove a group envelope. Adding stores the wrapped key the client produced; the server never constructs one. Revoking removes the envelope and marks the grant revoked, which blocks API access immediately.

- [ ] **Step 1: Write the failing tests.** The owner adds an envelope for a group and members of that group then see the record in `/sync/changes`; a group Owner who does not own the record gets 404; adding a second envelope for the same group replaces rather than duplicates; revoking hides the record from the group on the next `/sync/changes`; revoking does not delete the record for its owner; an envelope for a group the caller does not belong to is rejected.
- [ ] **Step 2: Run them, confirm they fail.** `cd api && dotnet test src/Xpense/Xpense.sln --filter FullyQualifiedName~SyncEnvelopeTests`
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Verify and commit.**

---

### Task 22: Server blindness tests

**Files:**
- Create: `api/src/Xpense/Xpense.Tests/Integration/ServerBlindnessTests.cs`
- Modify: `api/src/Xpense/Xpense.Tests/Architecture/SliceIsolationTests.cs` if the new slices need it — they should not

This is the API half of the acceptance criteria and it runs on every build from here on.

- [ ] **Step 1: Write the failing tests.**
  - Write a batch of records whose plaintext contains a marker string, then query `EncryptedRecords`, `RecordEnvelopes`, `ResourceGrants` and `SyncOperations` directly and assert the marker appears in no column of any row.
  - Assert no `sync` slice reads `Ciphertext` for any purpose other than returning it — grep-style assertion over the compiled slice types, or simply assert the handlers never call a decrypt API, since none exists in the solution.
  - Assert `EncryptedRecord` has no navigation to `Transaction`, `Account`, `Category` or `Budget`. The encrypted graph and the plaintext graph do not touch.
  - Assert a full `/sync/changes` response body for user A, serialised, contains none of user B's ciphertext bytes.
- [ ] **Step 2: Run them, confirm they fail.** `cd api && dotnet test src/Xpense/Xpense.sln --filter FullyQualifiedName~ServerBlindnessTests`
- [ ] **Step 3: Implement whatever the tests catch.**
- [ ] **Step 4: Full verification and commit.** `cd api && dotnet build src/Xpense/Xpense.sln && dotnet test src/Xpense/Xpense.sln`

---

## Phase 4 — Client sync, projection, and domain

### Task 23: Sync client and local projection

**Files:**
- Create: `web/src/sync/syncClient.ts`, `web/src/sync/syncClient.test.ts`
- Create: `web/src/sync/projection.ts`, `web/src/sync/projection.test.ts`

`syncClient` calls the six endpoints through `axios`, matching the existing `web/src/clients/` style. `projection.ts` turns decrypted records into the view models the pages already consume — the same shapes as `web/src/clients/types.ts`, so pages change as little as possible.

- [ ] **Step 1: Write the failing tests** with a mocked axios: pull applies creates, updates and tombstones in cursor order; the cursor is persisted after each page and resumed after a simulated reload; a decrypt failure moves the record to `quarantine` and leaves the previous good revision in `records`; a quarantined record never reaches the projection; the projection is rebuilt from IndexedDB alone after an unlock, with no network call.
- [ ] **Step 2: Run them, confirm they fail.** `cd web && npx vitest run src/sync/syncClient.test.ts src/sync/projection.test.ts`
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Verify and commit.**

---

### Task 24: Offline outbox, idempotency, and conflicts

**Files:**
- Create: `web/src/sync/outbox.ts`, `web/src/sync/outbox.test.ts`
- Create: `web/src/sync/conflicts.ts`, `web/src/sync/conflicts.test.ts`

Every mutation is encrypted, written to `outbox` with a `crypto.randomUUID()` idempotency key, and applied optimistically to the projection. The key is retained until the server acknowledges, so a replay after a dropped connection creates nothing twice. A 409 produces a conflict entry holding both decrypted revisions for the user to choose between — never a silent overwrite.

- [ ] **Step 1: Write the failing tests.** A mutation made offline is queued and replays on reconnect with its original idempotency key; a replayed create does not duplicate; a 409 records a conflict and does not touch the local record; resolving with "keep mine" re-encrypts at the new revision and retries; resolving with "keep theirs" drops the outbox entry; the outbox survives a lock and a reload; a queued entry holds ciphertext only.
- [ ] **Step 2: Run them, confirm they fail.** `cd web && npx vitest run src/sync/outbox.test.ts src/sync/conflicts.test.ts`
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Verify and commit.**

---

### Task 25: The client domain module

**Files:**
- Create: `web/src/domain/accountBalance.ts`, `web/src/domain/accountBalance.test.ts`
- Create: `web/src/domain/currencyMatch.ts`, `web/src/domain/currencyMatch.test.ts`
- Create: `web/src/domain/budgetSpending.ts`, `web/src/domain/budgetSpending.test.ts`
- Create: `web/src/domain/spendingByCategory.ts`, `web/src/domain/spendingByCategory.test.ts`
- Modify: `web/src/pages/Overview/Overview.tsx`, `web/src/pages/Budgets/Budgets.tsx`, `web/src/pages/Transactions/Transactions.tsx` to read the projection instead of the plaintext clients

The server can no longer calculate any of this. **Do not rewrite what already works on the client:** `web/src/budgets/budgetProgress.ts` and `web/src/budgets/budgetFormRules.ts` stay untouched and their tests must keep passing unchanged. `budgetSpending.ts` produces the `spent`, `uncounted` and `period` fields that `budgetProgress` already consumes, so the seam is the data, not the logic.

Rules carried over from the API:
- Never sum across currencies. One figure per currency.
- `uncounted` is spending in the budget's category and period in a **different** currency from the budget.
- A transaction whose currency does not match its account is a validation error, the client-side replacement for the API's 400.
- Budget periods follow the existing recurrence rules, ISO weeks included.

- [ ] **Step 1: Write the failing tests.** Port the assertions from `api/src/Xpense/Xpense.Tests/Unit/BudgetTests.cs` and `TransactionTests.cs`, including the ISO-week boundary and the non-repeating budget that must state an end. Add: a mixed-currency account list produces one balance per currency and no total; a mismatched transaction currency is rejected with a prose message.
- [ ] **Step 2: Run them, confirm they fail.** `cd web && npx vitest run src/domain`
- [ ] **Step 3: Implement, then wire the pages.**
- [ ] **Step 4: Verify and commit.** `cd web && npx vitest run src/budgets src/domain && npm test` — `budgetProgress.test.ts` and `budgetFormRules.test.ts` must pass **unedited**. If either needs a change, stop and report; that means logic moved that should not have.

---

### Task 26: Content Security Policy, Trusted Types, and bundling

**Files:**
- Modify: `web/index.html`
- Modify: `web/vite.config.ts`
- Modify: `web/vite.config.test.ts`

A strict CSP with no inline script, no third-party runtime script, `worker-src 'self'`, `connect-src 'self'`, and `require-trusted-types-for 'script'` where supported. Every dependency is bundled; nothing is loaded from a CDN.

- [ ] **Step 1: Write the failing tests** in `vite.config.test.ts`: the built `index.html` carries the CSP meta tag with the expected directives, and the build output contains no absolute `http://` or `https://` script source.
- [ ] **Step 2: Run it, confirm it fails.** `cd web && npx vitest run vite.config.test.ts`
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Verify and commit.** `cd web && npm run build && npx vitest run vite.config.test.ts` — the build must not warn about a blocked inline style. Fluent's Griffel injects styles at runtime, so `style-src` needs `'unsafe-inline'`; **script** never does. Say so in the commit message, not in a comment.

---

### Task 27: Browser security gates — virtual authenticator and the network test

**Files:**
- Modify: `web/package.json`
- Create: `web/vitest.browser.config.ts`
- Create: `web/src/vault/passkey.browser.test.ts`
- Create: `web/src/vault/networkLeak.browser.test.ts`

These two cannot run in jsdom. A second Vitest config with the Playwright provider and Chromium runs them; the default `npm test` is untouched.

- [ ] **Step 1: Install.** `cd web && npm install --save-dev @vitest/browser playwright && npx playwright install chromium`

- [ ] **Step 2: Write the failing tests.**

`passkey.browser.test.ts` uses the Chromium CDP `WebAuthn` domain to add a virtual authenticator with `hasPrf` enabled, then covers the spec's five cases: supported PRF, ignored PRF, registration without an immediate PRF result, replay of a used challenge, and two credentials on one account.

`networkLeak.browser.test.ts` is the strongest gate in this plan. It records the PRF output the fixture authenticator returns, drives a full unlock and a sync round trip while intercepting every request, then asserts the PRF bytes — raw, base64, base64url and hex — appear in **no** request URL, header or body, in no console message, in `localStorage`, in `sessionStorage`, or in any IndexedDB value. It repeats the assertion for the unwrapped User Master Key.

- [ ] **Step 3: Run them, confirm they fail.** `cd web && npx vitest run --config vitest.browser.config.ts`
- [ ] **Step 4: Implement whatever they catch.**
- [ ] **Step 5: Verify and commit.** Add a `test:browser` script to `package.json` so CI can call it separately.

---

### Task 28: Full-stack verification

- [ ] **Step 1: Run everything.**

```bash
cd web && npm test && npm run build && npm run check:init && npx vitest run --config vitest.browser.config.ts
cd ../api && dotnet build src/Xpense/Xpense.sln && dotnet test src/Xpense/Xpense.sln
```

- [ ] **Step 2:** The API build is warning-free. Keep it that way.
- [ ] **Step 3:** `ApiEndpointTests` should still pass untouched — the sync surface is additive and the plaintext endpoints are still live in this phase.
- [ ] **Step 4: Commit.**

---

## Phase 5 — GATED: legacy claim and the contract migration

> **STOP. Read this before executing anything below.**
>
> Phase 5 is the only destructive work in this plan and it is **gated by the spec**, not by convenience. Two gates must be satisfied first:
>
> 1. **An external cryptography-focused review** of the Phase 0–2 modules. The spec makes this a release gate: "A cryptography-focused external review is required before removing the plaintext schema."
> 2. **A claim rehearsal on a scratch copy of the database** that verifies record counts and the manifest hash, and proves a failed claim is restartable.
>
> **An autonomous agent must NOT execute the contract migration in Task 32.** It removes the plaintext financial schema. Once it runs, the plaintext data is gone and only the encrypted set remains — and only the user's keys can read that. Tasks 29 to 31 may be implemented and tested; Task 32 may be **written** but is applied by the operator, by hand, after both gates pass.

### Task 29: Claim mode and the claim token (still EXPAND only)

**Files:**
- Create: `api/src/Xpense/Xpense.Domain/Entities/ClaimToken.cs`
- Create: `api/src/Xpense/Xpense.Persistence/TypeConfiguration/ClaimTokenEntityTypeConfiguration.cs`
- Create: `api/src/Xpense/Xpense.Persistence/Migrations/<timestamp>_AddClaimTokens.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Claim/StartClaim.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Claim/DownloadLegacyDataset.cs`
- Create: `api/src/Xpense/Xpense.API/Features/Claim/CompleteClaim.cs`
- Create: `api/src/Xpense/Xpense.Tests/Integration/ClaimTests.cs`

Claim mode is configuration, not a migration and not a startup seeder — ADR 0004 and ADR 0005 both apply. The token is single-use, expires in 30 minutes, and only its hash is stored. While claim mode is on, normal financial writes are blocked. **No migration creates a user or rewrites ownership.**

- [ ] **Step 1: Write the failing tests.** A token outside claim mode is refused; an expired token is refused; a consumed token is refused; financial writes return a prose 409 during claim mode; the dataset download returns every plaintext row once; `CompleteClaim` marks the token consumed only after count and manifest verification succeeds; a failed verification leaves the plaintext untouched, the token unconsumed and claim mode on.
- [ ] **Step 2: Run them, confirm they fail.** `cd api && dotnet test src/Xpense/Xpense.sln --filter FullyQualifiedName~ClaimTests`
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Verify and commit.**

---

### Task 30: The claim client flow

**Files:**
- Create: `web/src/claim/claimDataset.ts`, `web/src/claim/claimDataset.test.ts`
- Create: `web/src/claim/manifest.ts`, `web/src/claim/manifest.test.ts`
- Create: `web/src/pages/Claim/Claim.tsx`, `web/src/pages/Claim/Claim.test.tsx`

The unlocked browser downloads the plaintext dataset once, validates it against the Task 25 domain rules, encrypts every record, uploads the encrypted set, and computes a manifest hash: SHA-256 over a canonical newline-joined list of `<recordType>|<recordUuid>` sorted lexically, prefixed by the record count.

- [ ] **Step 1: Write the failing tests.** The manifest hash is stable across two runs over the same dataset and changes when one record is dropped; a locked vault refuses to start a claim; an interrupted upload is restartable and does not double-create, because every item keeps its idempotency key; a domain-rule violation in the legacy data aborts the claim with a prose report naming the record; the encrypted set contains no plaintext label.
- [ ] **Step 2: Run them, confirm they fail.** `cd web && npx vitest run src/claim`
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Verify and commit.**

---

### Task 31: The claim rehearsal — GATE

**Files:**
- Create: `api/docs/runbooks/legacy-claim-rehearsal.md`

- [ ] **Step 1:** Take a copy of the production database into a scratch database. `api/backups/` already holds the backup tooling.
- [ ] **Step 2:** Run the full claim against the scratch copy with a real browser and a real unlocked vault.
- [ ] **Step 3:** Verify record counts per type and the manifest hash, both directions.
- [ ] **Step 4:** Kill the claim halfway and restart it. Prove it completes and that no record was created twice.
- [ ] **Step 5:** Write the verification report into the runbook. **This report plus the external cryptography review are the two gates on Task 32.**
- [ ] **Step 6: Commit the runbook.** Do not proceed without both gates.

---

### Task 32: The CONTRACT migration — WRITE ONLY, DO NOT APPLY

**Files:**
- Create: `api/src/Xpense/Xpense.Persistence/Migrations/<timestamp>_RemovePlaintextFinancials.cs`
- Delete: the plaintext slices under `api/src/Xpense/Xpense.API/Features/{Accounts,Transactions,Categories,Merchants,Tags,Budgets,Analytics,Priorities}`
- Delete: the plaintext entities and their type configurations
- Modify: `api/src/Xpense/Xpense.Tests/Integration/ApiEndpointTests.cs`
- Create: `api/src/Xpense/Xpense.Tests/Integration/PlaintextRemovedTests.cs`

**An autonomous agent must not run this migration.** Write it, review it, and hand it to the operator. It drops the plaintext financial tables and disables the legacy endpoints. There is no rollback that restores readable data — after it runs, the only copy is ciphertext that only the user's keys can open.

- [ ] **Step 1: Write the failing test.** `PlaintextRemovedTests` asserts the plaintext tables no longer exist and that a set of representative marker values from the claim rehearsal appear in no column of any remaining table. This is the spec's database-inspection gate.
- [ ] **Step 2: Write the migration and read its `Up` line by line.**
- [ ] **Step 3: Verify against a scratch database only.**

```bash
cd api && dotnet dotnet-ef database update --project src/Xpense/Xpense.Persistence --startup-project src/Xpense/Xpense.Persistence --connection "<scratch connection string>"
dotnet test src/Xpense/Xpense.sln --filter FullyQualifiedName~PlaintextRemovedTests
```

- [ ] **Step 4: Commit the migration. Do not apply it to any real database.** The `migrations` container applies it on the operator's deploy, after both gates. Say so in the commit message.

---

## Self-review

**Spec coverage.** Versioned primitives and AAD in Tasks 2–5. Key hierarchy, envelopes and groups in Tasks 6–7. The worker and the no-escape rule in Tasks 8–9. IndexedDB in Task 10. Passkey PRF with the registration fallback and the stripping serializer in Task 11. Recovery password and file in Tasks 12–13. Lock rules in Task 14. The six sync endpoints in Tasks 17–21. Sync engine, offline queue and conflicts in Tasks 23–24. The client domain module in Task 25. Every testing and security gate maps to a named file: known-answer and tamper tests in Tasks 3–5, virtual authenticator and the network gate in Task 27, database inspection in Tasks 22 and 32, two-user and two-group fixtures in Tasks 6–7, revocation in Task 7 and Task 21, sync behaviour in Tasks 17–21 and 23–24, the claim rehearsal in Task 31.

**Deliberately not here.** Identity, groups, invitations and email — their own spec and their own plan, and this plan depends on them at Task 16. Server-generated budget emails, which the spec removes from this phase. Native clients. Searchable encryption.

**Reuse over rewrite.** `budgetProgress.ts`, `budgetFormRules.ts`, `formatMoney.ts` and `useDebouncedValue.ts` are untouched and their tests must keep passing unedited. The existing `web/src/clients/` axios style is followed rather than replaced. No new .NET project, no new test framework on the API side, one extra test runner on the web side and only because a virtual authenticator needs a real browser.

**Three riskiest tasks.**

1. **Task 27, the browser security gates.** It is the only proof that PRF output never leaves the client, and it depends on Chromium's CDP `WebAuthn` domain exposing PRF through Playwright. If the virtual authenticator will not produce a PRF result, **report it and stop** — do not weaken the test into something that passes. A missing network gate is worse than a failing one.
2. **Task 25, the client domain module.** Every balance, budget and analytics figure moves from tested C# to new TypeScript in one step. The ISO-week budget boundaries and the `uncounted` cross-currency rule are the two places a silent behaviour change will hide. Port the assertions from `BudgetTests.cs` before writing the implementation, not after.
3. **Task 15, the expand migration.** It is the point of no return for the schema shape, and an accidental `AlterColumn` on an existing table turns an expand into a contract. Read the generated `Up` before committing, and keep the guard test that asserts the plaintext tables still hold every column.

---

## Open questions resolved by assumption

The spec left these open. Each is a decision taken here so the plan is executable; each is cheap to revisit.

1. **Argon2id library.** `@noble/hashes` `argon2.js`, not `hash-wasm`. Audited, zero-dependency, published two days before this plan; `hash-wasm` is faster but last published 2024-11-19. Pure-JS cost is accepted because the recovery path is rare and runs in the worker. Swap is one file.
2. **HPKE library.** `@hpke/core` alone. X25519 lives in core as of 1.x, so `@hpke/dhkem-x25519` is not installed.
3. **AAD encoding.** A canonical `|`-separated UTF-8 string, not JSON. JSON key order is not guaranteed and a re-serialised object would silently fail to decrypt.
4. **Sync cursor.** Base64url of a Postgres `bigint` sequence value. A timestamp cursor loses rows under clock skew.
5. **Record types.** The ten literals in Task 2, mirroring today's plaintext entities plus `userProfile`. The server rejects anything else.
6. **Groups and grants live in the identity plan.** `ResourceGrants` is the single grant model used by group endpoints and the sync surface; this plan adds no duplicate grant table.
7. **`ICurrentUser` is the only auth seam this plan builds.** The identity plan replaces the implementation. No second authentication system.
8. **Idempotency scope.** Keys are unique per user, retained 30 days, then pruned. The spec says "device-independent"; per-user is the narrowest scope that satisfies it.
9. **Ciphertext cap.** 64 KiB per record, 100 records per create batch, 500 records per `/sync/changes` page. The spec requires caps but names no numbers.
10. **Personal envelope encoding.** `RecordEnvelopes.GroupId IS NULL` means the owner's personal envelope. A sentinel UUID would need a fake group row.
11. **Recovery file format.** A plain-text file with labelled base64url lines and a version header, not JSON — a user may have to retype it.
12. **Lock timers.** One idle timer reset by `pointerdown`, `keydown` and `visibilitychange`, and one separate hidden timer. Both 15 minutes, both constants at the top of `vaultState.ts`.
13. **Browser test runner.** Vitest browser mode with the Playwright Chromium provider, driving CDP `WebAuthn` — one runner instead of adding Selenium beside Vitest. The spec says "WebDriver virtual-authenticator"; CDP is the equivalent capability in the runner already installed.
14. **jsdom crypto.** Node's `webcrypto` is installed onto `globalThis` in `vitest.setup.ts` when `subtle` is missing, via `Object.defineProperty` because jsdom defines `crypto` as a getter.
15. **Quarantine.** Its own IndexedDB store, never merged into the projection, surfaced as a sync-integrity error. The last known good revision stays in `records`.
16. **Conflict resolution.** The client offers "keep mine" or "keep theirs" and never merges. The spec says the server cannot merge; it does not say the client should try, and a wrong automatic merge on money is worse than a prompt.
17. **Manifest hash.** SHA-256 over the record count followed by `<recordType>|<recordUuid>` lines sorted lexically. The spec says "canonical manifest hash" without defining it.
18. **Group key rotation is Owner-only and client-driven.** The next unlocked Owner client performs it; the server never holds a group key and cannot rotate one.
19. **CSP.** `script-src 'self'` with no inline script; `style-src` allows inline because Griffel injects styles at runtime. That is the narrowest policy Fluent UI permits.
20. **Protocol version negotiation.** There is none. Version 1 is the only version; a record with any other `ProtocolVersion` is quarantined on the client and rejected by the API. Negotiation gets added when there is a version 2 to negotiate with.
