# 🛡️ QA Guardian

**Plataforma empresarial de QA automatizado**: el *quality gate* que decide si un despliegue
llega o no a producción. Ejecuta pruebas funcionales, de regresión, de API, de rendimiento,
de seguridad y de base de datos; genera evidencia automática; diagnostica fallos con IA
y bloquea el merge/despliegue cuando la calidad no cumple el umbral.

## Arquitectura

- **Backend**: .NET 9, ASP.NET Core, Clean Architecture + DDD + CQRS (MediatR), EF Core
  (SQL Server / SQLite), Redis, Hangfire, SignalR, Serilog.
- **Frontend**: React 18 + TypeScript + Material UI + Tailwind (Vite).
- **Integraciones**: Playwright, Postman/Newman, JMeter, OWASP ZAP, SonarQube, GitHub,
  GitHub Actions, Azure DevOps, SQL Server.
- **IA**: agente de diagnóstico de fallos y generación de casos de prueba sobre la
  Messages API de Anthropic (`claude-opus-4-8`, salida estructurada JSON) con
  degradación heurística cuando no hay API key.

```
src/
  QAGuardian.Domain          → Entidades, enums y reglas de negocio (DDD)
  QAGuardian.Application     → Casos de uso CQRS, validaciones, puertos
  QAGuardian.Infrastructure  → EF Core, runners, clientes externos, IA, reportes
  QAGuardian.API             → Controllers REST, JWT/RBAC, SignalR, Swagger
tests/                       → Pruebas unitarias e integración (38 en verde)
frontend/                    → SPA React (dashboard, proyectos, ejecuciones…)
database/                    → Scripts SQL (esquema, índices, seed, demo)
pipelines/                   → Plantilla de quality gate para proyectos cliente
docs/                        → Manuales técnico, usuario, instalación y arquitectura
```

## Inicio rápido (desarrollo)

```bash
# Backend (SQLite + Hangfire en memoria: no requiere SQL Server)
dotnet run --project src/QAGuardian.API   # http://localhost:5080  (swagger en /swagger)

# Frontend
cd frontend && npm install && npm run dev  # http://localhost:5173
```

Credenciales iniciales: `admin@qaguardian.local` / `QaGuardian.2026!` (cámbielas de inmediato).

## Inicio rápido (Docker)

```bash
cp .env.example .env    # complete los secretos
docker compose up -d    # API en :5080, frontend en :8081
docker compose --profile tools up -d   # agrega SonarQube (:9000) y ZAP (:8090)
```

## Verificación

```bash
dotnet test QAGuardian.sln          # 38 pruebas (unitarias + integración)
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

## Licencia

Uso interno — Sumimedical.
