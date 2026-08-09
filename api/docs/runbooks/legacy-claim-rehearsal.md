# Legacy claim rehearsal

## Gate status

**NOT RUN. TASK 32 IS BLOCKED.**

This file is the procedure and evidence form for the destructive plaintext-removal gate. Do not
change this status until all checks below have been performed against a restored production dump,
with a real browser, a real passkey and the candidate release images.

The second gate is an independent cryptography-focused review. A successful rehearsal does not
replace that review. Neither gate authorises an automated agent to apply Task 32.

## Hard stops

- Never point a rehearsal API, migration command or browser at the production database.
- Prefer a separate scratch host or PostgreSQL cluster. If a shared cluster is unavoidable, require
  a database name ending in `_claim_rehearsal_<date>` and verify `current_database()` before every
  database command.
- Restore a dump into a new empty database. Never use `--clean` against production.
- Apply migrations only through `20260809090649_AddLegacyClaimTokens`. Task 32 must not be present
  in the rehearsal image or migration command.
- Use a private HTTPS scratch origin whose configured WebAuthn relying-party id matches the real
  passkey. Do not weaken WebAuthn origin or relying-party checks.
- Keep `LegacyClaim__Enabled=true` after a successful claim and through Task 32. This keeps legacy
  writes blocked and the notification worker paused.
- Keep `LegacyClaim__DataMode=legacy` during the claim. A consumed claim token makes the browser use
  encrypted mode. Set the durable mode to `encrypted` only in the reviewed Task 32 cutover.
- Do not re-enable the notification worker after Task 32 until plaintext financial notifications,
  their API slices, `BudgetExceededRule` and pending `TransactionRecorded` work have been retired.
  Invitation email delivery needs a separate preserved path.
- Store no claim token, passkey material, Data Protection key, financial marker or decrypted browser
  data in this report.

## Evidence header

Fill every value. A blank value keeps the gate closed.

| Evidence | Value |
| --- | --- |
| Rehearsal date and UTC window | `<not run>` |
| Operator | `<not run>` |
| Candidate commit | `<not run>` |
| API, web, migrations and worker image digests | `<not run>` |
| Production dump filename and SHA-256 | `<not run>` |
| Scratch host and database name | `<not run>` |
| Scratch origin and configured relying-party id | `<not run>` |
| Designated user UUID | `<not run>` |
| Browser name and version | `<not run>` |
| External review report, reviewer and reviewed commit | `<missing>` |

## 1. Prepare an isolated copy

Create a fresh dump with the existing backup command. Copy the dump to the isolated scratch host
through the operator's approved secure channel.

```bash
cd api
docker compose exec -T postgres backup.sh
sha256sum backups/xpense-<stamp>.dump
```

On the scratch host, choose a new database name and reject anything that does not contain the
rehearsal suffix.

```bash
export REHEARSAL_DATABASE=xpense_claim_rehearsal_<yyyymmdd>
export REHEARSAL_ADMIN_CONNECTION=postgresql://<scratch-admin>@<scratch-host>/postgres
export PGHOST=<scratch-host>
export PGPORT=5432
export PGUSER=<scratch-app>
export PGDATABASE=$REHEARSAL_DATABASE
read -rs REHEARSAL_ADMIN_PASSWORD
read -rs PGPASSWORD
export PGPASSWORD
export REHEARSAL_CONNECTION_STRING="Host=$PGHOST;Port=$PGPORT;Database=$PGDATABASE;Username=$PGUSER;Password=$PGPASSWORD"
case "$REHEARSAL_DATABASE" in
  *_claim_rehearsal_*) ;;
  *) exit 1 ;;
esac

PGPASSWORD="$REHEARSAL_ADMIN_PASSWORD" createdb \
  --maintenance-db "$REHEARSAL_ADMIN_CONNECTION" \
  --owner "$PGUSER" \
  "$REHEARSAL_DATABASE"
unset REHEARSAL_ADMIN_PASSWORD
pg_restore --exit-on-error --no-owner --no-privileges \
  --dbname "$PGDATABASE" /private/path/xpense-<stamp>.dump
ACTUAL_DATABASE=$(psql --tuples-only --no-align --command 'select current_database();')
test "$ACTUAL_DATABASE" = "$REHEARSAL_DATABASE"
psql --tuples-only --no-align --command 'select current_database(), inet_server_addr();'
```

