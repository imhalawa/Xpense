# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Xpense/Xpense.sln ./
COPY src/Xpense/Xpense.Domain/Xpense.Domain.csproj Xpense.Domain/
COPY src/Xpense/Xpense.Persistence/Xpense.Persistence.csproj Xpense.Persistence/
COPY src/Xpense/Xpense.API/Xpense.API.csproj Xpense.API/
COPY src/Xpense/Xpense.Notifications/Xpense.Notifications.csproj Xpense.Notifications/
COPY src/Xpense/Xpense.Tests/Xpense.Tests.csproj Xpense.Tests/
RUN dotnet restore Xpense.sln

COPY src/Xpense/ ./
RUN dotnet publish Xpense.Notifications/Xpense.Notifications.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS final
WORKDIR /app

RUN adduser -D -u 1001 xpense
USER xpense

COPY --from=build /app ./

ENTRYPOINT ["dotnet", "Xpense.Notifications.dll"]
