# Deploy & Rollback — QA Guardian

Procedimiento concreto para el flujo `test → e2e smoke → deploy → rollback` del pipeline
[`ci.yml`](../../.github/workflows/ci.yml) (Sprint 16-C). Complementa el
[GO-LIVE-CHECKLIST](./GO-LIVE-CHECKLIST.md).

## 1. Qué hace CI automáticamente

```text
backend (test + cobertura) ─┐
                             ├─► e2e (Playwright smoke) ─► docker (build + tag) ─► deploy (push GHCR)
frontend (test + cobertura) ┘
```

- `backend` / `frontend`: compilan y corren pruebas con **piso de cobertura** (falla si la
  cobertura baja del umbral real — ver `tests/*/*.csproj` y `frontend/vite.config.ts`).
- `e2e`: levanta la API en `Development`/SQLite (sin SQL Server ni Redis) y corre
  `frontend/e2e/smoke.spec.ts` contra ella con Playwright/Chromium.
- `docker` (solo `push` a `main`/`master`): compila `qaguardian-api` y `qaguardian-frontend`,
  las etiqueta con `${{ github.sha }}` y las sube como artefacto del workflow.
- `deploy` (solo `main`/`master`, entorno `production` — configure aprobación manual en
  GitHub si se desea gate humano): publica esas mismas imágenes ya probadas en
  `ghcr.io/<owner>/qaguardian-api:<sha>` y `qaguardian-frontend:<sha>` (+ tag `latest`).

**Importante**: CI publica las imágenes en el registro; **no** tiene acceso al host de
producción. El `docker compose up -d` en el servidor real se ejecuta manualmente (o vía un
runner self-hosted / script del equipo de Ops) siguiendo los pasos de abajo.

## 2. Deploy a producción (manual, en el host)

Requiere `docker`/`docker compose` en el host y las credenciales del `.env` de producción
(ver [Manual-Instalación §4](../Manual-Instalacion.md#4-instalación-en-producción) y
[GO-LIVE-CHECKLIST](./GO-LIVE-CHECKLIST.md)).

```bash
# 1. Login al registro (una vez por host; usa un PAT con scope read:packages)
echo "$GHCR_TOKEN" | docker login ghcr.io -u <usuario> --password-stdin

# 2. Traer las imágenes recién publicadas por CI
SHA=<sha-del-commit-desplegado>
docker pull ghcr.io/<owner>/qaguardian-api:$SHA
docker pull ghcr.io/<owner>/qaguardian-frontend:$SHA
docker tag ghcr.io/<owner>/qaguardian-api:$SHA qaguardian-api:local-prod
docker tag ghcr.io/<owner>/qaguardian-frontend:$SHA qaguardian-frontend:local-prod

# 3. Aplicar el esquema EF ANTES de levantar la API (Sprint 16-A: migrate fuera del boot)
docker compose -f docker-compose.yml run --rm migrate

# 4. Hangfire schema + usuario SQL least-privilege (Sprint 20-A / B5 + 16-B).
#    db-init (sa) aplica en orden: 05-hangfire-schema.sql → 00-app-user.sql →
#    00-app-user-hangfire.sql. Obligatorio en primer deploy y tras upgrades de
#    Hangfire.SqlServer que cambien Install.sql. El API usa APP_DB_USER (no sa)
#    y Hangfire:PrepareSchemaIfNecessary=false.
docker compose -f docker-compose.yml run --rm db-init

# 5. Arrancar sin publicar puertos de DB/Redis (Sprint 16-B; SIN docker-compose.override.yml)
docker compose -f docker-compose.yml up -d

# 6. Verificar
curl -fsS http://localhost:5080/health/ready
curl -fsS http://localhost:5080/api/v1/version
```

Si omite el paso 4 en un volumen SQL vacío, el API fallará al inicializar
Hangfire.SqlServer (esquema `[HangFire]` ausente) — no hay auto-DDL en producción.

`docker-compose.yml` compila `api`/`frontend` con `build:` a partir del código fuente. Si el
host de producción no tiene el repo clonado (deploy solo por imagen), reemplace temporalmente
`build:` por `image: ghcr.io/<owner>/qaguardian-api:$SHA` (y equivalente para `frontend`) antes
del paso 5, o use un `docker-compose.prod.yml` con esos `image:` explícitos — no versionado en
este repo porque el nombre `<owner>` depende de dónde se publique.

## 3. Rollback

Disparadores: `/health/ready` no vuelve a `Healthy`, error 5xx sostenido, migración
incompatible, o regresión funcional detectada en el smoke post-deploy
(GO-LIVE-CHECKLIST §4).

```bash
# 1. Identificar el SHA/tag anterior conocido bueno (release notes, tags de Git, o
#    "docker images" si ya se había desplegado antes en ese host)
PREV_SHA=<sha-anterior-conocido-bueno>

# 2. Detener el release roto (deja sqlserver/redis/volúmenes intactos)
docker compose -f docker-compose.yml stop api frontend

# 3. Traer y re-etiquetar la imagen anterior
docker pull ghcr.io/<owner>/qaguardian-api:$PREV_SHA
docker pull ghcr.io/<owner>/qaguardian-frontend:$PREV_SHA
docker tag ghcr.io/<owner>/qaguardian-api:$PREV_SHA qaguardian-api:local-prod
docker tag ghcr.io/<owner>/qaguardian-frontend:$PREV_SHA qaguardian-frontend:local-prod

# 4. Solo si el release roto aplicó una migración EF incompatible: restaurar backup de
#    sqldata ANTES de levantar la API (una migración Down() no siempre es segura en prod
#    con datos reales — prefiera backup/restore sobre "dotnet ef database update <anterior>").
#    Ver GO-LIVE-CHECKLIST §0 (backup tomado antes del deploy).

# 5. Reiniciar con la imagen anterior
docker compose -f docker-compose.yml up -d api frontend

# 6. Verificar y comunicar
curl -fsS http://localhost:5080/health/ready
curl -fsS http://localhost:5080/api/v1/version   # debe mostrar la versión anterior
```

Postmortem obligatorio tras cualquier rollback en producción (GO-LIVE-CHECKLIST §8).

## 4. Notas

- **Por qué GHCR y no otro registro**: usa el `GITHUB_TOKEN` que ya existe en cada corrida de
  Actions (`permissions: packages: write`), sin secretos adicionales que provisionar.
- **Por qué el job `deploy` no hace `docker compose up` directo**: este repo no tiene acceso
  de red/SSH a ningún host real desde el runner de GitHub Actions. Automatizar eso requeriría
  secretos (IP, llave SSH o credenciales de un proveedor cloud) que no existen hoy; agregarlos
  sin que el usuario los provea sería inventar infraestructura. El paso manual de la sección 2
  es el que un runner self-hosted o un script de Ops ejecutaría en su lugar.
- **`environment: production`** en el job `deploy` permite configurar un *required reviewer*
  desde GitHub (Settings → Environments) para exigir aprobación manual antes de publicar,
  igual que `deploy-production` en `pipelines/templates/quality-gate-pipeline.yml`.