`psql`, EF and the API connection string above are built from the same `PGHOST`, `PGPORT`,
`PGDATABASE`, `PGUSER` and `PGPASSWORD` values. The exact equality check must pass before migration
and must be repeated immediately before API startup. Record the server address, dump hash and restore
log. Configure a separate API, web origin and Data Protection directory for this database. Do not
reuse production ports, container names, volumes or public routing.

Apply only the expand migration target, then record the applied migration list and prove the model
has no pending changes.

```bash
cd api
dotnet tool restore
dotnet dotnet-ef database update 20260809090649_AddLegacyClaimTokens \
  --project src/Xpense/Xpense.Persistence \
  --startup-project src/Xpense/Xpense.Persistence \
  --connection "$REHEARSAL_CONNECTION_STRING"
dotnet dotnet-ef migrations has-pending-model-changes \
  --project src/Xpense/Xpense.Persistence \
  --startup-project src/Xpense/Xpense.Persistence
```

Before starting the claim, verify all of these:

- the designated user already exists and owns at least one usable passkey vault wrapper;
- the designated user owns no encrypted financial, taxonomy, budget or notification records;
- the API and browser use the candidate commit and the scratch connection string;
- the scratch notification worker has claim mode enabled and performs no event processing;
- registration and public access to the scratch origin are disabled;
- the nine source tables and `TransactionTags` are unchanged after restore.

## 2. Capture the source baseline

Counts include soft-deleted rows. Split transactions by `TransactionKind`: `0` and `1` are
`transaction`; `2` is `transfer`.

```sql
SELECT 'account' AS type, COUNT(*) FROM "Xpense"."Accounts"
UNION ALL SELECT 'necessityScale', COUNT(*) FROM "Xpense"."Priorities"
UNION ALL SELECT 'category', COUNT(*) FROM "Xpense"."Categories"
UNION ALL SELECT 'merchant', COUNT(*) FROM "Xpense"."Merchants"
UNION ALL SELECT 'tag', COUNT(*) FROM "Xpense"."Tags"
UNION ALL SELECT 'transaction', COUNT(*) FROM "Xpense"."Transactions" WHERE "Kind" <> 2
UNION ALL SELECT 'transfer', COUNT(*) FROM "Xpense"."Transactions" WHERE "Kind" = 2
UNION ALL SELECT 'budget', COUNT(*) FROM "Xpense"."Budgets"
UNION ALL SELECT 'notification', COUNT(*) FROM "Xpense"."Notifications"
ORDER BY 1;
```

Capture deterministic hashes without writing row contents to the report. Run this function before
the claim, after interruption and after completion. Keep only the three hash files; compare them with
`cmp`. `COPY` output is ordered by the primary key, includes every column and includes soft-deleted
rows.

```bash
set -euo pipefail
capture_source_hashes() {
  local output=$1
  : > "$output"
  hash_query() {
    local name=$1
    local query=$2
    local digest
    digest=$(psql --quiet --set ON_ERROR_STOP=1 --command "COPY ($query) TO STDOUT" | openssl dgst -sha256 | awk '{print $2}')
    test "${#digest}" -eq 64
    case "$digest" in
      *[!0-9a-f]*) return 1 ;;
    esac
    printf '%s %s\n' "$name" "$digest" >> "$output"
  }
  hash_query Accounts 'SELECT row_to_json(source)::text FROM (SELECT * FROM "Xpense"."Accounts" ORDER BY "Id") source'
  hash_query Priorities 'SELECT row_to_json(source)::text FROM (SELECT * FROM "Xpense"."Priorities" ORDER BY "Id") source'
  hash_query Categories 'SELECT row_to_json(source)::text FROM (SELECT * FROM "Xpense"."Categories" ORDER BY "Id") source'
  hash_query Merchants 'SELECT row_to_json(source)::text FROM (SELECT * FROM "Xpense"."Merchants" ORDER BY "Id") source'
  hash_query Tags 'SELECT row_to_json(source)::text FROM (SELECT * FROM "Xpense"."Tags" ORDER BY "Id") source'
  hash_query Transactions 'SELECT row_to_json(source)::text FROM (SELECT * FROM "Xpense"."Transactions" ORDER BY "Id") source'
  hash_query Budgets 'SELECT row_to_json(source)::text FROM (SELECT * FROM "Xpense"."Budgets" ORDER BY "Id") source'
  hash_query Notifications 'SELECT row_to_json(source)::text FROM (SELECT * FROM "Xpense"."Notifications" ORDER BY "Id") source'
  hash_query TransactionTags 'SELECT row_to_json(source)::text FROM (SELECT * FROM "Xpense"."TransactionTags" ORDER BY "TransactionId", "TagId") source'
}

capture_source_hashes source-before.sha256
```

