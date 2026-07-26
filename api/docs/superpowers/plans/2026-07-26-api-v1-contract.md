# API v1 Contract Reset Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the legacy API routes with a tested `/api/v1` contract.

**Architecture:** Controllers expose API DTOs only and delegate to existing use cases. A dedicated integration-test factory replaces SQL Server with isolated SQLite so endpoint tests exercise routing, validation, serialization, and persistence together.

**Tech Stack:** .NET 8, ASP.NET Core controllers, EF Core, NUnit, FluentAssertions, WebApplicationFactory, SQLite.

## Global Constraints

- Remove legacy API routes; do not run compatibility routes in parallel.
- Use plural `/api/v1` resource paths and RFC 7807 problem details.
- Use `{ cents, currency }` for public money values and ISO 8601 UTC timestamps.
- Add a failing test before each production-code change.
- Run tests serially with `DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1 dotnet test ... -m:1 -p:BuildInParallel=false`.

---

### Task 1: Integration-test host

**Files:**
- Modify: `src/Xpense/Xpense.API/Program.cs`
- Modify: `src/Xpense/Xpense.Tests/Xpense.Tests.csproj`
- Create: `src/Xpense/Xpense.Tests/Infrastructure/WebApiTestFactory.cs`
- Test: `src/Xpense/Xpense.Tests/Infrastructure/WebApiTestFactoryTests.cs`

- [ ] Write a failing test that creates the factory and requests a v1 route.
- [ ] Verify it fails because no `WebApiTestFactory` or v1 route exists.
- [ ] Add `Microsoft.AspNetCore.Mvc.Testing` and `Microsoft.EntityFrameworkCore.Sqlite`; expose `partial class Program` and replace the production context registration with a SQLite in-memory context in the factory.
- [ ] Verify the factory test passes and production SQL Server is not contacted.
- [ ] Commit the test-host foundation.

### Task 2: V1 account, category, tag, and merchant resources

**Files:**
- Modify: `src/Xpense/Xpense.API/Controllers/AccountController.cs`
- Modify: `src/Xpense/Xpense.API/Controllers/CategoryController.cs`
- Modify: `src/Xpense/Xpense.API/Controllers/TagController.cs`
- Modify: `src/Xpense/Xpense.API/Controllers/MerchantController.cs`
- Create: `src/Xpense/Xpense.Tests/Integration/V1ResourceEndpointTests.cs`

- [ ] Write failing integration tests for plural v1 routes, `201` creation, and legacy-route `404` responses.
- [ ] Verify they fail against the legacy routes.
- [ ] Move routes to `/api/v1/...`, use resource IDs in path parameters, return `CreatedAtAction` and `NoContent` where applicable, and remove response envelopes.
- [ ] Verify route and status tests pass.
- [ ] Commit the resource-route slice.

### Task 3: Unified transaction and transfer contract

**Files:**
- Modify: `src/Xpense/Xpense.API/Controllers/TransactionController.cs`
- Create: `src/Xpense/Xpense.API/Models/Requests/CreateTransactionRequest.cs`
- Create: `src/Xpense/Xpense.API/Models/Responses/V1TransactionResponse.cs`
- Create: `src/Xpense/Xpense.Tests/Integration/V1TransactionEndpointTests.cs`
- Test: `src/Xpense/Xpense.Tests/Unit/CreateTransactionRequestTests.cs`

- [ ] Write failing unit and integration tests for `POST /api/v1/transactions` with `income` and `expense` types and for filtered paging.
- [ ] Verify the tests fail because the unified contract does not exist.
- [ ] Map the public request to the existing deposit/withdraw use cases, return a single response DTO, and return validation problem details for unsupported types.
- [ ] Add the separate `POST /api/v1/transfers` contract only when its use case is implemented; otherwise return no transfer route in this slice.
- [ ] Verify tests pass and commit the transaction slice.

### Task 4: Analytics, error normalization, and full regression

**Files:**
- Modify: `src/Xpense/Xpense.API/Controllers/AnalyticsController.cs`
- Modify: `src/Xpense/Xpense.API/Helpers/XpenseController.cs`
- Test: `src/Xpense/Xpense.Tests/Integration/V1AnalyticsAndErrorsTests.cs`

- [ ] Write failing integration tests for `GET /api/v1/analytics/spending/by-category` and RFC 7807 validation/not-found errors.
- [ ] Verify expected failures.
- [ ] Move analytics to the v1 route and replace custom success/error envelopes with the agreed contracts.
- [ ] Run the complete serial test suite, inspect warnings and failures, and commit the completed reset.
