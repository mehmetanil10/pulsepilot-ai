# syntax=docker/dockerfile:1

ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /source

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/PulsePilot.Domain/PulsePilot.Domain.csproj src/PulsePilot.Domain/
COPY src/PulsePilot.Application/PulsePilot.Application.csproj src/PulsePilot.Application/
COPY src/PulsePilot.Infrastructure/PulsePilot.Infrastructure.csproj src/PulsePilot.Infrastructure/
COPY src/PulsePilot.Api/PulsePilot.Api.csproj src/PulsePilot.Api/
COPY src/PulsePilot.Worker/PulsePilot.Worker.csproj src/PulsePilot.Worker/
RUN dotnet restore src/PulsePilot.Api/PulsePilot.Api.csproj \
    && dotnet restore src/PulsePilot.Worker/PulsePilot.Worker.csproj

COPY src/ src/

FROM build AS api-publish
RUN dotnet publish src/PulsePilot.Api/PulsePilot.Api.csproj \
    --configuration Release \
    --output /app/api \
    --no-restore \
    /p:UseAppHost=false

FROM build AS worker-publish
RUN dotnet publish src/PulsePilot.Worker/PulsePilot.Worker.csproj \
    --configuration Release \
    --output /app/worker \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
WORKDIR /app

USER root
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

COPY --from=api-publish --chown=app:app /app/api ./

USER app

HEALTHCHECK --interval=10s --timeout=3s --start-period=10s --retries=5 \
    CMD curl --fail --silent --show-error --max-time 2 \
        http://127.0.0.1:8080/health/ready || exit 1

ENTRYPOINT ["dotnet", "PulsePilot.Api.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS worker-final
WORKDIR /app

ENV DOTNET_EnableDiagnostics=0

COPY --from=worker-publish --chown=app:app /app/worker ./

USER app

ENTRYPOINT ["dotnet", "PulsePilot.Worker.dll"]
