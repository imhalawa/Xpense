# Task 3b report: atomic transfer domain model

## Status

Complete. `POST /api/v1/transfers` is exposed only after the transfer use case,
atomic persistence boundary, correlated-leg model, SQL Server migration, and
focused tests were implemented.

## Contract and domain decisions

- The public request uses v1 resource identifiers:
  `sourceAccountId`, `destinationAccountId`, `amount: { cents, currency }`,
  optional `reason`, and optional ISO 8601 `occurredAt`.
- A `Transfer` aggregate stores the source and destination account IDs, exact
  integer cents, currency, reason, and occurrence timestamp.
- Each aggregate owns exactly two `TransferLeg` rows: one debit leg for the
  source account and one credit leg for the destination account. Both legs
  repeat the exact cents and currency and carry the same `TransferId`, making
  the movement directly auditable.
- A unique database index on `(TransferId, Direction)` prevents duplicate
  debit or credit legs for one transfer. Foreign keys to both accounts use
  restrictive deletion; transfer-to-leg deletion cascades.
- The repository opens a serializable database transaction before account
  lookup and validation, then persists both balance changes, the aggregate,
  and its legs in one `SaveChangesAsync` call before commit. Any exception is
  rolled back.
- Validation rejects nonpositive amounts, identical accounts, missing/deleted
  accounts, and transfers exceeding the source balance. Amount changes use
  `Money.ToSingle()`, which already preserves fractional cents conversion via
  decimal division.
- Only POST is exposed. Success is a direct `201` transfer representation; no
  GET route or fictional `Location` was added because the agreed resource list
  contains only `POST /api/v1/transfers`.
- Rejections use RFC 7807 responses: validation problems for invalid money,
  identical accounts, and insufficient funds; missing accounts return a 404
  problem response.

## Persistence

Added SQL Server migration `20260726153747_AddAtomicTransfers` and updated the
model snapshot. It creates `Xpense.Transfers` and `Xpense.TransferLegs`, their
foreign keys, and audit indexes. EF verification reports no model changes
pending after that migration.

## TDD evidence

1. Added `TransferTransactionUseCaseTests` before production types existed.
   The focused run failed to compile because `ITransferRepository` and
   `Transfer` did not exist, which was the expected RED state.
2. Implemented the domain/persistence slice and reached GREEN: 4/4 unit tests.
3. Added `V1TransferEndpointTests` while the route was still absent. The
   focused run failed 5/5 at HTTP 404, the expected RED state.
4. Added the POST request/response/controller only after domain tests were
   green; focused unit and endpoint tests then passed 9/9.
5. Added the missing-account contract and temporarily removed its handler to
   verify the test failed at HTTP 500; restoring the 404 problem handler made
   the final focused set pass 10/10.

The tests cover exact debit/credit balances, exact cents/currency persistence,
two correlated legs, identical-account rejection, insufficient-funds
rejection, simulated persistence failure with no database writes, invalid
money, missing accounts, direct response shape, and absence of partial writes.

## Verification

Commands used the requested SDK and serial build settings:

```text
DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1 \
  /home/atom/.local/share/dotnet-sdk-8/dotnet test \
  src/Xpense/Xpense.Tests/Xpense.Tests.csproj \
  -m:1 -p:BuildInParallel=false --no-restore \
  --filter "FullyQualifiedName~TransferTransactionUseCaseTests|FullyQualifiedName~V1TransferEndpointTests"

Passed: 10, Failed: 0, Skipped: 0
```

```text
DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1 \
  /home/atom/.local/share/dotnet-sdk-8/dotnet test \
  src/Xpense/Xpense.Tests/Xpense.Tests.csproj \
  -m:1 -p:BuildInParallel=false --no-restore

Passed: 49, Failed: 0, Skipped: 2, Total: 51
```

```text
dotnet-ef migrations has-pending-model-changes \
  --project Xpense.Persistence/Xpense.Persistence.csproj \
  --startup-project Xpense.Persistence/Xpense.Persistence.csproj \
  --context XpenseDbContext --no-build

No changes have been made to the model since the last migration.
```

The suite still emits pre-existing nullable/reference warnings and expected
SQLite schema warnings. No new compiler warning originates from the transfer
files.

## Remaining concerns

- Accounts currently have no currency field. The transfer aggregate and both
  legs preserve the requested currency exactly, but cross-account currency
  compatibility cannot be checked until the account model owns a currency.
- Production startup uses `EnsureCreated`; existing databases must apply the
  included migration through the deployment process because `EnsureCreated`
  does not upgrade an existing schema.
- Balance precision remains the existing account schema's two decimal places;
  public and audit values stay in integer cents, so the supported contract is
  exact to cents.
