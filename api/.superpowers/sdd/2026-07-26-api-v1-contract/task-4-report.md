# Task 4: Analytics v1 Route Slice

## Scope completed

- Moved the analytics summary endpoint from `GET /api/analytics/today/categories` to `GET /api/v1/analytics/spending/by-category`.
- Preserved the existing use case and direct `TodayExpensesByCategoryResponse` payload; no date-range behavior was added.
- Added integration coverage for the direct v1 summary payload and removal of the legacy route.

## Verification

Attempted the required serial command:

```text
DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1 '/mnt/c/Program Files/dotnet/dotnet.exe' test src/Xpense/Xpense.Tests/Xpense.Tests.csproj --filter FullyQualifiedName~V1AnalyticsEndpointTests -m:1 -p:BuildInParallel=false
```

It could not start because the available `dotnet.exe` reports that no .NET SDKs are installed. `git diff --check` completed without output.

## Concern

The red and green integration runs need to be repeated in an environment with a .NET 8 SDK before relying on the test result.
