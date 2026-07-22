# Manual de Instalación — QA Guardian

## 1. Requisitos

| Componente | Versión mínima | Notas |
|---|---|---|
| .NET SDK | 9.0 | Backend |
| Node.js | 22 LTS | Frontend y runners (Playwright/Newman vía `npx`) |
| SQL Server | 2019 | Producción (en desarrollo se usa SQLite automáticamente) |
| Redis | 7 | Opcional en desarrollo (cae a caché en memoria) |
| Docker | 24+ | Para compose, OWASP ZAP y SonarQube |
| JMeter | 5.6 | Solo si se ejecutan pruebas de rendimiento en el host |

## 2. Instalación en desarrollo

```bash
git clone <repositorio> && cd QAGuardian
dotnet build QAGuardian.sln
dotnet test QAGuardian.sln            # verificación: 82 pruebas en verde
```

Antes del primer `dotnet run`, configure los secretos locales con `dotnet user-secrets`
(nunca en `appsettings.Development.json`, que sí se versiona — OWASP A05:2025/A07:2025):

```bash
cd src/QAGuardian.API
dotnet user-secrets set "Jwt:SigningKey" "genere-una-clave-aleatoria-de-al-menos-32-caracteres"
dotnet user-secrets set "Security:EncryptionKey" "genere-otra-clave-aleatoria-para-cifrado-de-tokens"
dotnet user-secrets set "Seed:AdminEmail" "admin@qaguardian.local"
dotnet user-secrets set "Seed:AdminPassword" "elija-una-contraseña-unica-para-su-equipo"
dotnet run --project src/QAGuardian.API
```

El perfil `Development` usa **SQLite** y **Hangfire en memoria**: no necesita SQL Server
ni Redis. Con `Database:ApplyMigrationsOnStartup=true` (solo Development): SQL Server aplica
migraciones EF; SQLite usa `EnsureCreated` desde el modelo (las migraciones del repo son
SQL Server y no se ejecutan tal cual en SQLite). Luego siembra roles/admin/quality gate. Si falta alguno de los cuatro
secretos, el arranque falla con un mensaje explícito indicando cuál falta.

**Helper Dev (recomendado si el SQLite venía de `EnsureCreated` pre–Sprint 16):**

```powershell
.\scripts\Update-DevDatabase.ps1          # estado del SQLite local
.\scripts\Update-DevDatabase.ps1 -Reset   # borra qaguardian-dev.db; el siguiente run recrea esquema
```

Frontend:

```bash
cd frontend
npm install
npm run dev        # http://localhost:5173 con proxy hacia la API
```

## 3. Instalación con Docker Compose

```bash
cp .env.example .env
# Edite .env: SQL_SA_PASSWORD, APP_DB_USER/APP_DB_PASSWORD, JWT_SIGNING_KEY (>=32 chars),
# ENCRYPTION_KEY, ANTHROPIC_API_KEY
docker compose up -d
```

`docker compose up` carga automáticamente `docker-compose.override.yml`, que publica
`1433`/`6379` al host **solo para depuración local** (SSMS, Redis Insight). El archivo
base `docker-compose.yml` (usado en producción/CI, ver §4) no publica esos puertos.

| Servicio | URL |
|---|---|
| Frontend | http://localhost:8081 |
| API + Swagger | http://localhost:5080/swagger |
| Hangfire (solo admins) | http://localhost:5080/hangfire |
| SQL Server | localhost,1433 (solo con `docker-compose.override.yml`, dev) |
| `migrate` (one-shot) | aplica EF antes del API; sin puerto |
| `db-init` (one-shot) | esquema Hangfire + usuario mínimo privilegio + grants Hangfire; sin puerto |
| SonarQube (perfil `tools`) | http://localhost:9000 |
| OWASP ZAP (perfil `tools`) | http://localhost:8090 |

**Non-root (Sprint 16-B):** las imágenes `api` (usuario `qaguardian`, UID 1000) y
`frontend` (`nginxinc/nginx-unprivileged`, UID 101) no ejecutan como root. El acceso de
`api` a `/var/run/docker.sock` (necesario para lanzar sandboxes de runners, ver
[ADR-011](Architecture/adr/ADR-011-runner-sandbox-seams.md)) sigue siendo un privilegio
residual; si el contenedor no puede usar el socket, defina `DOCKER_GID` en `.env` con el
GID del grupo `docker` del host (`getent group docker | cut -d: -f3` en Linux).