Start the isolated API with:

- `LegacyClaim__Enabled=true`;
- `LegacyClaim__DesignatedUserId=<designated user UUID>`;
- `LegacyClaim__DataMode=legacy`;
- the scratch connection string;
- the private scratch HTTPS origin and the production-compatible relying-party id.

Sign in with the real passkey. Start the claim from `/claim`. After the dataset is downloaded, record
from `ClaimTokens` only the expected record count, expected type-count JSON, hexadecimal manifest
hash, source-content hash and timestamps. Never record the raw claim token or its hash.

## 3. Prove interruption is restartable

Use the same browser profile throughout this step. Enable browser network throttling so there is time
to observe a partial upload. Wait until the claimant owns more than zero and fewer than the expected
number of claimed records, then terminate the browser process. Do not press Cancel; this step
tests an abrupt loss.

Capture this interrupted-state evidence:

- `ClaimTokens.ConsumedAt` is null;
- claim mode still returns 409 for every blocked legacy financial mutation;
- the source counts and source hashes still equal the baseline;
- the partial encrypted record count is between zero and the expected count;
- every partial record id is unique;
- each partial record has exactly one personal envelope and no group envelope;
- every `SyncOperations.IdempotencyKey` beginning with `legacy-claim-v1:` is unique.

In the same fail-fast shell that defines `capture_source_hashes`, capture the interrupted snapshot only
after the browser process has stopped:

```bash
capture_source_hashes source-interrupted.sha256
cmp source-before.sha256 source-interrupted.sha256
```

Restart the same browser profile, sign in and unlock with the real passkey, then resume the claim. A
new short-lived bearer token is expected; deterministic record UUIDs and idempotency keys must reuse
the already uploaded ciphertext instead of creating a second record.

## 4. Verify the completed graph in both directions

Completion must return success and set `ClaimTokens.ConsumedAt`. Record these comparisons:

In the same fail-fast shell, capture the completed source snapshot only after completion succeeds:

```bash
capture_source_hashes source-completed.sha256
cmp source-before.sha256 source-completed.sha256
```

| Check | Source or pinned value | Encrypted value | Result |
| --- | ---: | ---: | --- |
| Total record count | `<not run>` | `<not run>` | `<not run>` |
| Account | `<not run>` | `<not run>` | `<not run>` |
| Necessity scale | `<not run>` | `<not run>` | `<not run>` |
| Category | `<not run>` | `<not run>` | `<not run>` |
| Merchant | `<not run>` | `<not run>` | `<not run>` |
| Tag | `<not run>` | `<not run>` | `<not run>` |
| Transaction | `<not run>` | `<not run>` | `<not run>` |
| Transfer | `<not run>` | `<not run>` | `<not run>` |
| Budget | `<not run>` | `<not run>` | `<not run>` |
| Notification | `<not run>` | `<not run>` | `<not run>` |
| Manifest SHA-256 | `<not run>` | `<not run>` | `<not run>` |

Map encrypted type numbers as follows: account `0`, transaction `1`, transfer `2`, category `3`,
merchant `4`, tag `5`, budget `6`, notification `7`, necessity scale `9`. Restrict every target query
to the designated `OwnerUserId`; ignore user-profile type `8`.

