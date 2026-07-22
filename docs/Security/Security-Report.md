# Security Report — Sprint 2: Remediación OWASP

**Proyecto**: QA Guardian · **Sprint**: 2 (Seguridad) · **Fecha**: 2026-07-16
**Equipo**: OWASP Specialist, Ethical Hacker, DevSecOps, Cloud Architect, Security Engineer
**Metodología**: auditoría de código fuente real (no genérica) contra OWASP Top 10 2025,
seguida de corrección y verificación automatizada (98 tests unitarios en verde).

---

## Resumen ejecutivo

Se auditó el código real del backend (.NET 9), frontend (React/TS) e infraestructura
(docker-compose, nginx) contra OWASP Top 10 2025. Se confirmaron **9 hallazgos reales**
(1 Crítico, 3 Altos, 5 Medios) mediante lectura directa del código — no se asumió ningún
hallazgo sin verificarlo contra la implementación existente. Los 9 se corrigieron en este
sprint; 2 hallazgos adicionales de menor severidad quedan documentados como backlog
(requieren cambios de modelo de datos o de infraestructura de despliegue, fuera del alcance
de "corregir OWASP" a nivel de código de aplicación).

| Severidad | Encontrados | Corregidos | Backlog |
|---|---|---|---|
| Crítica | 1 | 1 | 0 |
| Alta | 3 | 3 | 0 |
| Media | 5 | 5 | 0 |
| Baja | 2 | 0 | 2 |

---

## Hallazgos y remediación (mapeo OWASP Top 10 2025)

### SEC-01 — A07:2025 Identification & Authentication Failures — **CRÍTICO**
**Hallazgo**: contraseña de administrador semilla (`admin@qaguardian.local` /
`QaGuardian.2026!`) documentada públicamente en el README, y hardcodeada como *fallback* en
tres lugares: `Program.cs`, `docker-compose.yml`, `appsettings.json`. Cualquier despliegue
que omitiera la variable de entorno correspondiente quedaba con un admin de contraseña
pública y predecible.

**Evidencia (antes)**:
```csharp
// Program.cs
await initializer.InitializeAsync(
    app.Configuration["Seed:AdminEmail"] ?? "admin@qaguardian.local",
    app.Configuration["Seed:AdminPassword"] ?? "QaGuardian.2026!");
```
```yaml
# docker-compose.yml
Seed__AdminPassword: ${ADMIN_PASSWORD:-QaGuardian.2026!}
```

**Corrección**:
- `Program.cs`: elimina el fallback; exige `Seed:AdminEmail`/`Seed:AdminPassword` explícitos;
  **rechaza** el valor público conocido fuera de `Development`.
- `DbInitializer.cs`: defensa en profundidad — rechaza el mismo valor también a nivel de
  siembra, independientemente de quién invoque `InitializeAsync`.
- `docker-compose.yml` / `.env.example`: `ADMIN_PASSWORD` ahora es obligatorio (`:?`), sin
  valor por defecto.
- `appsettings.json` (base): `Seed:AdminPassword` vacío; cada ambiente lo provee vía secreto.
- `appsettings.Development.json`: valor de desarrollo movido a `dotnet user-secrets` (ver
  SEC-09).
- `README.md`: reemplazada la documentación de la credencial pública por instrucciones de
  configuración.

**Archivos**: `src/QAGuardian.API/Program.cs`, `src/QAGuardian.Infrastructure/Persistence/DbInitializer.cs`,
`docker-compose.yml`, `.env.example`, `src/QAGuardian.API/appsettings.json`, `README.md`.
**Tests de regresión**: `DbInitializerSecurityTests.cs` (3 casos).

---

### SEC-02 — A05:2025 Security Misconfiguration — **ALTO**
**Hallazgo**: solo 3 cabeceras de seguridad presentes (`X-Content-Type-Options`,
`X-Frame-Options`, `Referrer-Policy`). Ausentes: `Content-Security-Policy`,
`Strict-Transport-Security`, `Permissions-Policy`, `Cross-Origin-Opener-Policy`,
`Cross-Origin-Resource-Policy` — tanto en la API como en nginx (el que sirve el HTML real al
usuario).

