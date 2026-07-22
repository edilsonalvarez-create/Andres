# 🛡️ QA Guardian

**Plataforma empresarial de QA automatizado**: el *quality gate* que decide si un despliegue
llega o no a producción. Ejecuta pruebas funcionales, de regresión, de API, de rendimiento,
de seguridad y de base de datos; genera evidencia automática; diagnostica fallos con IA
y bloquea el merge/despliegue cuando la calidad no cumple el umbral.

## Arquitectura

- **Backend**: .NET 9, ASP.NET Core, Clean Architecture + DDD + CQRS (MediatR), EF Core
  (SQL Server / SQLite), Redis, Hangfire, SignalR, Serilog.
- **Frontend**: React 18 + TypeScript + Material UI + Tailwind (Vite).
- **Integraciones (implementadas)**: Playwright, Postman/Newman, JMeter, OWASP ZAP,
  SonarQube, GitHub (PRs / checks / Actions YAML), SQL Server (validación BD).
- **No implementado** (no shipped): Azure DevOps, Jira, Xray, qTest, TestRail.
- **IA**: agente de diagnóstico/generación (Anthropic, default `claude-sonnet-4-5`)
  con caché por fingerprint, confidence y degradación heurística sin API key.
- **Release:** `1.0.0-rc.1` — ver [Release Notes](docs/Release/RELEASE-NOTES-1.0.0-rc.1.md)
  y [Go Live Checklist](docs/Release/GO-LIVE-CHECKLIST.md).

```
src/
  QAGuardian.Domain          → Entidades, enums y reglas de negocio (DDD)
  QAGuardian.Application     → Casos de uso CQRS, validaciones, puertos
  QAGuardian.Infrastructure  → EF Core, runners, clientes externos, IA, reportes
  QAGuardian.API             → Controllers REST, JWT/RBAC, SignalR, Swagger
tests/                       → Unitarias (166) + integración (11) + mutación (Stryker)
frontend/                    → SPA React (dashboard, proyectos, ejecuciones…)
database/                    → Scripts SQL (esquema, índices, seed, demo)
pipelines/                   → Plantilla de quality gate para proyectos cliente
docs/                        → Manuales técnico, usuario, instalación y arquitectura
```

## Inicio rápido (desarrollo)

```bash
# Secretos locales (una sola vez; nunca se versionan — ver docs/Manual-Instalacion.md §2)
cd src/QAGuardian.API
dotnet user-secrets set "Jwt:SigningKey" "genere-una-clave-aleatoria-de-al-menos-32-caracteres"
dotnet user-secrets set "Security:EncryptionKey" "genere-otra-clave-aleatoria-para-cifrado-de-tokens"
dotnet user-secrets set "Seed:AdminEmail" "admin@qaguardian.local"
dotnet user-secrets set "Seed:AdminPassword" "elija-una-contraseña-unica-para-su-equipo"
cd ../..

# Backend (SQLite + Hangfire en memoria: no requiere SQL Server)
dotnet run --project src/QAGuardian.API   # http://localhost:5080  (swagger en /swagger)

# Frontend
cd frontend && npm install && npm run dev  # http://localhost:5173
```

Credenciales iniciales: la app **no siembra un admin con contraseña por defecto**. Si
`Seed:AdminEmail` / `Seed:AdminPassword` faltan, o si la contraseña coincide con un valor
documentado públicamente, el arranque falla con un mensaje explícito.

## Inicio rápido (Docker)

```bash
cp .env.example .env    # complete los secretos
docker compose up -d    # API en :5080, frontend en :8081
docker compose --profile tools up -d   # agrega SonarQube (:9000) y ZAP (:8090)
```

Producción (imágenes ya publicadas en GHCR por CI, sin `build:` local): use
`docker-compose.prod.yml` — ver [Deploy & Rollback](docs/Release/DEPLOY-ROLLBACK.md).

## Verificación

```bash
dotnet test QAGuardian.sln          # 177 pruebas (166 unitarias + 11 integración)
cd frontend && npm run test         # 25 pruebas (Vitest)
cd frontend && npm run build        # typecheck estricto + build
```

## Documentación

| Documento | Contenido |
|---|---|
| [Manual de Instalación](docs/Manual-Instalacion.md) | Requisitos, despliegue local, Docker y producción |
| [Manual de Usuario](docs/Manual-Usuario.md) | Uso de la plataforma por rol |
| [Manual Técnico](docs/Manual-Tecnico.md) | API, integraciones, configuración y extensión |
| [Manual de Arquitectura](docs/Manual-Arquitectura.md) | Capas, modelo ER, decisiones y seguridad |
| [Casos de Prueba](docs/Casos-de-Prueba.md) | Suite de aceptación de la propia plataforma |
| [Threat Model](docs/Security/Threat-Model.md) | Modelo de amenazas STRIDE |
| [Security Report](docs/Security/Security-Report.md) | Auditoría OWASP Top 10 2025 y remediación (Sprint 2) |
| [Security Checklist](docs/Security/Security-Checklist.md) | Checklist operativo por categoría OWASP, para cada release/PR |
| [Pentest Checklist](docs/Security/Pentest-Checklist.md) | Guía de pruebas manuales de penetración |
| [Regression Tests](docs/Security/Regression-Tests.md) | Tests automatizados y manuales que fijan cada corrección de seguridad |
| [UX/UI Audit](docs/UX/UX-UI-Audit.md) | Auditoría Nielsen + WCAG 2.2 de cada pantalla (Sprint 3) |
| [Design System](docs/UX/Design-System.md) | Componentes compartidos, tokens de tema y justificación de cada decisión |

| [Technical Debt Audit](docs/Architecture/Technical-Debt-Audit.md) | Deuda técnica real (SRP, OCP, duplicación) y refactors aplicados (Sprint 4) |
| [Diagramas de arquitectura](docs/Architecture/Diagrams.md) | Diagramas Mermaid antes/después de los refactors de Sprint 4 |
| [Testing Strategy](docs/Testing/Testing-Strategy.md) | Pirámide de pruebas, cobertura medida y los 10 tipos de test (Sprint 5) |
| ADRs 8-10 | Ver tabla de decisiones en [Manual de Arquitectura](docs/Manual-Arquitectura.md#5-decisiones-de-arquitectura-adr-resumidas) |

## Licencia

Uso interno — Sumimedical.
