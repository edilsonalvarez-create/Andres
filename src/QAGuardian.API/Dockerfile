# ── Etapa de compilación ─────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/QAGuardian.Domain/QAGuardian.Domain.csproj src/QAGuardian.Domain/
COPY src/QAGuardian.Application/QAGuardian.Application.csproj src/QAGuardian.Application/
COPY src/QAGuardian.Infrastructure/QAGuardian.Infrastructure.csproj src/QAGuardian.Infrastructure/
COPY src/QAGuardian.API/QAGuardian.API.csproj src/QAGuardian.API/
RUN dotnet restore src/QAGuardian.API/QAGuardian.API.csproj

COPY src/ src/
RUN dotnet publish src/QAGuardian.API/QAGuardian.API.csproj -c Release -o /app/publish /p:UseAppHost=false

# ── Etapa de ejecución ───────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Node.js para los runners (Playwright y Newman se invocan vía npx)
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates gnupg \
    && curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && npm install -g newman @playwright/test \
    && npx playwright install --with-deps chromium \
    && apt-get clean && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Cliente Docker (sin daemon): lanza los sandboxes efímeros. Sprint 17-A (B1): ya NO
# se comunica con /var/run/docker.sock montado, sino con `docker-socket-proxy` vía
# DOCKER_HOST=tcp://... (allowlist de endpoints, red interna). Ver ADR-012.
COPY --from=docker:27-cli /usr/local/bin/docker /usr/local/bin/docker

# Non-root (Sprint 16-B): mismo UID:GID que usan los contenedores sandbox
# (Runners:SandboxUser=1000:1000), para no introducir un tercer identificador.
# Sprint 17-A: el API ya no monta el socket ni requiere `group_add`/DOCKER_GID;
# el acceso al daemon queda mediado por el proxy con allowlist (ver ADR-012).
RUN groupadd --gid 1000 qaguardian \
    && useradd --uid 1000 --gid 1000 --create-home --shell /usr/sbin/nologin qaguardian \
    && mkdir -p /app/storage/evidence /app/storage/runner-workspaces \
    && chown -R qaguardian:qaguardian /app

USER qaguardian
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
    CMD curl -fsS http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "QAGuardian.API.dll"]