```sql
SELECT "RecordType", COUNT(*)
FROM "Xpense"."EncryptedRecords"
WHERE "OwnerUserId" = '<designated-user-uuid>'
  AND "RecordType" IN (0, 1, 2, 3, 4, 5, 6, 7, 9)
GROUP BY "RecordType"
ORDER BY "RecordType";

SELECT
  CASE "RecordType"
    WHEN 0 THEN 'account'
    WHEN 1 THEN 'transaction'
    WHEN 2 THEN 'transfer'
    WHEN 3 THEN 'category'
    WHEN 4 THEN 'merchant'
    WHEN 5 THEN 'tag'
    WHEN 6 THEN 'budget'
    WHEN 7 THEN 'notification'
    WHEN 9 THEN 'necessityScale'
  END || '|' || LOWER("Id"::text) AS identity
FROM "Xpense"."EncryptedRecords"
WHERE "OwnerUserId" = '<designated-user-uuid>'
  AND "RecordType" IN (0, 1, 2, 3, 4, 5, 6, 7, 9)
ORDER BY identity COLLATE "C";
```

Export `<type-name>|<lowercase-record-uuid>` ordered with ordinal/C collation. Use raw `COPY` output so
no heading, separator or row-count text enters the file. The shell remains fail-fast from the source
hash step.

```bash
psql --quiet --set ON_ERROR_STOP=1 --command "COPY (
  SELECT
    CASE \"RecordType\"
      WHEN 0 THEN 'account'
      WHEN 1 THEN 'transaction'
      WHEN 2 THEN 'transfer'
      WHEN 3 THEN 'category'
      WHEN 4 THEN 'merchant'
      WHEN 5 THEN 'tag'
      WHEN 6 THEN 'budget'
      WHEN 7 THEN 'notification'
      WHEN 9 THEN 'necessityScale'
    END || '|' || LOWER(\"Id\"::text) AS identity
  FROM \"Xpense\".\"EncryptedRecords\"
  WHERE \"OwnerUserId\" = '<designated-user-uuid>'::uuid
    AND \"RecordType\" IN (0, 1, 2, 3, 4, 5, 6, 7, 9)
  ORDER BY identity COLLATE \"C\"
) TO STDOUT" > target-identities.txt
```

Recreate the manifest without a trailing newline and hash it independently.

```bash
COUNT=$(awk 'END { print NR }' target-identities.txt)
{
  printf '%s' "$COUNT"
  if [ "$COUNT" -gt 0 ]; then
    printf '\n'
    awk 'NR > 1 { printf "\n" } { printf "%s", $0 }' target-identities.txt
  fi
} | openssl dgst -sha256
```

This manifest result must equal `ClaimTokens.ExpectedManifestHash`.

Export and hash the full target graph separately. The API completion check derives the same expected
id/type/tombstone/parent graph from the locked source snapshot and refuses any missing, extra or
mismatched row.

```sql
SELECT
  CASE "RecordType"
    WHEN 0 THEN 'account'
    WHEN 1 THEN 'transaction'
    WHEN 2 THEN 'transfer'
    WHEN 3 THEN 'category'
    WHEN 4 THEN 'merchant'
    WHEN 5 THEN 'tag'
    WHEN 6 THEN 'budget'
    WHEN 7 THEN 'notification'
    WHEN 9 THEN 'necessityScale'
  END || '|' || LOWER("Id"::text) || '|' || LOWER("IsDeleted"::text) || '|' ||
  COALESCE(LOWER("ParentResourceId"::text), '-') AS graph_identity
FROM "Xpense"."EncryptedRecords"
WHERE "OwnerUserId" = '<designated-user-uuid>'
  AND "RecordType" IN (0, 1, 2, 3, 4, 5, 6, 7, 9)
ORDER BY graph_identity COLLATE "C";
```

Export the graph through raw `COPY` and record its SHA-256. Do not attach decrypted payloads.

```bash
psql --quiet --set ON_ERROR_STOP=1 --command "COPY (
  SELECT
    CASE \"RecordType\"
      WHEN 0 THEN 'account'
      WHEN 1 THEN 'transaction'
      WHEN 2 THEN 'transfer'
      WHEN 3 THEN 'category'
      WHEN 4 THEN 'merchant'
      WHEN 5 THEN 'tag'
      WHEN 6 THEN 'budget'
      WHEN 7 THEN 'notification'
      WHEN 9 THEN 'necessityScale'
    END || '|' || LOWER(\"Id\"::text) || '|' || LOWER(\"IsDeleted\"::text) || '|' ||
    COALESCE(LOWER(\"ParentResourceId\"::text), '-') AS graph_identity
  FROM \"Xpense\".\"EncryptedRecords\"
  WHERE \"OwnerUserId\" = '<designated-user-uuid>'::uuid
    AND \"RecordType\" IN (0, 1, 2, 3, 4, 5, 6, 7, 9)
  ORDER BY graph_identity COLLATE \"C\"
) TO STDOUT" > target-graph.txt
openssl dgst -sha256 target-graph.txt
```