**Corrección**: cabeceras agregadas en ambos puntos (API `Program.cs`, `frontend/nginx.conf`):
```
Content-Security-Policy: default-src 'self'; script-src 'self';
  style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self' data:;
  connect-src 'self' wss:; object-src 'none'; base-uri 'self';
  form-action 'self'; frame-ancestors 'none'
Strict-Transport-Security: max-age=31536000; includeSubDomains
Permissions-Policy: camera=(), microphone=(), geolocation=(), payment=(), usb=(), interest-cohort=()
Cross-Origin-Opener-Policy: same-origin
Cross-Origin-Resource-Policy: same-origin
```
HSTS se activa solo fuera de `Development` (`app.UseHsts()` condicional) para no romper
`dotnet run` local sobre HTTP.

**Trade-off documentado**: `style-src` conserva `'unsafe-inline'` porque MUI/Emotion inyecta
`<style>` en runtime; `script-src` permanece estricto (sin `unsafe-inline`/`unsafe-eval`).

**Archivos**: `src/QAGuardian.API/Program.cs`, `frontend/nginx.conf`.

---

### SEC-03 — A05:2025 Security Misconfiguration / A01:2025 Broken Access Control — **ALTO**
**Hallazgo**: access token (30 min) y refresh token (7 días) se almacenaban en
`localStorage`, legible por cualquier script del origen. Un único XSS (dependencia npm
comprometida, futuro input sin sanear) exfiltraría ambos tokens, otorgando control de sesión
persistente por hasta 7 días — incluso después de cerrado el navegador.

**Corrección** (rediseño del ciclo de autenticación):
- El refresh token **nunca llega a JavaScript**: viaja solo en una cookie `httpOnly` +
  `Secure` (fuera de Development) + `SameSite=Strict`, con `Path=/api/v1/auth`.
- El access token vive en una variable de módulo en memoria (`client.ts`), nunca en
  `localStorage`/`sessionStorage`.
- Al recargar la página, un *silent refresh* (`POST /auth/refresh`, cookie automática)
  re-hidrata la sesión sin exponer el token en ningún almacenamiento persistente.
- Nuevo endpoint `POST /auth/logout`: revoca el refresh token server-side y limpia ambas
  cookies (una cookie `httpOnly` no puede borrarse desde JS).
- CSRF double-submit (`qaguardian_csrf`, no-httpOnly, comparación en tiempo constante) en
  `/auth/refresh` y `/auth/logout`, ya que introducir una cookie reabre superficie CSRF en
  esos dos endpoints específicos (ver SEC-04).

**Archivos**: `src/QAGuardian.API/Controllers/AuthController.cs` (reescrito),
`src/QAGuardian.Application/Features/Auth/AuthCommands.cs` (+`LogoutCommand`),
`frontend/src/api/client.ts` (reescrito), `frontend/src/auth/AuthContext.tsx` (bootstrap
asíncrono + `loading`), `frontend/src/App.tsx` (gate de carga), `frontend/src/types.ts`
(`AuthResponse` sin `refreshToken`).
**Tests de regresión**: `LogoutCommandTests.cs` (3 casos) + verificación manual de cookies
(ver Pentest-Checklist.md PT-04..PT-07).

---

### SEC-04 — A01:2025 Broken Access Control (CSRF) — **MEDIO**
**Hallazgo derivado de SEC-03**: al introducir una cookie de sesión, `/auth/refresh` y
`/auth/logout` quedan expuestos a que un sitio malicioso dispare la petición aprovechando el
envío automático de cookies del navegador. Impacto acotado (rotar tokens / cerrar sesión de
la víctima; CORS impide leer la respuesta), pero corregido por defensa en profundidad.

**Corrección**: patrón *double-submit cookie*. En login se emiten dos cookies:
`qaguardian_rt` (httpOnly, el secreto real) y `qaguardian_csrf` (legible por JS, valor
aleatorio de 32 bytes). El SPA lee la segunda y la reenvía como header `X-CSRF-Token`; el
servidor exige que header y cookie coincidan (`CryptographicOperations.FixedTimeEquals`,
evita timing attacks). Un atacante cross-site no puede leer la cookie CSRF de este origen
para igualarla.

**Archivos**: `src/QAGuardian.API/Controllers/AuthController.cs`, `frontend/src/api/client.ts`.

---

### SEC-05 — A02:2025 Cryptographic Failures — **MEDIO**
**Hallazgo**: los tokens de integraciones (SonarQube, GitHub, ZAP) se cifraban en reposo con
AES-256-**CBC** sin autenticación (sin HMAC ni modo AEAD). Vulnerable en teoría a *padding
oracle* y *bit-flipping*: un atacante con acceso de escritura a la BD podría alterar el
ciphertext de forma predecible sin conocer la clave.

