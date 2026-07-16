# Checklist Go Live — QA Guardian `1.0.0-rc.1` → `1.0.0`

Usar este checklist en el ambiente de preproducción antes de promover a producción.

## 0. Precondiciones

- [ ] RC `1.0.0-rc.1` desplegado en staging/QA
- [ ] Ventana de cambio aprobada (CAB / Product Owner)
- [ ] Responsable de rollback identificado
- [ ] Backup de base de datos tomado

## 1. Configuración y secretos

- [ ] `.env` / secret manager sin valores de ejemplo
- [ ] `SQL_SA_PASSWORD` / connection string de producción
- [ ] `JWT_SIGNING_KEY` ≥ 32 caracteres, único por ambiente
- [ ] `ENCRYPTION_KEY` ≥ 32 caracteres, único por ambiente
- [ ] `ADMIN_PASSWORD` única (≠ `QaGuardian.2026!`)
- [ ] `ANTHROPIC_API_KEY` configurada o documentado modo heurístico
- [ ] `ASPNETCORE_ENVIRONMENT=Production` (o `QA` en preprod)
- [ ] `Cors__AllowedOrigins` apunta solo al frontend real (HTTPS)

## 2. Deployment / Docker

- [ ] `docker compose up -d --build` sin errores
- [ ] Imágenes etiquetadas (`qaguardian-api:1.0.0-rc.1`, frontend igual)
- [ ] Volúmenes `sqldata` / `evidence` persistentes
- [ ] Migraciones EF / init DB completados
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
- [ ] Runbook de rollback leído por on-call

## 8. Rollback

Si falla Go Live:

1. [ ] Detener tráfico al nuevo release (`docker compose stop api frontend` o rollback de ingress)
2. [ ] Restaurar imágenes/tag anterior conocidos buenos
3. [ ] Restaurar backup DB si hubo migración incompatible
4. [ ] Verificar `/health/ready` del release previo
5. [ ] Comunicar incidente y abrir postmortem

Comandos típicos:

```bash
# Revertir a tag/imagen anterior
docker compose down
# redeploy imagen previa, ej. qaguardian-api:<sha-anterior>
docker compose up -d
```

## 9. Sign-off

| Rol | Nombre | Fecha | OK |
|-----|--------|-------|----|
| Release Manager | | | [ ] |
| Tech Lead | | | [ ] |
| Product Owner | | | [ ] |
| Seguridad / Auditor | | | [ ] |

**Decisión:** ☐ Go Live `1.0.0` ☐ Hold ☐ Rollback
