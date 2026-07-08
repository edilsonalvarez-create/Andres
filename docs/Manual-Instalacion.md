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
dotnet test QAGuardian.sln            # verificación: 38 pruebas en verde
dotnet run --project src/QAGuardian.API
```

El perfil `Development` usa **SQLite** y **Hangfire en memoria**: no necesita SQL Server
ni Redis. La base se crea y siembra automáticamente (roles, admin, quality gate).

Frontend:

```bash
cd frontend
npm install
npm run dev        # http://localhost:5173 con proxy hacia la API
```

## 3. Instalación con Docker Compose

```bash
cp .env.example .env
# Edite .env: SQL_SA_PASSWORD, JWT_SIGNING_KEY (>=32 chars), ENCRYPTION_KEY, ANTHROPIC_API_KEY
docker compose up -d
```

| Servicio | URL |
|---|---|
| Frontend | http://localhost:8081 |
| API + Swagger | http://localhost:5080/swagger |
| Hangfire (solo admins) | http://localhost:5080/hangfire |
| SQL Server | localhost,1433 |
| SonarQube (perfil `tools`) | http://localhost:9000 |
| OWASP ZAP (perfil `tools`) | http://localhost:8090 |

## 4. Instalación en producción

1. **Base de datos**: ejecute en orden `database/01-schema.sql`, `02-indexes-constraints.sql`
   y `03-seed.sql` (el `04-sample-data.sql` es solo para demos). Alternativamente deje que
   la aplicación cree el esquema en el primer arranque.
2. **Secretos**: configure por variables de entorno (nunca en appsettings):
   - `ConnectionStrings__DefaultConnection`, `ConnectionStrings__Redis`
   - `Jwt__SigningKey` (aleatoria, mínimo 32 caracteres)
   - `Security__EncryptionKey` (cifra los tokens de integraciones en reposo)
   - `Anthropic__ApiKey` (agente IA; opcional — sin ella opera en modo heurístico)
   - `Smtp__*` para notificaciones por correo
   - `Seed__AdminEmail` / `Seed__AdminPassword`
3. **Ambientes**: `ASPNETCORE_ENVIRONMENT` ∈ `Development | QA | Staging | Production`.
   Cada uno tiene su `appsettings.{Ambiente}.json` con CORS y logging adecuados.
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
| ZAP no genera reporte | Sin acceso a Docker | Monte el socket Docker o use ZAP remoto |
| Notificaciones no llegan | Canal sin suscripción al evento | Revise flags `events` del canal |
| IA responde "heuristic-fallback" | Sin `Anthropic__ApiKey` | Configure la API key |
