# Security Checklist — QA Guardian

Checklist operativo para desarrollo continuo y revisión de PRs. Basado en OWASP Top 10 2025
y en los controles ya implementados en la plataforma (ver Security-Report.md para el detalle
de cada fix del Sprint 2). Úsese en cada release y al revisar cambios que toquen auth,
secretos, o superficie de red.

---

## A01:2025 — Broken Access Control

- [x] Todo endpoint no público requiere `[Authorize]` (verificado: controllers usan
      `[Authorize]` a nivel de clase + políticas específicas por acción).
- [x] Las políticas RBAC (`Policies.Administer/ManageProjects/ExecuteTests/ManageDefects/
      ViewReports`) están centralizadas en `Program.cs`, no repetidas ad-hoc.
- [x] SignalR hub declara política explícita y valida existencia del recurso antes de unir
      la conexión a un grupo.
- [ ] **Pendiente (backlog TM-01)**: no hay ACL por proyecto — cualquier rol ve todos los
      proyectos. Si el negocio requiere aislamiento multi-tenant estricto, priorizar.
- [ ] Al agregar un endpoint nuevo: ¿tiene `[Authorize]`? ¿La política es la más restrictiva
      posible para el caso de uso? ¿Se probó con un usuario de rol insuficiente (debe dar 403)?

## A02:2025 — Cryptographic Failures

- [x] Contraseñas: BCrypt factor 12 (`BcryptPasswordHasher`).
- [x] JWT: HS256 con clave ≥ 32 bytes, validado en el constructor de `JwtTokenService`.
- [x] Cifrado en reposo de tokens de integraciones: AES-256-**GCM** (autenticado), no CBC.
- [x] TLS terminado en el borde (nginx/LB) — verificar en cada entorno que certificados estén
      vigentes y que HSTS esté activo (`Strict-Transport-Security` presente fuera de Development).
- [ ] Al agregar un nuevo secreto de configuración: ¿tiene validación de longitud/formato en
      el constructor del servicio que lo usa? ¿Falla rápido y con mensaje claro si falta?

## A03:2025 — Injection (SQL / XSS / Command)

- [x] EF Core parametrizado en toda la capa de persistencia (no hay SQL concatenado).
- [x] FluentValidation en el borde de cada comando/query de escritura.
- [x] React escapa por defecto (JSX); **no** hay uso de `dangerouslySetInnerHTML`, `eval` ni
      `new Function` en el frontend (verificado por grep en este sprint).
- [x] CSP (`script-src 'self'`) como capa adicional contra XSS si un sink se colara a futuro.
- [ ] Al agregar contenido HTML dinámico (rich text, markdown renderizado): ¿se sanea con una
      librería (DOMPurify) antes de insertarlo? ¿Se evita `dangerouslySetInnerHTML` si es posible?
- [ ] Al invocar procesos externos (runners: Playwright/JMeter/Newman/ZAP vía `npx`/CLI):
      ¿los argumentos de usuario se pasan como array de argumentos, nunca interpolados en un
      string de shell?

## A04:2025 — Insecure Design

- [x] Quality Gates como control de negocio explícito (aprobar/rechazar despliegues).
- [x] Revisión de PR + pipeline CI como defensa en profundidad.
- [x] Contraseñas: política de complejidad en registro/cambio (10+ caracteres, mayúscula,
      minúscula, dígito) vía FluentValidation.
- [x] Bloqueo de cuenta tras 5 intentos fallidos (15 min), progresivo.

## A05:2025 — Security Misconfiguration

- [x] Cabeceras de seguridad completas en API y nginx: `X-Content-Type-Options`,
      `X-Frame-Options`, `Referrer-Policy`, `Content-Security-Policy`,
      `Strict-Transport-Security`, `Permissions-Policy`, `Cross-Origin-Opener-Policy`,
      `Cross-Origin-Resource-Policy`.
- [x] Swagger UI solo se expone en `Development`.
- [x] Sin secretos versionados en ningún `appsettings*.json` (todos vacíos o inexistentes;
      se resuelven vía user-secrets/env vars/secret manager).
