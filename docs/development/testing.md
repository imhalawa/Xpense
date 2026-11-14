# Testing

## Required API checks

Before finishing, run from `api/`:

```bash
dotnet build src/Xpense/Xpense.sln
dotnet test src/Xpense/Xpense.sln
```

Keep the build warning-free. `ApiEndpointTests` is the route-level safety net for refactors; change it only when deliberately changing the HTTP contract. Integration tests use PostgreSQL through Testcontainers and require Docker.

## Web checks

From `web/`, run the relevant checks after installing dependencies:

- `npm run build`: type-check and bundle.
- `npm test`: unit and component tests.
- `npm run test:browser`: browser tests.
- `npm run check:init`: entry-module initialization.

See [getting started](getting-started.md) for setup and the root `Makefile` for combined commands.
