# Security and encryption

Xpense is designed so that the person running the server cannot read what you spent your money on.

## What this protects against, and what it does not

**It protects against** someone reading the database or its backups — an honest-but-curious
operator, a stolen dump, a misconfigured backup bucket.

**It does not protect against** an operator who serves hostile JavaScript, a compromised browser or
extension, malware on your machine, screenshots, or somebody you shared data with keeping a copy.
The application is delivered by the same server that stores the data; if that server turns hostile
at delivery time, no client-side scheme survives it.

Being specific about this matters more than a reassuring summary would.

## What the server can and cannot see

| The server holds | The server never receives |
| --- | --- |
| Your email and account state | Decrypted amounts, labels, categories or notes |
| Passkey public data and sessions | Your vault master key or any record key |
| Record id, owner, type, ciphertext size, revision, timestamps | The WebAuthn PRF output |
| Group membership, roles, invitations, grants | Group keys or recovery secrets |
| Sync cursors and idempotency keys | Your exported private key, or search terms |

Currency and the time money moved are **inside** the encrypted payload. The accepted leakage is
metadata: which records exist, roughly how big they are, and when they changed.

> **A caveat worth stating plainly.** This release stores encrypted group names, envelopes and
> record ciphertext, but it does not yet migrate the older plaintext financial tables into the
> encrypted store. Until that migration completes, do not describe the whole database as zero
> knowledge.

## How the keys fit together

- Every user has one **master key**. It wraps their encryption private key and every personal
  record envelope.
- Every record has its own **record key**, with one envelope for you and one per group you shared
  it with.
- Every group has a **group key**, sealed to each member's public key with HPKE so the server can
  carry it without opening it.

The master key itself is wrapped by whichever method you unlock with — a passkey's PRF output, a
recovery password stretched with Argon2id, or a recovery file's secret.

Protocol v1 uses AES-256-GCM with fresh 96-bit nonces, HKDF-SHA-256, WebAuthn Level 3 PRF,
Argon2id, and HPKE base mode over X25519. Each ciphertext's associated data binds the protocol
version, record id, record type, owner and revision, so a record cannot be silently moved to
another owner or replayed at an older revision.

## In the browser

Ciphertext, envelopes and sync metadata live in IndexedDB. Unwrapped keys and decrypted data live
only inside a dedicated Web Worker, never in page scope.

The master key is never persisted unlocked, so a reload always needs another unlock. The vault
auto-locks after 15 minutes idle and after 15 minutes hidden, and you can lock it yourself at any
time.

Those timings are fixed in this release. Reloading the page therefore always asks for the passkey
again, which is the cost of never writing the master key to disk in a form that outlives the tab.
Making the timing a user setting is not done yet.

Two browser tests enforce the boundary: one asserts that nothing recognisable as plaintext leaves
the page over the network, and one exercises the encrypted runtime end to end.

## Sync and conflicts

Clients exchange ciphertext through a small sync API.

- Creates are **idempotent** — replaying the same idempotency key returns the record already
  stored, so a retry after a dropped connection cannot duplicate.
- Updates use **optimistic concurrency** on a revision. A stale update returns **409** with the
  current ciphertext; the unlocked client decrypts both versions and asks you which to keep. The
  server cannot merge what it cannot read.
- Deletes are **tombstones**, so every device learns that something was removed.
- Ciphertext that fails authentication is **quarantined**, never partially rendered. A record that
  cannot be trusted is not shown as if it could be.

Batches are capped at 100 records and 64 KB of ciphertext each.

## Data modes

An installation is in one of three modes, and the client asks which on startup.

| Mode | Meaning |
| --- | --- |
| `legacy` | Data is stored in the original plaintext tables |
| `claiming` | A migration to encrypted storage is in progress; the app routes you to the claim page |
| `encrypted` | All data goes through the encrypted vault |

## Migrating existing data: the legacy claim

Existing plaintext data is **not** handed to whoever registers first. It is claimed deliberately,
by a user the operator names.

1. An expand migration adds the new tables without touching the plaintext rows.
2. The operator enables claim mode and names the user who owns the existing data.
3. The API issues that user a single-use token valid for **30 minutes**. Every route that would
   change plaintext is blocked meanwhile, and the notifications worker pauses.
4. That user's unlocked browser downloads the dataset once, validates it, encrypts everything,
   uploads it, then verifies the record count and a canonical manifest hash.
5. Only after verification succeeds is the token marked consumed.

If any step fails, the plaintext is untouched and the installation stays in claim mode — you can
retry. A separate, reviewed contract migration drops the plaintext columns later, once you are
satisfied.

The operator procedure is in
[`api/docs/runbooks/legacy-claim-rehearsal.md`](../api/docs/runbooks/legacy-claim-rehearsal.md).

## Recovery, and the honest warning

There is no operator master key and no support reset. If you lose every way of unlocking your
vault, the data cannot be recovered by anyone. Keep a recovery file or a second registered device.
See [Users and access](users-and-access.md).