**Corrección**: migrado a **AES-256-GCM** (cifrado autenticado). Formato de salida:
`nonce(12B) ‖ tag(16B) ‖ ciphertext`. Cualquier alteración de un solo bit del ciphertext o
del tag hace fallar el descifrado con `AuthenticationTagMismatchException` (verificado con
test de regresión).

**Impacto operativo — acción requerida en despliegue**: el formato de cifrado cambió; los
tokens de integraciones ya guardados en base de datos con el esquema anterior **no son
descifrables** con el nuevo código. Runbook: tras desplegar, reconfigurar una vez cada
integración activa (`POST /api/v1/integrations`) para que se re-cifre con GCM.

**Archivos**: `src/QAGuardian.Infrastructure/Identity/IdentityServices.cs`
(`AesTokenEncryptionService`).
**Tests de regresión**: `AesTokenEncryptionServiceTests.cs` (5 casos, incluyendo detección de
manipulación).

---

### SEC-06 — A02:2025 Cryptographic Failures — **MEDIO**
**Hallazgo**: `JwtTokenService` solo validaba que `Jwt:SigningKey` no estuviera vacía; no
exigía una longitud mínima. Una clave corta permitiría a un atacante con capacidad de cómputo
offline forjar tokens HS256 válidos para cualquier usuario/rol.

**Corrección**: se exige un mínimo de 32 bytes (256 bits) en UTF-8 — RFC 7518 §3.2 para
HS256. El arranque falla con un mensaje explícito indicando el tamaño detectado.

**Archivos**: `src/QAGuardian.Infrastructure/Identity/IdentityServices.cs` (`JwtTokenService`).
**Tests de regresión**: `JwtTokenServiceTests.cs` (5 casos).

---

### SEC-07 — A09:2025 Security Logging & Monitoring Failures — **MEDIO**
**Hallazgo**: SignalR requiere que el JWT viaje en el query string de `/hubs/testruns`
(`?access_token=...`) porque el navegador no permite cabeceras custom en el *handshake* de
WebSocket — patrón oficialmente documentado por Microsoft, no eliminable sin romper el
tiempo real. El riesgo real es que ese token quedara persistido en logs de acceso.
`nginx.conf` no tenía `access_log` explícito, por lo que heredaba el formato `combined` por
defecto, el cual **sí** registra la URL completa con el token.

**Corrección**: `location /hubs/ { access_log off; ... }` — se excluye esa ruta del log de
acceso de nginx. Se verificó además que Serilog (`UseSerilogRequestLogging()`) usa la
plantilla por defecto, que registra `RequestPath` sin query string (no requería cambio, mera
verificación).

**Archivos**: `frontend/nginx.conf`.
**Riesgo residual aceptado**: cualquier proxy/CDN adicional agregado en el futuro delante de
nginx debe replicar la misma exclusión.

---

### SEC-08 — A01:2025 Broken Access Control — **MEDIO**
**Hallazgo**: `TestRunHub.SubscribeToRun(testRunId)` aceptaba cualquier GUID sin verificar
que el test run existiera, y dependía de `[Authorize]` implícito (sin política explícita).

**Corrección**: se agrega `[Authorize(Policy = Policies.ViewReports)]` explícito al hub y se
valida la existencia del `testRunId` antes de unir la conexión al grupo, rechazando con
`HubException` y registrando un `LogWarning` (auditoría de intentos).

**Nota de alcance honesta**: esto **no** cierra el hallazgo TM-01 del Threat Model (ausencia
de ACL por proyecto) — los endpoints REST equivalentes tienen el mismo nivel de granularidad
(rol global, sin pertenencia a proyecto). El fix aplicado es defensa en profundidad
específica del hub (evita enumeración de IDs vía grupos), no una resolución del modelo de
autorización completo. Ver Threat-Model.md §5 para la recomendación de ACL por proyecto.

**Archivos**: `src/QAGuardian.API/Hubs/TestRunHub.cs`.

---

### SEC-09 — A05:2025 Security Misconfiguration — **BAJO**
**Hallazgo**: `Jwt:SigningKey`, `Security:EncryptionKey`, `Seed:AdminEmail` y
`Seed:AdminPassword` estaban hardcodeados (con valores de desarrollo, pero versionados) en
`appsettings.Development.json`, pese a que el proyecto ya tenía `UserSecretsId` configurado
(commit previo: "Habilitar user secrets en la API"). Normaliza el hábito de commitear
secretos, aunque el valor específico tuviera baja sensibilidad.

