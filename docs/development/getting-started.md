# Getting started

## Requirements

- Docker for PostgreSQL and the API containers.
- Node 24 for the web client; .NET 10 SDK to run the API on the host.
- A passkey-capable browser and authenticator with the PRF extension for the current registration flow.

## Start

From the repository root:

```bash
make start
```

The command starts PostgreSQL, migrations, the API, the worker, and the web development server. The API uses `http://localhost:4000`; the client uses `http://localhost:5173`.

| Command | Purpose |
| --- | --- |
| `make start api` | Start the API containers |
| `make start web` | Start the web server |
| `make start dev` | Run the API and web client on the host with hot reload |
| `make stop` | Stop containers |
| `make logs` | Follow container logs |
| `make build` | Build images and the web bundle |
| `make test` | Run the .NET tests; Docker is required |

`make start dev` frees ports 4000 and 5173 before starting. `make start` copies `api/.env.example` to `api/.env` if needed.

## Current limitation

The client requests `/api/v1/claim/status` to choose a projection, but the API no longer exposes that endpoint. The unlock gate cannot select a projection against the current API, so the financial screens are blocked until this mismatch is fixed. See [web and sync](../architecture/web-and-sync.md).

## Register

1. Open `http://localhost:5173` and select **Create one**.
2. Enter an email and an optional passkey name.
3. Select **Create account** and complete the passkey prompt.

Registration can be `Open`, `InviteOnly`, or `Closed`. See [deployment](../operations/deployment.md) for configuration and [security](../architecture/security.md) for the server's access to data.