## 4. Instalación en producción

0. **Compose sin puertos DB/Redis públicos**: despliegue solo con el archivo base
   (sin `docker-compose.override.yml`, que es exclusivamente para desarrollo local):

   ```bash
   docker compose -f docker-compose.yml up -d
   ```

   `sqlserver` y `redis` quedan accesibles únicamente en la red interna de compose
   (`api`, `migrate`, `db-init`). No exponga esos puertos en el host de producción.

1. **Base de datos (fuente de verdad = migraciones EF)** — el proceso API en
   Production/QA/Staging **no** ejecuta `MigrateAsync` ni `EnsureCreated` (guardia en
   `Program.cs` + `Database:ApplyMigrationsOnStartup=false`). Aplique el esquema **antes**
   de arrancar la API:

   ```powershell
   # Job / máquina de release (requiere .NET SDK + dotnet tool restore)
   .\scripts\Migrate-Database.ps1 -ConnectionString "Server=...;Database=QAGuardian;User Id=...;Password=...;TrustServerCertificate=True"
   ```

   Equivalente:

   ```bash
   export ConnectionStrings__DefaultConnection='Server=...;Database=QAGuardian;...'
   dotnet tool restore
   dotnet ef database update \
     --project src/QAGuardian.Infrastructure/QAGuardian.Infrastructure.csproj \
     --startup-project src/QAGuardian.API/QAGuardian.API.csproj \
     --connection "$ConnectionStrings__DefaultConnection"
   ```

   Con Docker Compose, el servicio `migrate` corre `dotnet ef database update` y la API
   espera `service_completed_successfully` antes de arrancar (solo seed).

   - **Scripts del DBA (alternativa)**: ejecute en orden `database/01-schema.sql`,
     `02-indexes-constraints.sql` y `03-seed.sql` (el `04` es demo) y configure
     `Database__SkipInitialization=true` para omitir también el seed del API.
   - **Upgrade desde parches ad-hoc** (tablas Sprint 8/11/12 ya creadas por Ensure*):
     si `database update` falla porque la tabla ya existe, baselinee el historial EF
     (inserte en `__EFMigrationsHistory` el `MigrationId` pendiente que corresponda al
     esquema ya presente) o restaure desde backup y re-aplique migraciones en vacío.
   - Nuevas migraciones (desarrollo):
     `dotnet ef migrations add <Nombre> --project src/QAGuardian.Infrastructure --startup-project src/QAGuardian.API`.

1.b **Esquema Hangfire fuera del runtime (Sprint 20-A / B5)**: con
   `Hangfire:PrepareSchemaIfNecessary=false` (default en Production/QA/Staging y en
   `appsettings.json`), el API **no** crea tablas Hangfire. Aplíquelas con cuenta
   privilegiada **antes** de arrancar la API (tras migrate):

   ```bash
   sqlcmd -S <server> -U sa -P "<sa-password>" -C \
     -v HangFireSchema="HangFire" \
     -i database/05-hangfire-schema.sql
   ```

   El script es el `Install.sql` oficial de Hangfire.SqlServer **1.8.18** (schema v9),
   idempotente. Sin este paso, el arranque con SQL Server storage falla de forma
   explícita (Hangfire no encuentra `[HangFire].[Schema]` / tablas).