**Corrección**: los cuatro valores se removieron del archivo versionado y se migraron a
`dotnet user-secrets` (documentado en `docs/Manual-Instalacion.md` §2 y `README.md`).

**Hallazgo colateral detectado durante la migración**: al ejecutar `dotnet user-secrets list`
para esta migración, se confirmó que ya existe un `Anthropic:ApiKey` real almacenado en el
secret store local de esta máquina — correctamente ubicado (fuera de git), pero su valor
apareció en la salida de una herramienta durante esta sesión de trabajo. Se recomienda al
equipo evaluar si el historial de esta sesión requiere rotarlo por higiene, aunque el
mecanismo de almacenamiento en sí es el correcto y no constituye una fuga a git.

**Archivos**: `src/QAGuardian.API/appsettings.Development.json`, `docs/Manual-Instalacion.md`,
`README.md`.

---

## Hallazgos NO corregidos en este sprint (backlog documentado)

| ID | Hallazgo | OWASP 2025 | Por qué no se corrigió ahora |
|---|---|---|---|
| TM-01 | RBAC sin ACL por proyecto | A01 Broken Access Control | Requiere nuevo modelo de datos (`ProjectMember`) y tocar todos los handlers — sprint propio. |
| TM-11 | `ForwardedHeadersMiddleware` no configurado | A09 Logging Failures / A05 Misconfig | Requiere conocer los rangos de IP del proxy de cada entorno real (dev/QA/staging/prod difieren); configurarlo sin restringir el origen sería spoofeable. |
| TM-09 | Auditoría sin firma criptográfica | A08 Software & Data Integrity Failures | Cambio de esquema de BD + estrategia de firma (HMAC/hash-chain) — se documenta como mejora futura, no bloqueante. |

---

## Verificación

- **Compilación**: `QAGuardian.Application`, `QAGuardian.Infrastructure`, `QAGuardian.API`
  compilan sin errores.
- **Tests unitarios**: **98/98 en verde** (82 preexistentes + 16 nuevos de este sprint).
- **Frontend**: `tsc -b --noEmit` sin errores; `npm run build` (typecheck + vite build) exitoso.
- **Tests de integración HTTP** (`QAGuardian.IntegrationTests`): **no se pudieron ejecutar en
  esta sesión** por un proceso `QAGuardian.API` (PID 28304) que ya estaba corriendo en
  background antes de este sprint, bloqueando la copia de las DLL de salida (conflicto de
  archivo, no error de compilación — confirmado compilando `QAGuardian.API.csproj` a una
  carpeta temporal, que sí tuvo éxito). **Acción recomendada**: detener ese proceso y correr
  `dotnet test QAGuardian.sln` completo antes de mergear a producción.

---

## Archivos modificados (resumen)

| Archivo | Cambio |
|---|---|
| `src/QAGuardian.API/Program.cs` | Fail-fast admin seed, CSP/HSTS/Permissions-Policy, headers |
| `src/QAGuardian.API/Controllers/AuthController.cs` | Reescrito: cookies httpOnly, CSRF, logout |
| `src/QAGuardian.API/Hubs/TestRunHub.cs` | Política explícita + validación de existencia |
| `src/QAGuardian.Application/Features/Auth/AuthCommands.cs` | + `LogoutCommand` |
| `src/QAGuardian.Application/Features/TestRuns/DownloadEvidenceQuery.cs` | (Sprint 1, sin cambios) |
| `src/QAGuardian.Infrastructure/Identity/IdentityServices.cs` | Min. clave JWT, AES-GCM |
| `src/QAGuardian.Infrastructure/Persistence/DbInitializer.cs` | Rechazo de password público |
| `docker-compose.yml`, `.env.example` | `ADMIN_PASSWORD` obligatorio |
| `src/QAGuardian.API/appsettings*.json` | Sin secretos versionados |
| `frontend/nginx.conf` | Headers, CSP, HSTS, `access_log off` en `/hubs/` |
| `frontend/src/api/client.ts` | Reescrito: sin localStorage, cookies + CSRF |
| `frontend/src/auth/AuthContext.tsx` | Bootstrap asíncrono (`loading`), logout real |
| `frontend/src/App.tsx` | Gate de carga antes de redirigir a `/login` |
| `frontend/src/types.ts` | `AuthResponse` sin `refreshToken` |
| `README.md`, `docs/Manual-Instalacion.md` | Documentación de credenciales y user-secrets |
| `tests/QAGuardian.UnitTests/**` | +16 tests de regresión (ver Regression-Tests.md) |
