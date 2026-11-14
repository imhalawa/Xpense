---
status: accepted
date: 2026-08-04
supersedes: the "URLs use the database-backed id" rule in docs/contract/api-v1-contract-design.md
---

# Account number is the public identifier

## Decision

Use `AccountNumber` in public routes, requests, and responses. Keep the database `Id` as the internal primary and foreign key.

## Context

The previous contract exposed database IDs. A public number is easier to use when identifying another user's account, and the transaction and transfer creation requests previously used different identifiers.

## Alternatives considered

- **Expose `Id`:** rejected because it exposes an implementation detail to users.
- **Use numbers in bodies and IDs in routes:** rejected because one resource would have two addressing schemes.
- **Expose both:** rejected because clients could depend on either identifier.

## Consequences

Account numbers were sequential and therefore guessable. Cross-user operations need authorization and consideration of non-sequential identifiers. Unused account equality code was removed. The earlier contract rule using database IDs was superseded.
