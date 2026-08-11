---
status: accepted
date: 2026-08-11
---

# Local-first is the requirement, not end-to-end encryption

Xpense stops encrypting records in the browser. The server stores plaintext again and can read it.
`web/src/crypto/`, `web/src/claim/`, the encrypted vault projection and the three data modes
(`legacy`, `claiming`, `encrypted`) are removed.

What stays is local-first: a local record store plus a durable write queue, so the app works with no
connection and the writes made offline arrive when one returns. That is the requirement — whether
internet exists or not, a user must reach their money.

Two things survive that look like part of the vault and are not. **Passkeys stay**: they are
authentication, not confidentiality, and `web/src/auth/authFlow.ts:90` already handles a
`prfUnsupported` result, so sign-in never depended on the PRF-derived wrapping key. **The sync
outbox stays** — `web/src/sync/outbox.ts`, `conflicts.ts`, `lifecycle.ts` — carrying plaintext
payloads. Record ids, revisions, tombstones, idempotency keys and the changes cursor are unchanged.

## Why

Local-first and end-to-end encryption shipped in the same release, so they read as one decision.
They are two. Local-first is about availability without a network. End-to-end encryption is about
who may read the stored bytes. Only the first was ever a requirement.

The second cost the server its ability to read anything, and the bill came in three parts.

- **The notification worker stopped working.** `Xpense.Notifications/Rules/BudgetExceededRule.cs`
  compares a period's spend against a limit. Neither number exists server-side when both are
  ciphertext, so the rule cannot run at all — not slowly, not partially.
  [ADR 0006](0006-a-budget-reports-and-never-blocks.md) put crossing detection in the consumer
  precisely because the consumer can read the data.
- **Analytics is blocked** for the same reason, and stays blocked for as long as the vault exists.
- **The domain is implemented twice** — a C# domain over plaintext and a TypeScript one over
  ciphertext — with three data modes bridging them and a client that must ask which mode it is in
  before it can render anything.

The measured size of what that costs to keep: `web/src/crypto/` is 1,141 lines with 1,140 lines of
tests beside it, and `web/src/claim/` is 1,309 with 875. The frontend carries three parallel
projections — `plaintextProjection.ts` 564, `fixtureProjection.ts` 492,
`encryptedVaultProjection.ts` 426 — and `transitionVaultProjection.ts` routing between them.

Maintainability decided it. Xpense has one maintainer, and his own reading of the vault is that it
adds too much complexity and sits above what he can develop and debug unaided. Code that cannot be
debugged by the only person who will ever debug it, protecting data that is unrecoverable by design
— no operator master key, no support reset — is a bad trade whichever way it fails.

The threat the vault actually addressed was a stolen database dump or a leaked backup.
[`docs/security-and-encryption.md`](../../../docs/security-and-encryption.md) is explicit that an
operator serving hostile JavaScript defeats the scheme entirely, and it is the same server that
serves the app. So the vault defended one threat, and encryption at rest plus encrypted backups
defends that same threat with operator tooling rather than with a second domain model. Today the
operator is the owner, and the owner is the only user.

Comparable projects landed in the same place. Firefly III has no client-side encryption and points
users at database access control and disk encryption instead. Actual Budget offers it as an
optional, off-by-default, per-budget feature, and documents the limit this decision is about: bank
sync tokens cannot be covered by it, because the server needs to use them.

## Considered options

**Keep the vault.** Rejected on everything above: it costs the notification worker, analytics and a
duplicated domain, permanently, to defend a threat that operator-side encryption already covers.

**Make encryption optional and off by default, per user — the Actual Budget shape.** The honest
compromise, and the right one for a hosted service with strangers on it. Rejected as the most code
of any option: both paths must exist, both must be tested, and every server-side feature must either
work twice or be absent for encrypted users. It keeps the duplicated domain, which is the expensive
part, and adds a switch on top.

**Keep the vault and move the server-side features into the client.** Derive budget alerts and
analytics in the browser, delete the worker. Rejected because it cannot cover everything: an
invitation and another member's activity are facts only the server holds. It also leaves the
two-domain problem entirely in place.

**Encrypt selected columns server-side with a key the server holds.** Cheap, and it looks like a
middle ground. Rejected because it protects nothing that database encryption at rest does not, while
adding a key to store, rotate and lose.

## Consequences

**The server can read your finances.** "The person running the server cannot read what you spent
your money on" is no longer true and must come out of the documentation rather than be softened in
it. The stated threat model becomes: the operator is trusted, and the database is defended by
encryption at rest, encrypted backups and access control.

**If Xpense ever becomes a paid service, optional end-to-end encryption has to be added back
deliberately**, with notifications and analytics designed around it from the start rather than
discovered to be impossible afterwards. That is a product decision with a real cost attached, not a
refactor, and this ADR is the thing to reverse when it is taken.

Nothing is in production, so there is no data to migrate out, no expand/contract dance, and no
retired key material to hold. The claim flow is deleted rather than run.

**Notifications split by origin.** Budget alerts derive client-side, which is where the data now
is — and a server cannot notify a client that is offline anyway. Anything only the server knows —
an invitation, another member's activity — arrives on sync. One feature, two producers.

The 409 conflict flow is unchanged: a stale update still returns the current record and the client
still chooses. The server could now compare the two versions itself, but nothing needs it to.

[ADR 0009](0009-authorization-is-an-explicit-query.md) holds, and one of its consequences changes
owner. Its last line hands the closing of per-user ownership on the plaintext tables to the vault
claim and migration flow. That flow is gone, so ownership is now an ordinary server-side change with
nothing in front of it, and its wording about operators inspecting ciphertext describes plaintext.

## Sources

- [Firefly III — Security](https://docs.firefly-iii.org/explanation/more-information/security/) — the
  database is not encrypted and its values are readable; disk encryption is the recommendation
- [Firefly III — End-to-End (Client-Side) Encryption, issue #2039](https://github.com/firefly-iii/firefly-iii/issues/2039)
- [Actual Budget — Syncing Across Devices](https://actualbudget.org/docs/getting-started/sync/) — end-to-end
  encryption is optional, per budget file, and off unless you turn it on
- [Actual Budget — Connecting Your Bank](https://actualbudget.org/docs/advanced/bank-sync/) — bank sync
  tokens live on the server and are outside the encryption, because the server must use them