The graph hash is separate evidence and is not expected to equal the manifest hash. Then prove:

- the source baseline is unchanged;
- the encrypted id/type/tombstone/parent set exactly matches the deterministic source mapping;
- every encrypted row uses protocol version `1`, non-empty nonce and non-empty ciphertext;
- every record has exactly one non-empty protocol-`1` personal envelope;
- there are no extra claimed-type rows for the designated user;
- record ids, personal-envelope uniqueness and `legacy-claim-v1:` idempotency keys are unique;
- the interrupted and resumed runs did not add a second record or envelope.

Select several distinctive source labels, reasons and notification messages locally. Search every
text, JSON and decoded byte column in `EncryptedRecords`, `RecordEnvelopes`, `SharedResources` and
`SyncOperations`; every marker count must be zero. Do not copy the markers into the report. Record
only the inspected columns, query hash and zero-result counts.

## 5. Prove the encrypted product path

Keep claim mode enabled. Close and reopen the browser so no unlocked key survives. Unlock again with
the real passkey and verify Overview, Transactions and Budgets render from `/sync/changes` and
IndexedDB while all legacy routes are unavailable.

Use browser request blocking or an isolated reverse proxy to fail all requests under the legacy
Accounts, Transactions, Categories, Merchants, Tags, Budgets, Analytics, Priorities and Notifications
routes. Export the HAR. It must show no attempted legacy request from the encrypted application.

Before testing the application, prove the blocker is real. From the same browser origin, deliberately
request one safe read route in each blocked family with header `X-Rehearsal-Canary: 1`. The proxy or
browser log must record the expected block for all nine families. Clear the network log after the
canaries, load the encrypted application and capture a separate HAR. A missing canary block fails the
gate; an application request to any blocked family also fails the gate.

Through the normal UI:

1. create, edit and delete a personal account;
2. create and edit taxonomy, including an exact category-priority change;
3. create, edit and delete an income or expense without changing its immutable root account;
4. create and delete a budget;
5. reload, unlock again and confirm the surviving changes render;
6. lock during a sync pull and confirm decrypted content disappears from the DOM.

The network must contain only ciphertext, nonces, envelopes and metadata for sync mutations. Obtain a
fresh antiforgery pair for each mutation. Inspect IndexedDB and the server database for the same
plaintext markers; all ciphertext stores and tables must remain marker-free. Group spaces and
plaintext financial notifications are not approved by this rehearsal.

## 6. Failure and rollback procedure

If any check fails:

1. keep claim mode enabled and the notification worker paused;
2. do not run Task 32 and do not delete plaintext rows;
3. retain the scratch dump, logs, HAR and non-secret hashes for diagnosis;
4. discard the scratch browser profile and scratch database after evidence is secured;
5. fix the candidate, restore a fresh copy of the same production dump and repeat the whole rehearsal.

The production installation is unchanged by this rehearsal. A failed scratch run is not repaired by
editing encrypted rows by hand.

## 7. External cryptography review gate

Attach an independent report that names the reviewer, reviewed commit, scope, findings, resolutions
and explicit release verdict. Its scope must cover protocol versioning, AAD, key hierarchy, passkey
PRF, recovery wrappers, Worker isolation, personal and group envelopes, sync parsing, quarantine,
network leakage and lock cleanup.

| Gate | Status | Evidence |
| --- | --- | --- |
| Production-copy claim rehearsal | **NOT PASSED** | `<missing>` |
| External cryptography review | **NOT PASSED** | `<missing>` |
| Task 32 authorised | **NO** | Both rows above must be passed by named humans |

Even after both rows pass, Task 32 is written and reviewed first. An operator applies it by hand to a
scratch restore, reruns the plaintext-removal inspection, and only then schedules the production
cutover. An autonomous agent never applies that migration.