- [x] `docker-compose.yml`/`.env.example` exigen secretos explícitos (`:?`), sin fallback.
- [ ] Antes de cada release: `grep` del diff en busca de patrones de secretos (API keys,
      connection strings con password real, `-----BEGIN...KEY-----`) — no confiar solo en
      revisión manual.
- [ ] Verificar que `ASPNETCORE_ENVIRONMENT` en producción **nunca** sea `Development`
      (desactivaría el rechazo de la contraseña de admin pública, entre otras protecciones).

## A06:2025 — Vulnerable & Outdated Components

- [ ] `npm audit` / `dotnet list package --vulnerable` como parte del pipeline CI (verificar
      que esté configurado; no cubierto por este sprint de seguridad de aplicación).
- [ ] Revisar mensualmente actualizaciones de: `Microsoft.IdentityModel.*`, `BCrypt.Net`,
      `Microsoft.AspNetCore.*`, `axios`, `@microsoft/signalr`, MUI.

## A07:2025 — Identification & Authentication Failures

- [x] Sin credenciales por defecto: arranque falla si `Seed:AdminPassword` falta o coincide
      con un valor públicamente documentado.
- [x] Mensajes de error genéricos en login (no revela si el correo existe).
- [x] Rotación de refresh tokens en cada uso (`/auth/refresh` invalida el anterior).
- [x] Refresh token en cookie `httpOnly`+`Secure`+`SameSite=Strict`; access token solo en
      memoria del cliente.
- [x] `POST /auth/logout` revoca el refresh token server-side (no solo "olvidarlo" en el
      cliente).
- [ ] Al añadir un nuevo flujo de autenticación (SSO adicional, API key para integraciones
      externas): ¿el token resultante tiene expiración? ¿Se audita su emisión?

## A08:2025 — Software & Data Integrity Failures

- [x] Cifrado autenticado (GCM) garantiza integridad de secretos de integraciones en reposo.
- [ ] Auditoría (`AuditLog`) sin firma criptográfica — backlog (TM-09). Si se requiere
      cumplimiento regulatorio fuerte de no-repudio, priorizar.
- [ ] Verificar `csproj`/`package.json`: ¿los `PackageReference`/`dependencies` usan rangos
      abiertos que permitirían una actualización maliciosa silenciosa? Preferir versiones fijas.

## A09:2025 — Security Logging & Monitoring Failures

- [x] `AuditMiddleware` registra toda escritura autenticada (usuario, acción, ruta, IP).
- [x] `nginx.conf`: `access_log off` en `/hubs/` (evita persistir el `access_token` de
      SignalR en logs de acceso).
- [x] Serilog no incluye query strings en el log de requests por defecto (verificado).
- [ ] Verificar que los logs de producción **no** terminen en un sistema con retención
      indefinida sin control de acceso — los logs de auditoría son ellos mismos un activo
      sensible (contienen IPs, correos, rutas con IDs de negocio).

## A10:2025 (o equivalente vigente) — SSRF / lógica de integraciones externas

- [x] Los tokens hacia SonarQube/GitHub/ZAP se cifran en reposo (GCM) y se resuelven por
      proyecto (`IntegrationSettingResolver`), no se aceptan URLs arbitrarias sin validar
      formato (`Uri.TryCreate` en `IntegrationConnectionTester`).
- [x] **12-B:** Guard reutilizable `ISsrfGuard` / `SsrfGuard` (`ValidateOutboundUri` → `Result<Uri>`):
      bloquea RFC1918, loopback, link-local, ULA IPv6, metadata (`169.254.169.254`,
      `metadata.google.internal`), exige HTTPS (allowlist HTTP solo en Development via
      `Security:Ssrf:AllowHttpHosts`), DNS resolve + validación de IP (rebinding básico).
      Tests: `SsrfGuardTests`.
- [x] **12-C:** Guard cableado en:
      `NotificationDispatcher.PostJsonAsync`, validators de webhooks / Sonar / ZAP,
      `IntegrationConnectionTester`, `SonarQubeClient`, HttpClient `"notifications"` +
      Sonar via `SsrfOutboundHandler`. GitHub API host fijo (`api.github.com`) — sin BaseUrl
      de API configurable. Tests: `SsrfWiringTests`.
