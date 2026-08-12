---
status: accepted
date: 2026-08-09
---

# Authorization is an explicit query

Xpense authorizes private data with explicit EF queries that begin at the current user. There is no global authorization query filter.

## Why

A user may own a resource, belong to several groups, hold different roles in each group, or receive a viewer or editor grant. A global filter cannot express all of those paths without hiding important differences between read and edit access. It also makes a query look safe when a new relationship was never added to the filter.

Each decision therefore correlates the current user, non-deleted group, active membership, matching resource type and active grant in one SQL query. Transfer authorization checks every distinct involved account. Group ownership requires both matching owner metadata and one active Owner membership.

## Privacy contract

An authenticated caller who cannot see a private resource receives the same empty 404 as a missing resource. This applies to unrelated, revoked, deleted and cross-group data. Feature code never returns 403 for these decisions. The server still uses 401 for an absent or invalid session.

## Considered options

**EF global query filters.** Rejected because the permission is a user-membership-grant-resource join, not a property of one row. A missing authorization predicate would also look like an ordinary empty result and become harder to review.

**One `IAuthorizationHandler` per resource.** Rejected because the handler needs the resource loaded before it can decide. That load is the authorization query, only moved away from the slice that must shape it correctly.

**Return 403 for known private resources.** Rejected because it confirms that the record exists. The neutral 404 makes a permission problem and a genuine missing record intentionally indistinguishable to a client; server logs and tests must preserve the operational difference.

## Consequences

- Queries repeat authorization predicates deliberately.
- Authorization tests must cover multi-group leakage, revoked state, deleted groups and mismatched resource types.
- Reads must repeat the decisive predicates in their final data query to close time-of-check/time-of-use gaps.
- Global visibility filters may hide ordinary soft-deleted rows, but they are not authorization controls.
- Operators can read record payloads and access metadata in PostgreSQL, but cannot use an application query path to bypass these rules.
- Legacy plaintext accounts, transactions, budgets, categories, merchants and tags require a session but still lack per-user ownership. Until that is closed, self-hosters must treat those tables as operator-readable shared legacy data.
- [ADR 0010](0010-local-first-is-the-requirement-not-end-to-end-encryption.md) deleted the encrypted-vault claim and migration flow that used to own closing that ownership gap. Nothing stands in front of it now: it is an ordinary server-side change, and it has no owner until one is assigned.
