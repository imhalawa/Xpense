# Xpense identity, groups, invitations, and sharing

Date: 2026-08-08
Status: written for user review
Scope: account authentication, server sessions, groups, invitations, resource grants, and hosted-ready boundaries
Depends on: `2026-08-08-zero-knowledge-vault-and-recovery-design.md` for key envelopes
Supersedes: `2026-08-07-auth-and-users-design.md`

## Outcome

One Xpense server may host unrelated users and families. Each user has a private personal space and
may belong to several groups. A family is represented by a group; family relationships such as
husband, wife, or child are not roles in the software.

Authentication proves who is using the API. Encryption separately proves whether that client can
open financial content. An authenticated server session must never be treated as a vault key.

## Chosen approach

Xpense uses ASP.NET Core Identity on .NET 10 for user storage, passkey authentication, and secure
cookie sessions. Microsoft documents passwordless account creation and sign-in in .NET 10, while
also warning that its passkey support is for authentication rather than general WebAuthn use. The
client therefore performs the WebAuthn PRF operation used for vault unlock and sends only the
assertion fields required for authentication to the API. See
[Microsoft's passkey guidance](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/passkeys/?view=aspnetcore-10.0).

Registration is open by default. Deployment configuration may change it to invite-only or closed,
but there is no special first-registered owner. The one-time claim of today's global data is a
separate operator-authorised process assigned to a designated test user.

## Identity model

Server-visible identity data:

| Record | Required fields |
|---|---|
| User | UUID, normalized unique email, optional recovery-password hash, account state, timestamps |
| User profile | encrypted display name and preferences |
| Passkey credential | credential id, public key, transports, counter, backup flags, display label |
| User encryption identity | public encryption key, encrypted private-key payload |
| Session | Identity cookie and standard security-stamp state |

Emails are visible to the server because sign-in, invitation targeting, abuse controls, and email
delivery require them. Display names and financial preferences are encrypted. Initials are derived
after unlock; before unlock the UI shows the email prefix.

Users may register several passkeys. Deleting the final usable vault wrapper is refused unless a
recovery wrapper already exists. Passkey labels and last-used times are visible in account settings.
Account creation is committed only after the client supplies its encryption public key, encrypted
private key, and at least one valid User Master Key wrapper. A short-lived pending registration may
perform the follow-up assertion needed by credentials that do not return PRF output during creation;
expired pending registrations leave no usable user account.

## Authentication and session rules

- Passkeys are the normal registration and sign-in method, with user verification required.
- A configured recovery password or recovery file can authenticate when every passkey is lost; both
  also provide a separate local vault wrapper.
- Successful authentication creates an HttpOnly, Secure, SameSite=Lax cookie. Production requires
  HTTPS and an explicit WebAuthn relying-party domain.
- Mutating requests require an antiforgery token in addition to the cookie.
- Login, registration, invitation inspection, and recovery endpoints are rate-limited.
- Authentication failures do not reveal whether an email exists.
- Sign-out revokes the session and asks the browser vault to lock first.
- A user whose passkey signs in but cannot produce a usable vault wrapper is authenticated but
  remains in the `Vault locked` state.

The public authentication surface is:

| Endpoint | Purpose |
|---|---|
| `POST /api/v1/auth/register/options` | Create short-lived registration options |
| `POST /api/v1/auth/register` | Verify the passkey ceremony and complete account creation |
| `POST /api/v1/auth/passkey/options` | Create short-lived assertion options |
| `POST /api/v1/auth/passkey/sign-in` | Verify the assertion and set the cookie |
| `POST /api/v1/auth/password/sign-in` | Recovery-password authentication when configured |
| `POST /api/v1/auth/recovery/sign-in` | Consume a recovery-file authentication token |
| `POST /api/v1/auth/logout` | Revoke the session and clear the cookie |
| `GET /api/v1/auth/me` | Return identity, group summaries, and vault-wrapper availability |
| `GET/POST/DELETE /api/v1/users/me/passkeys` | Manage additional credentials safely |

Challenges are single-use, expire after five minutes, and are bound to the intended ceremony. The
relying-party domain is configured explicitly; it is never inferred from an unvalidated Host
header.

## Group model

| Record | Required fields |
|---|---|
| Group | UUID, owner user id, encrypted name, created/updated timestamps |
| Group membership | group id, user id, role, state, encrypted group-key envelope |
| Group invitation | UUID, inviter, optional target email, token hash, state, expiry |
| Resource grant | resource id, group id, permission, state, key-envelope reference |

A group has exactly one Owner and any number of Members. The Owner manages membership, invitations,
and ownership transfer. Ownership does not grant access to a member's private personal space.
Before leaving, the Owner must transfer ownership or delete the empty group.

Membership and financial permission are different:

- Membership allows the user to see the group in the space switcher and receive a group-key
  envelope.
- A resource grant allows the group to access one financial resource.
- `Viewer` reads the shared resource.
- `Editor` reads it and may create, edit, or soft-delete related records.
- The resource's personal owner retains full control and is the only person who can add or remove
  its group grant.

## Sharing rules

Accounts and budgets are shared explicitly; nothing becomes visible merely because users share a
group.

Sharing an account grants the group access to:

- The account record.
- Transactions touching that account.
- The category, merchant, and tag labels needed to render those transactions.

A transfer is readable when the user can read at least one involved account. An inaccessible
counterparty is displayed as `Private account`. Creating, editing, or deleting a transfer requires
Editor access to both involved accounts.

A budget has its own grant. A budget shared to a group counts only transactions from accounts that
are also shared to that same group. Private-account spending never contributes to a shared budget,
even if it uses the same category label.

Removing a grant blocks API access immediately and starts client-side key rotation for future data.
It cannot erase plaintext or ciphertext that a former member already saved. The UI states this
before revocation.

## Invitation flow

Invitation links are time-limited, single-use, and valid for seven days. The server stores a hash
of the bearer token, never the token itself.

For an existing user, the Owner's unlocked client encrypts the group key to the invitee's public
key when creating an invitation bound to that user's normalized email. Acceptance can then complete
immediately. An open link that is not bound to an existing user always requires Owner approval.

For a new or unknown user:

1. The invitation link opens registration or sign-in.
2. The recipient creates an encryption identity and requests membership.
3. The request becomes `Awaiting owner approval`.
4. The next unlocked Owner client approves it and creates the group-key envelope.
5. The recipient can enter the group after the envelope syncs.

The extra approval prevents the server from ever holding the group key or an invitation decryption
secret. Possessing an emailed link alone cannot decrypt family data.

## Email delivery

Invitation creation always returns a copyable Xpense link. Delivery is an optional adapter:

- A hosted installation configures an email provider and sends the link.
- A self-hosted installation may configure SMTP.
- Without an adapter, invitation creation still succeeds and reports `Link ready to share`.

Email contains no financial data, group key, recovery secret, or passkey material. A
`GroupInvitationCreated` event contains only the invitation id. The delivery worker loads the
server-visible address and a short-lived delivery payload encrypted with ASP.NET Core Data
Protection, records delivery status, deletes that payload after the final attempt, and follows the
existing Events-table queue rules. The payload contains only the bearer link, which cannot decrypt
the group and still requires sign-in plus an existing key envelope or Owner approval. Delivery
failure never cancels the invitation.

## Authorization boundary

The API authorizes server-visible metadata before returning or accepting ciphertext. Every query
starts from the authenticated user and one of these proofs:

- The user owns the record.
- The user is an active member of a group holding an active Viewer or Editor grant.
- The operation is group administration and the user is that group's Owner.

EF global filters are not the primary security boundary because access is no longer a simple
`UserId` equality. Each vertical slice performs an explicit authorization query against ownership,
membership, and grants. Shared authorization predicates may live in a dedicated infrastructure
service, but feature request/response types remain isolated under the existing architecture rules.

The server validates permission, ciphertext size, record type, parent resource identifiers, and
revision. It cannot validate encrypted amounts, labels, balances, dates, or budget rules. Those
domain checks run in the trusted client.

## Error behaviour

- Missing authentication returns 401.
- Authenticated but unauthorized access returns 404, not 403, so record existence is not disclosed.
- Stale revisions return 409 with the latest encrypted revision.
- A consumed, expired, or revoked invitation returns one neutral invalid-invitation response.
- A pending invitation returns its current state without exposing the group's encrypted name until
  the recipient has a key envelope.
- Email failure returns a successful invitation plus a delivery warning and copyable link.

All RFC 7807 responses use prose validation messages and stable error codes for client handling.

## Hosted-product boundaries

- Server operator, billing owner, group Owner, and resource owner are separate concepts.
- No API gives a deployment operator an implicit group membership or financial grant.
- Registration policy, relying-party domain, public URL, and email adapter are configuration.
- UUIDs are used for all new externally visible identities; database integer ids do not cross the
  new contract.
- Billing may later attach to a user or group without changing membership or encryption ownership.
- Billing tables, plans, checkout, and operator dashboards are not implemented in this phase.

## Testing

- Passkey registration, sign-in, multiple credentials, logout, challenge expiry, replay rejection,
  recovery-password and recovery-file sign-in, and neutral unknown-user failures.
- Cookie flags, antiforgery enforcement, rate limits, and relying-party domain validation.
- Permission matrix for private owner, group Viewer, group Editor, group Owner without a grant,
  revoked member, and unrelated user.
- Users belonging to several groups with no cross-group grant leakage.
- Transfer access and hidden private counterparties.
- Shared-budget calculations excluding private accounts.
- Existing-user invitation, new-user pending approval, expiry, one-time use, owner transfer, and
  membership revocation.
- Email adapter success, disabled, and failure paths without invitation loss.
- Architecture tests continue to enforce one endpoint per file and slice isolation.

## Acceptance criteria

- Open registration creates unrelated private users; it never claims existing financial rows.
- A group Owner cannot read a Member's personal records.
- A group can receive Viewer or Editor access to an account or budget explicitly.
- Revocation prevents future API reads immediately and rotates keys before new content is written.
- Invitation links work without email, and hosted email can deliver the same link.
- The server never receives a group key, user master key, recovery secret, or WebAuthn PRF output.
- The model leaves billing possible without coupling it to family permissions.

## Out of scope

- Family-specific roles or parental controls.
- Automatic contact discovery.
- Social login and OAuth providers.
- Billing implementation and deployment-operator UI.
- Native clients.
