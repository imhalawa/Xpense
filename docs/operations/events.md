# Event operations

Run these commands from `api/`. The notification worker has no HTTP health endpoint. Check its logs and the event queue to assess processing.

## Inspect the queue

List events awaiting processing:

```bash
docker compose exec -T postgres psql -U xpense -d xpense \
  -c 'select "EventId", "Type", "Attempts", "LastError" from "Xpense"."Events" where "ProcessedAt" is null;'
```

List events with recorded errors:

```bash
docker compose exec -T postgres psql -U xpense -d xpense \
  -c 'select "EventId", "Type", "Attempts", "LastError", "ProcessedAt" from "Xpense"."Events" where "LastError" is not null;'
```

An event with both `ProcessedAt` and `LastError` has exhausted its attempts. There is no separate dead-letter table.

## Retry an event

After diagnosing the error, replace `EVENT_ID` with the event ID and reset its processing state:

```bash
docker compose exec -T postgres psql -U xpense -d xpense \
  -c 'update "Xpense"."Events" set "ProcessedAt" = null, "Attempts" = 0 where "EventId" = '\''EVENT_ID'\'';'
```

In-app notifications are deduplicated by event ID and payload hash. Review the event and its side effects before retrying, especially when it triggers email.

## Invitation email

The API and worker must share the Data Protection key directory. Keep the keys private and back them up with the database. Enable email with `XPENSE_EMAIL_ENABLED` and configure SMTP as described in [deployment](deployment.md).

A successful or disabled delivery clears its protected link payload. A delivery that fails on the final attempt is marked failed and its payload is cleared.

## Polling

The worker waits one second when idle and ten seconds after a pump error. A full batch is followed immediately by another batch. Processing can take longer than one second when the worker is busy or retries fail.
