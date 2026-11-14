# API reference

## OpenAPI

The generated OpenAPI document is the authoritative description of routes, request and response schemas, and security requirements.

1. Run the API in Development.
2. Open `/swagger/v1/swagger.json` or the Swagger UI at the API root.

Swagger is not exposed outside Development.

## Authentication

- Protected reads require the `xpense.session` cookie.
- Protected mutations also require the paired `xpense.antiforgery` cookie and `X-Xpense-Antiforgery` header.

## Responses

- Errors use Problem Details.
- Inaccessible private resources return a neutral 404.
- Creates provide an absolute `Location` header.
- `GET /health` reports infrastructure health.

## Operations

The API exposes authentication, financial resources, analytics, notifications, groups, invitations, and sync. The generated document describes the current operations. Legacy claim and record-envelope operations have been removed.

See [API architecture](../architecture/api.md) for structural boundaries.