- [x] **Allowlist documentada:** `Security:Ssrf:AllowHttpHosts` y `AllowPrivateHosts`
      (solo Development: `localhost`, `127.0.0.1` en `appsettings.Development.json`).
      Producción debe dejar ambos arrays vacíos.

## Sprint 18 — B2 / B3 / B7 (SQL allowlist, SSRF hops, EncryptionKey)

- [x] **B2 — Allowlist SQL:** `ISqlHostGuard` / `SqlHostGuard` valida DataSource contra
      `DatabaseValidation:AllowedSqlHosts` + criterio SSRF (metadata, RFC1918, loopback, ULA,
      IP decimal, DNS a privadas). Fail-closed fuera de Development si la allowlist está vacía;
      en Development vacía solo localhost. Cableado en Upsert de entornos
      (`DatabaseValidationCommands`) y defensa en profundidad en `SqlServerSchemaValidator.CompareAsync`
      antes de abrir `SqlConnection`. Tests: `SqlHostGuardTests`, `Sprint18SqlHostAllowlistTests`.
- [x] **B3 — SSRF redirects + pin IP:** `SsrfHttpHandlerFactory` fija `AllowAutoRedirect=false` y
      `ConnectCallback` con `ResolvePinnedAddress` (anti DNS-rebinding TOCTOU).
      `SsrfOutboundHandler` revalida cada hop 3xx (máx. `MaxRedirectHops`). Named clients
      Sonar + `"notifications"` usan el primary endurecido; `IntegrationConnectionTester` usa
      `CreateClient("notifications")`. Tests: `SsrfOutboundHardeningTests`.
- [x] **B7 — EncryptionKey + secretos UI:** fail-fast de `Security:EncryptionKey` (≥32) en
      `Program.cs` y ctor de `AesTokenEncryptionService` (rechaza `null`/`""`/corta).
      `launchSettings.json` sin secretos versionados. GET de canales con `TargetHint`
      (`NotificationChannelDto.MaskTarget`); catch de `NotificationDispatcher` sin `Target` en logs.
      Tests: `AesTokenEncryptionServiceTests`, `NotificationChannelDtoMaskingTests`.
- [ ] **Residual B2:** hosts allowlisted pueden ser privados a propósito (operación); un Admin/
      ManageProjects con allowlist mal configurada (`*.corp.local`) sigue pudiendo apuntar a
      SQL internos. No hay ACL distinta por entorno.
- [ ] **Residual B3:** pin de IP mitiga rebinding entre resolve y connect; no cubre commits
      DNS posteriores ni clientes sin el factory (p. ej. `IGitHubClient` host fijo sin handler
      SSRF — destino no configurable).
- [ ] **Residual B7:** `PostJsonAsync` aún puede registrar la URL del webhook en warnings de
      SSRF/status (no el catch de canal); rotar webhooks si aparecen en logs.

## Ejecución de scripts / RCE (Sprint 13)

- [x] **13-C:** Runners de usuario (`Playwright`, `Newman`, `JMeter`, `SeleniumIde`, `Visual`,
      codegen) usan `ISandboxedProcessExecutor` — no `ProcessExecutor` directo en el host API.
- [x] Fuera de Development: aunque `Runners:UseSandbox=false`, DI **fuerza** sandbox Docker
      (no fallback host). Development: fallback local + **warning** en log.
- [x] ZAP (Sprint 19-A / B4 VERIFY): vía `ISandboxedProcessExecutor` + imagen oficial
      (`SandboxImageZap` / `ghcr.io/zaproxy/zaproxy:stable`), sin `PreferHostDockerCli`;
      `targetUrl` validado con `ISsrfGuard` y pasado como argv citado (no shell).
      Tests: `ZapSandboxHardeningTests`, `RunnerSandboxWiringTests.Zap_usa_sandbox_*`.
