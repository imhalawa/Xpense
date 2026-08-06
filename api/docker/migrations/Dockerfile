# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Xpense/Xpense.sln ./
COPY src/Xpense/Xpense.Domain/Xpense.Domain.csproj Xpense.Domain/
COPY src/Xpense/Xpense.Persistence/Xpense.Persistence.csproj Xpense.Persistence/
COPY src/Xpense/Xpense.API/Xpense.API.csproj Xpense.API/
COPY src/Xpense/Xpense.Tests/Xpense.Tests.csproj Xpense.Tests/
RUN dotnet restore Xpense.sln

COPY src/Xpense/ ./

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

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine AS final
WORKDIR /app

RUN adduser -D -u 1001 xpense
USER xpense

COPY --from=build /app/efbundle ./efbundle

ENTRYPOINT ["./efbundle"]
