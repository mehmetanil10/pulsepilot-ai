# syntax=docker/dockerfile:1

ARG DOTNET_VERSION=10.0
ARG DOTNET_SDK_DIGEST=sha256:e1ffd2a92ae84c1291bc1b6887501f8af98e6331e7af6d4c8d37168c5e87a64c
ARG DOTNET_RUNTIME_DIGEST=sha256:f5b3b2e2e548828d50e349726f51a5de001286f02c4bbde77db0dd34eb9f55ff

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}@${DOTNET_SDK_DIGEST} AS build
WORKDIR /source

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/PulsePilot.Domain/PulsePilot.Domain.csproj src/PulsePilot.Domain/
COPY src/PulsePilot.Application/PulsePilot.Application.csproj src/PulsePilot.Application/
COPY src/PulsePilot.Infrastructure/PulsePilot.Infrastructure.csproj src/PulsePilot.Infrastructure/
COPY src/PulsePilot.Api/PulsePilot.Api.csproj src/PulsePilot.Api/
COPY src/PulsePilot.Worker/PulsePilot.Worker.csproj src/PulsePilot.Worker/
COPY src/PulsePilot.HealthProbe/PulsePilot.HealthProbe.csproj src/PulsePilot.HealthProbe/
RUN dotnet restore src/PulsePilot.Api/PulsePilot.Api.csproj \
    && dotnet restore src/PulsePilot.Worker/PulsePilot.Worker.csproj \
    && dotnet restore src/PulsePilot.HealthProbe/PulsePilot.HealthProbe.csproj

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

FROM build AS health-probe-publish
RUN dotnet publish src/PulsePilot.HealthProbe/PulsePilot.HealthProbe.csproj \
    --configuration Release \
    --output /app/health-probe \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-noble-chiseled-extra@${DOTNET_RUNTIME_DIGEST} AS runtime-base
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_gcServer=1

USER app

FROM runtime-base AS final

ARG BUILD_CREATED=unknown
ARG BUILD_REVISION=unknown
ARG BUILD_VERSION=dev
LABEL org.opencontainers.image.created=$BUILD_CREATED \
      org.opencontainers.image.description="PulsePilot AI HTTP API" \
      org.opencontainers.image.revision=$BUILD_REVISION \
      org.opencontainers.image.source="https://github.com/mehmetanil10/pulsepilot-ai" \
      org.opencontainers.image.title="PulsePilot API" \
      org.opencontainers.image.version=$BUILD_VERSION

EXPOSE 8080

COPY --from=api-publish --chown=app:app /app/api ./
COPY --from=health-probe-publish --chown=app:app /app/health-probe /health-probe

HEALTHCHECK --interval=10s --timeout=3s --start-period=10s --retries=5 \
    CMD ["dotnet", "/health-probe/PulsePilot.HealthProbe.dll", "http://127.0.0.1:8080/health/ready"]

ENTRYPOINT ["dotnet", "PulsePilot.Api.dll"]

FROM runtime-base AS migration-final

ARG BUILD_CREATED=unknown
ARG BUILD_REVISION=unknown
ARG BUILD_VERSION=dev
LABEL org.opencontainers.image.created=$BUILD_CREATED \
      org.opencontainers.image.description="PulsePilot AI one-shot database migrator" \
      org.opencontainers.image.revision=$BUILD_REVISION \
      org.opencontainers.image.source="https://github.com/mehmetanil10/pulsepilot-ai" \
      org.opencontainers.image.title="PulsePilot Migration" \
      org.opencontainers.image.version=$BUILD_VERSION

COPY --from=api-publish --chown=app:app /app/api ./

ENTRYPOINT ["dotnet", "PulsePilot.Api.dll"]

FROM runtime-base AS worker-final

ARG BUILD_CREATED=unknown
ARG BUILD_REVISION=unknown
ARG BUILD_VERSION=dev
LABEL org.opencontainers.image.created=$BUILD_CREATED \
      org.opencontainers.image.description="PulsePilot AI feedback processing worker" \
      org.opencontainers.image.revision=$BUILD_REVISION \
      org.opencontainers.image.source="https://github.com/mehmetanil10/pulsepilot-ai" \
      org.opencontainers.image.title="PulsePilot Worker" \
      org.opencontainers.image.version=$BUILD_VERSION

COPY --from=worker-publish --chown=app:app /app/worker ./

ENTRYPOINT ["dotnet", "PulsePilot.Worker.dll"]

# Render builds the final Dockerfile stage for the free demo API. The API hosts
# feedback processing in-process there, while Compose continues to use the
# separate production-style API and Worker stages above.
FROM final AS render-final
