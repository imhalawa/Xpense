# syntax=docker/dockerfile:1

# The build context is the repository root, not this directory -- the build needs the whole
# solution. Compose passes `context: .` with `dockerfile: docker/api/Dockerfile`.
#
# The stanza up to `dotnet restore` is byte-identical to docker/migrations/Dockerfile on purpose.
# BuildKit keys its cache on layer content rather than on which Dockerfile produced it, so the
# second image reuses this restore instead of repeating it. Keep the two in step: if they drift,
# the builds still work, they just quietly stop sharing and get slower.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Xpense/Xpense.sln ./
COPY src/Xpense/Xpense.Domain/Xpense.Domain.csproj Xpense.Domain/
COPY src/Xpense/Xpense.Persistence/Xpense.Persistence.csproj Xpense.Persistence/
COPY src/Xpense/Xpense.API/Xpense.API.csproj Xpense.API/
COPY src/Xpense/Xpense.Tests/Xpense.Tests.csproj Xpense.Tests/
RUN dotnet restore Xpense.sln

COPY src/Xpense/ ./
RUN dotnet publish Xpense.API/Xpense.API.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

# Nothing in this image writes to disk or binds a privileged port, so it has no reason to be root.
RUN adduser -D -u 1001 xpense
USER xpense

COPY --from=build /app ./

# Kestrel binds 8080 inside; compose publishes it on the host as 4000.
EXPOSE 8080
ENTRYPOINT ["dotnet", "Xpense.API.dll"]
