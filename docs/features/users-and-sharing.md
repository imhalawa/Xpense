# Users and sharing

Users register and sign in with passkeys. The browser currently includes an unlock gate and vault-related flows; the server stores sync payloads as plaintext. See [security and privacy](../architecture/security.md) for the trust boundary.

Registration can be configured as open, invite-only or closed. The API also provides recovery-file and recovery-password sign-in and passkey management endpoints; their presence does not imply that every flow has a client screen.

Groups have an owner and members. Resource grants allow a group access to selected resources with a viewer or editor role. Invitations let an owner invite someone by email; when email is disabled, the invitation link can be passed on manually. The group, grant and invitation endpoints are available through the API, but the current client does not expose their full management flows.

Private resource queries check the caller's authorization and return a neutral 404 when access is denied. Session-authenticated mutations require the antiforgery cookie and header. Consult the generated [API contract](../development/api-reference.md) for the available operations.
