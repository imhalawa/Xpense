# Project structure

## Repository

| Path | Responsibility |
| --- | --- |
| `api/` | API, domain, persistence, notification worker, tests, and container services |
| `web/` | Browser client |
| `docs/` | Project documentation |

## API

The solution is under `api/src/Xpense/`.

| Path | Responsibility |
| --- | --- |
| `Xpense.API/Features/` | Endpoint slices, grouped by feature |
| `Xpense.API/Contracts/` | HTTP response contracts shared across features |
| `Xpense.API/Infrastructure/` | Endpoint discovery, validation, and other API infrastructure |
| `Xpense.API/ExceptionHandlers/` | Domain exception to HTTP error mappings |
| `Xpense.Domain/` | Entities, value objects, enums, domain events, and exceptions |
| `Xpense.Persistence/` | Database context, mappings, migrations, and persistence helpers |
| `Xpense.Notifications/` | Event processing, notification rules, and delivery |
| `Xpense.Tests/` | Endpoint, integration, unit, and architecture tests |

`api/docker/` contains service Dockerfiles. `api/docker-compose.yml` defines PostgreSQL, migrations, the API, and the notification worker. See [API architecture](../architecture/api.md).

## Web client

The client source is under `web/src/`.

| Path | Responsibility |
| --- | --- |
| `auth/`, `pages/`, `shell/` | Authentication flows, pages, navigation, and application shell |
| `components/`, `fluent/`, `theme/` | Shared interface components and styling |
| `clients/` | API clients and HTTP types |
| `domain/`, `money/`, `budgets/`, `transactions/` | Client-side financial rules and feature logic |
| `sync/`, `vault/` | Local projections, storage, sync, and conflict handling |
| `crypto/`, `claim/` | Remaining cryptographic and legacy claim code |
| `notifications/` | Client notification behavior |
| `contexts/`, `hooks/`, `utils/`, `typings/` | Shared client state, hooks, utilities, and types |

`App.tsx` composes the application routes and providers; `main.tsx` starts the client. See [web and sync](../architecture/web-and-sync.md) for the current integration limitation.
