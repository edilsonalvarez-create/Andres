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

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
    CMD curl -fsS http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "QAGuardian.API.dll"]
