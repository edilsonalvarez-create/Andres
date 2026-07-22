# Checklist Go Live — QA Guardian `1.0.0-rc.1` → `1.0.0`

Usar este checklist en el ambiente de preproducción antes de promover a producción.
Procedimiento paso a paso de deploy/rollback: [DEPLOY-ROLLBACK.md](./DEPLOY-ROLLBACK.md).

## 0. Precondiciones

- [ ] RC `1.0.0-rc.1` desplegado en staging/QA
- [ ] Ventana de cambio aprobada (CAB / Product Owner)
- [ ] Responsable de rollback identificado
- [ ] Backup de base de datos tomado
- [ ] Pipeline CI verde en el commit a desplegar: `backend` → `frontend` → `e2e` (smoke Playwright) → `docker` → `deploy` (ver `.github/workflows/ci.yml`)
- [ ] Imágenes publicadas en GHCR para ese `sha` (`ghcr.io/<owner>/qaguardian-api:<sha>`, `qaguardian-frontend:<sha>`)

## 1. Configuración y secretos

- [ ] `.env` / secret manager sin valores de ejemplo
- [ ] `SQL_SA_PASSWORD` / connection string de producción
- [ ] `JWT_SIGNING_KEY` ≥ 32 caracteres, único por ambiente
- [ ] `ENCRYPTION_KEY` ≥ 32 caracteres, único por ambiente
- [ ] `ADMIN_PASSWORD` única (≠ `QaGuardian.2026!`)
- [ ] `ANTHROPIC_API_KEY` configurada o documentado modo heurístico
- [ ] `ASPNETCORE_ENVIRONMENT=Production` (o `QA` en preprod)
- [ ] `Cors__AllowedOrigins` apunta solo al frontend real (HTTPS)
- [ ] `APP_DB_USER` / `APP_DB_PASSWORD` (usuario SQL de mínimo privilegio) configurados; `SQL_SA_PASSWORD` **no** se usa en la connection string del API (Sprint 16-B)

## 2. Deployment / Docker

- [ ] `docker compose -f docker-compose.yml up -d --build` **sin** `docker-compose.override.yml` (ese archivo es solo para desarrollo local)
- [ ] Imágenes etiquetadas (`qaguardian-api:1.0.0-rc.1`, frontend igual)
- [ ] Volúmenes `sqldata` / `evidence` persistentes
- [ ] **Migraciones EF aplicadas fuera del proceso API** (`migrate` compose o `.\scripts\Migrate-Database.ps1` / `dotnet ef database update`) **antes** de tráfico
- [ ] Confirmado: API con `ASPNETCORE_ENVIRONMENT=Production` (o QA/Staging) **sin** `Migrate`/`EnsureCreated` en boot (`Database__ApplyMigrationsOnStartup` no efectivo fuera de Development)
- [ ] Seed admin solo si corresponde (`Database__SkipInitialization=false` + secretos Seed); si el DBA ya sembró, `SkipInitialization=true`
- [ ] **Usuario SQL de mínimo privilegio** creado (`database/00-app-user.sql` o servicio compose `db-init`); el API se conecta con él, no con `sa`
- [ ] **Puertos DB/Redis no publicados al host** (`docker compose -f docker-compose.yml config` no debe mostrar `ports` para `sqlserver`/`redis`)
- [ ] **Imágenes API/frontend corren non-root** (`docker exec <contenedor> id` → UID 1000 en `api`, UID 101 en `frontend`)
- [ ] Frontend responde en URL pública
- [ ] API responde en URL pública (sin Swagger en producción)

## 3. Health & monitoreo

- [ ] `GET /health/live` → Healthy
- [ ] `GET /health/ready` → Healthy (DB)
- [ ] `GET /api/v1/version` → `1.0.0-rc.1`
- [ ] Alertas configuradas si `/health/ready` falla (opcional: uptime monitor)
- [ ] Logs Serilog accesibles / retenidos
- [ ] Hangfire dashboard restringido a Administrador

## 4. Testing smoke (post-deploy)

- [ ] Login admin
- [ ] Dashboard carga KPIs
- [ ] Crear/listar proyecto
- [ ] Catálogo: módulo + requerimiento + historia
- [ ] Caso de prueba + vincular historia
- [ ] Trazabilidad muestra cobertura
- [ ] Ejecutar smoke / ver SignalR progress
- [ ] Quality Gate visible
- [ ] Crear defecto
- [ ] Canal de notificación de prueba (opcional)
- [ ] Solicitud de aprobación + decide (TechLead/PO)
- [ ] Auditoría muestra escrituras recientes

## 5. Seguridad

- [ ] HTTPS terminado (reverse proxy / ingress)
- [ ] Cookies refresh `Secure` + `HttpOnly` en prod
- [ ] Rate limiting activo
- [ ] Usuario no-admin no accede a `/admin/users`
- [ ] Evidencias no listables anónimamente
- [ ] Revisar [Security Report](../Security/Security-Report.md) hallazgos abiertos

## 6. Performance / UX

- [ ] Login first paint: chunks lazy (Network tab)
- [ ] Dashboard < 3s en staging con datos demo
- [ ] Compresión `Content-Encoding: br` en JSON
- [ ] Navegación menú: Trazabilidad / Aprobaciones / Notificaciones / Auditoría visibles
- [ ] Tema claro/oscuro usable

## 7. Documentación

- [ ] [Manual Instalación](../Manual-Instalacion.md) actualizado para el ambiente
- [ ] [Release Notes](./RELEASE-NOTES-1.0.0-rc.1.md) comunicadas al equipo
- [ ] Runbook de rollback leído por on-call ([DEPLOY-ROLLBACK.md](./DEPLOY-ROLLBACK.md))

## 8. Rollback

Si falla Go Live, seguir [DEPLOY-ROLLBACK.md §3](./DEPLOY-ROLLBACK.md#3-rollback):

1. [ ] Detener tráfico al nuevo release (`docker compose stop api frontend` o rollback de ingress)
2. [ ] Restaurar imágenes/tag anterior conocidos buenos (`docker pull ghcr.io/<owner>/qaguardian-api:<sha-anterior>`)
3. [ ] Restaurar backup DB si hubo migración incompatible
4. [ ] Verificar `/health/ready` del release previo
5. [ ] Comunicar incidente y abrir postmortem

Comandos típicos (detalle completo en DEPLOY-ROLLBACK.md):

```bash
docker compose -f docker-compose.yml stop api frontend
docker pull ghcr.io/<owner>/qaguardian-api:<sha-anterior>
docker pull ghcr.io/<owner>/qaguardian-frontend:<sha-anterior>
docker compose -f docker-compose.yml up -d api frontend
```

## 9. Sign-off

| Rol | Nombre | Fecha | OK |
|-----|--------|-------|----|
| Release Manager | | | [ ] |
| Tech Lead | | | [ ] |
| Product Owner | | | [ ] |
| Seguridad / Auditor | | | [ ] |

**Decisión:** ☐ Go Live `1.0.0` ☐ Hold ☐ Rollback
