---
status: accepted
date: 2026-08-09
---

# Authorization is an explicit query

## Decision

Authorize private resources in explicit queries starting from the current user. Correlate the resource, active membership, group, grant, and resource type. Return a neutral 404 when an authenticated caller cannot access a private resource; an absent or invalid session still receives 401.

## Context

Users can own resources, belong to multiple groups, and hold different roles and grants. A global filter hides the authorization path and can miss a relationship or the distinction between read and edit access.

## Alternatives considered

- **Global authorization filters:** rejected because permission depends on a multi-entity relationship and must be visible in each query.
- **A separate authorization handler per resource:** rejected because loading the resource still requires an authorization query.
- **Return 403 for known resources:** rejected because it reveals existence.

## Consequences

Queries repeat the authorization predicates. Tests must cover group isolation, revocation, deletion, and resource types. Final reads repeat decisive predicates to avoid time-of-check/time-of-use gaps. Visibility filters are not authorization controls.

At the time of the decision, legacy financial tables lacked per-user ownership. Operators could read payloads and access metadata in PostgreSQL.
