# syntax=docker/dockerfile:1

# Applies migrations and exits. Runs as its own deployment step so no application instance ever
# migrates itself -- see docs/adr/0004-migrations-are-a-deployment-step.md.
#
# The stanza up to `dotnet restore` is byte-identical to docker/api/Dockerfile on purpose; see the
# note there about the shared BuildKit cache.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Xpense/Xpense.sln ./
COPY src/Xpense/Xpense.Domain/Xpense.Domain.csproj Xpense.Domain/
COPY src/Xpense/Xpense.Persistence/Xpense.Persistence.csproj Xpense.Persistence/
COPY src/Xpense/Xpense.API/Xpense.API.csproj Xpense.API/
COPY src/Xpense/Xpense.Tests/Xpense.Tests.csproj Xpense.Tests/
RUN dotnet restore Xpense.sln

COPY src/Xpense/ ./

# `dotnet ef` comes from .config/dotnet-tools.json, so the manifest is the single place the version
# is pinned -- the same one used to scaffold migrations locally.
#
# The bundle is self-contained: the runtime is inside the binary, so the final image needs no SDK,
# no .NET runtime and no source tree. That means naming a target runtime, and it has to follow the
# architecture actually being built for -- a hardcoded x64 RID produces a binary that dies on an
# Apple Silicon machine with "failed to open elf at /lib/ld-musl-x86_64.so.1". TARGETARCH is
# supplied by BuildKit; musl rather than glibc because the final image is Alpine-based.
ARG TARGETARCH
RUN case "${TARGETARCH}" in \
      amd64) rid=linux-musl-x64 ;; \
      arm64) rid=linux-musl-arm64 ;; \
      *) echo "unsupported TARGETARCH: ${TARGETARCH}" >&2; exit 1 ;; \
    esac \
 && dotnet tool restore \
 && dotnet dotnet-ef migrations bundle \
      --project Xpense.Persistence \
      --startup-project Xpense.Persistence \
      --configuration Release \
      --self-contained --target-runtime "${rid}" \
      --output /app/efbundle

# runtime-deps, not runtime: a self-contained binary carries its own .NET but still needs the
# native libraries (libstdc++, ICU) that this image exists to provide.
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine AS final
WORKDIR /app

RUN adduser -D -u 1001 xpense
USER xpense

COPY --from=build /app/efbundle ./efbundle

# Compose appends `--connection <string>`. XpenseContextFactory hardcodes Host=localhost for
# design time, which inside a container means this container, so the value is always passed in.
ENTRYPOINT ["./efbundle"]