1.c **SQL least-privilege (el proceso API nunca usa `sa`)**: tras el esquema EF y Hangfire,
   cree el login de aplicación (`db_datareader` + `db_datawriter`, sin DDL) y otorgue
   DML explícito sobre el esquema HangFire:

   ```bash
   sqlcmd -S <server> -U sa -P "<sa-password>" -C \
     -v AppLogin="qaguardian_app" AppPassword="<contraseña-única>" \
     -i database/00-app-user.sql
   sqlcmd -S <server> -U sa -P "<sa-password>" -C \
     -v AppLogin="qaguardian_app" HangFireSchema="HangFire" \
     -i database/00-app-user-hangfire.sql
   ```

   Configure `ConnectionStrings__DefaultConnection` de la API con ese usuario (no `sa`).
   Variables típicas: `APP_DB_USER` / `APP_DB_PASSWORD` (Compose) o
   `ConnectionStrings__DefaultConnection` en el host. En Production/QA/Staging el
   placeholder versionado usa `User Id=qaguardian_app` — la contraseña va solo por
   secreto/env. Development sigue en SQLite + Hangfire en memoria (`UseInMemory=true`).

   Con Docker Compose, `db-init` automatiza 1.b + 1.c en orden:
   `05-hangfire-schema.sql` → `00-app-user.sql` → `00-app-user-hangfire.sql`.

2. **Secretos**: configure por variables de entorno o archivos secretos montados
   (`--env-file`, secret manager, Docker/Kubernetes secrets — nunca en appsettings ni
   hardcodeados en la imagen):
   - `ConnectionStrings__DefaultConnection` (usuario de mínimo privilegio, ver 1.c),
     `ConnectionStrings__Redis`
   - `Jwt__SigningKey` (aleatoria, mínimo 32 caracteres)
   - `Security__EncryptionKey` (cifra los tokens de integraciones en reposo)
   - `Anthropic__ApiKey` (agente IA; opcional — sin ella opera en modo heurístico)
   - `Smtp__*` para notificaciones por correo
   - `Seed__AdminEmail` / `Seed__AdminPassword`
3. **Ambientes**: `ASPNETCORE_ENVIRONMENT` ∈ `Development | QA | Staging | Production`.
   Cada uno tiene su `appsettings.{Ambiente}.json` con CORS y logging adecuados.
   Fuera de Development, `Database__ApplyMigrationsOnStartup` se ignora (siempre false).
4. **TLS**: termine HTTPS en el balanceador/nginx; la API emite cabeceras de seguridad.
5. **Runners**: la imagen Docker de la API ya incluye Node, Playwright (Chromium) y Newman.
   Para ZAP se requiere acceso al daemon Docker o un ZAP remoto; para JMeter, el binario
   `jmeter` en el PATH del contenedor/host de ejecución.

## 5. Primer inicio de sesión

1. Abra el frontend e ingrese con el usuario semilla (`Seed__AdminEmail`).
2. Cambie la contraseña y cree los usuarios del equipo con sus roles
   (Administrador, QA, Desarrollador, Líder Técnico, DevOps, Product Owner, Auditor, Cliente).
3. Cree el primer proyecto y configure sus integraciones (`POST /api/v1/integrations`):
   SonarQube (`baseUrl`, token, `extraJson: {"projectKey":"..."}`) y GitHub
   (`baseUrl: https://api.github.com`, token PAT, `extraJson: {"repository":"owner/repo"}`).
4. Configure canales de notificación (`POST /api/v1/admin/notification-channels`).

## 6. Verificación post-instalación

```bash
curl http://localhost:5080/health            # → Healthy
curl http://localhost:5080/swagger/v1/swagger.json | jq '.info.title'
```

Además: inicie sesión, cree un proyecto de prueba y dispare una ejecución Smoke;
debe completarse y evaluar el quality gate por defecto.

## 7. Solución de problemas

| Síntoma | Causa probable | Acción |
|---|---|---|
| 500 al arrancar: `Jwt:SigningKey` | Falta la clave | Defina `Jwt__SigningKey` |
| Ejecuciones quedan "Pendiente" | Hangfire sin storage | Verifique conexión SQL o `Hangfire__UseInMemory=true` |
| API no arranca / Hangfire: falta esquema | No se ejecutó `05-hangfire-schema.sql` y `PrepareSchemaIfNecessary=false` | Corra `db-init` (o el sqlcmd de §4.1.b) con sa/DBA antes del API |
| ZAP no genera reporte | Sin acceso a Docker | Monte el socket Docker o use ZAP remoto |
| Notificaciones no llegan | Canal sin suscripción al evento | Revise flags `events` del canal |
| IA responde "heuristic-fallback" | Sin `Anthropic__ApiKey` | Configure la API key |
