# QA Guardian — Release Notes `1.0.0-rc.1`

**Fecha:** 2026-07-16  
**Tipo:** Release Candidate (Enterprise)  
**Versión:** `1.0.0-rc.1`  
**Rama de referencia:** `master`

## Resumen

Primera Release Candidate Enterprise de QA Guardian. Consolida sprints de seguridad, performance, IA y capacidades premium (trazabilidad, aprobaciones, notificaciones, auditoría) sobre la plataforma de quality gates y ejecución multi-runner.

## Highlights

- Quality Gates con UI, evaluación y bloqueo de despliegue
- Ejecución multi-framework (Playwright, Postman, JMeter, ZAP, Selenium IDE, visual)
- Dashboard ejecutivo con KPIs, tendencias, heatmap y exportación PDF/Excel
- Motor IA (Anthropic Sonnet por defecto) con caché, confidence y grounding de PRs
- Matriz de trazabilidad Req → Historia → Caso → Resultado → Defecto
- Flujo de aprobaciones humanas (activación, release, gate override)
- Admin de canales de notificación (Email / Teams / Slack / Discord / Telegram)
- Página de auditoría y roles RBAC
- Optimizaciones de performance (code splitting, N+1, compresión Brotli, SignalR)
- Docker Compose + health checks liveness/readiness + CI en `main`/`master`

## Cambios por área

### Seguridad
- Semilla de admin sin contraseña pública fuera de Development
- Cabeceras de seguridad, HSTS, rate limiting, JWT + refresh httpOnly
- Cifrado de tokens de integración, auditoría HTTP

### Performance
- Lazy routes + `manualChunks` (Vite)
- Queries con `AsNoTracking` / split queries / batch dashboard & quality gates
- Compresión de respuestas API y cache inmutable de assets nginx

### IA
- Modelo configurable (`claude-sonnet-4-5`), thinking off por defecto
- Fingerprint cache, confidence anti-alucinación, diff de PR + catálogo
- Benchmarks unitarios Sprint 7

### Enterprise (Sprint 8)
- `/trazabilidad`, `/aprobaciones`, `/notificaciones`, `/auditoria`
- API coverage + approvals + link caso↔historia
- Dashboard filtrable por proyecto

### Release Candidate (Sprint 9)
- Versionado `1.0.0-rc.1` (`Directory.Build.props`, `VERSION`, `/api/v1/version`)
- `/health/live` y `/health/ready` (DB)
- CI: tests frontend + ramas `master`
- Compose: API healthcheck + frontend espera API healthy

## Validación RC (ejecutada)

| Suite | Resultado |
|-------|-----------|
| Unit tests (.NET Release) | **178 passed** |
| Frontend Vitest | **25 passed** |
| API build Release | OK (tras liberar proceso) |
| Frontend `tsc` | OK (Sprint 8) |

## Breaking / notas operativas

- En **QA/Production**, `Seed:AdminPassword` **no** puede ser `QaGuardian.2026!`
- Docker exige `.env` con `SQL_SA_PASSWORD`, `JWT_SIGNING_KEY`, `ENCRYPTION_KEY`, `ADMIN_PASSWORD`
- Desarrollo local: `launchSettings` puede usar credenciales documentadas solo en Development
- Multiempresa (tenant) completo queda fuera de RC; aislamiento por proyecto ACL es backlog

## Artefactos de despliegue

```bash
docker compose up -d --build
curl -fsS http://localhost:5080/health/live
curl -fsS http://localhost:5080/health/ready
curl -fsS http://localhost:5080/api/v1/version
```

Frontend Docker: http://localhost:8081  
API: http://localhost:5080  

## Próximo paso

Promover a `1.0.0` tras completar [GO-LIVE-CHECKLIST.md](./GO-LIVE-CHECKLIST.md) en ambiente QA/staging.
