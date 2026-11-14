---
status: accepted
date: 2026-08-04
---

# The generated OpenAPI document is the contract

## Decision

Use the generated OpenAPI document as the HTTP contract. Serve it at `/swagger/v1/swagger.json` in Development. Remove the obsolete hand-written OpenAPI files.

## Context

The hand-written description was stale and invalid: schemas were missing or unrelated, routes and responses differed from the implementation, resources were absent, and the server URL was invalid. The generated document reflects endpoint metadata and security requirements; contract tests check important requirements, statuses, and schemas.

## Alternatives considered

- **Repair the hand-written contract:** rejected because maintaining another description would repeat the drift.
- **Commit a generated snapshot in CI:** deferred until contract diffs justify the additional process.

## Consequences

Without a committed snapshot, contract changes appear in code rather than as a separate document diff. A generated snapshot can be added later.
