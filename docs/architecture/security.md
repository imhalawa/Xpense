# Security and privacy

Xpense is self-hosted and trusts the operator. Sync payloads are stored as plaintext JSON on the server, which can read financial data as well as identity and access metadata. Protect the database, backups, application secrets and Data Protection keys with access controls and encryption at rest. Use HTTPS for internet-facing installations.

Passkeys authenticate users. Session cookies protect authenticated requests; mutations also require a paired antiforgery cookie and header. Private resource access is checked explicitly against the user, group, membership and grant; unauthorized private resources return a neutral 404.

The client still contains vault and legacy claim code. Client-side record encryption is planned for removal; passkey authentication remains. Sync records are not end-to-end encrypted. The API's former legacy-claim and envelope endpoints have been removed.

See [users and sharing](../features/users-and-sharing.md) for available flows and [deployment](../operations/deployment.md) for operator configuration and backup requirements.