- [x] **17-A (B1):** el API ya **no** monta `docker.sock` ni usa `group_add`/`DOCKER_GID`; el
      acceso al daemon pasa por `docker-socket-proxy` con allowlist (`CONTAINERS`/`IMAGES`/`POST`;
      resto denegado) en red interna `docker-control`, vía `DOCKER_HOST` (ver ADR-012).
- [x] **19-B (B8 VERIFY) — Límites de recursos y red sandbox:**
      `DockerSandboxedProcessExecutor` aplica `--memory` / `--cpus` / `--pids-limit`
      (`Runners:SandboxMemoryLimit` default `512m`, `SandboxCpus` default `1.0`,
      `SandboxPidsLimit` default `256`).
      `NormalizeNetwork` solo admite `none`|`bridge` (default `none`); **`host` vetado**
      (`InvalidOperationException`). Cualquier otro valor también falla en configuración.
      Tests: `RunnerSandboxSeamsTests` (asserts de flags + `NormalizeNetwork` host rechazado).
- [x] **19-B — Modelo de red / egress (opt-in consciente):**
  - `none` (default): sin egress — adecuado para la mayoría de runners.
  - `bridge`: egress completo del contenedor (necesario p. ej. ZAP / APIs bajo prueba).
    No hay firewall de aplicación en el código. Gancho operativo:
    `Runners:SandboxNetworkName` — si está definido con mode `bridge`, se pasa a
    `--network <nombre>` (red Docker dedicada creada por ops con reglas de egress /
    `docker network create …`). Vacío → `--network bridge`.
  - Documentar en el entorno: no usar `host`; preferir red dedicada acotada cuando se active
    `bridge`.
- [ ] **Residual B4:** no hay allowlist de URLs de escaneo ZAP por proyecto; solo
      `ISsrfGuard` (+ `AllowHttp`/`AllowPrivateHosts` globales). Un actor con permiso de
      lanzar security runs puede apuntar ZAP a cualquier URL pública permitida por SSRF.
- [ ] **Residual B8 — workspace / cuota de disco:** el `WorkingDirectory` real del run es
      `storage/evidence/runs/{runId}` (`FileEvidenceStorage.CreateRunDirectory`). La evidencia
      vive en ese path; **no** se borra post-run (limpieza agresiva rompería downloads).
      `IScriptExecutionEnvironment` / `storage/runner-workspaces` sí limpia al `Dispose`, pero
      hoy no es el path de producción del Hangfire run. Riesgo: crecimiento de disco por runs
      acumulados — mitigar con retención/ops (TTL o job de purge), no con delete inmediato
      tras el runner.
- [ ] **Residual RCE (mitigado, no cerrado):** la allowlist del proxy mantiene
      `POST /containers/create`, que aún permite montar rutas del host desde un contenedor nuevo;
      `bridge` sin allowlist de destinos a nivel app (solo gancho de red dedicada);
      imagen API aún instala Node/Playwright (legado).
      Cierre definitivo: sacar la orquestación del proceso API (agente/daemon remoto, Sprint 17-B)
      o gVisor/Kata.

---

## Checklist de PR (a aplicar en cada revisión de código)

- [ ] ¿El endpoint nuevo/modificado tiene `[Authorize]` con la política correcta?
- [ ] ¿Hay algún secreto (API key, password, connection string) en el diff?
- [ ] ¿Se agregó/modificó un endpoint de autenticación? → revisar CSRF y cookies.
- [ ] ¿Se agregó una dependencia npm/NuGet nueva? → revisar changelog/CVEs conocidos.
- [ ] ¿Se toca serialización JSON de tokens/credenciales? → verificar que no se filtren campos
      sensibles en la respuesta (ej. el refresh token nunca debe volver a aparecer en un
      cuerpo JSON).
- [ ] ¿Se agregó un input de usuario que se renderiza en el frontend? → verificar que no use
      `dangerouslySetInnerHTML` sin sanear.
- [ ] ¿Los tests de regresión de seguridad (`JwtTokenServiceTests`,
      `AesTokenEncryptionServiceTests`, `DbInitializerSecurityTests`, `LogoutCommandTests`)
      siguen en verde?
