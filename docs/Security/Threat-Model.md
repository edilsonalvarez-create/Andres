# Threat Model — QA Guardian

**Metodología**: STRIDE (Spoofing, Tampering, Repudiation, Information Disclosure, Denial of
Service, Elevation of Privilege) + DFD (Data Flow Diagram) por confianza.
**Alcance**: Sprint 2 — Seguridad (JWT, cookies, headers, CORS, CSRF, XSS, secrets, SignalR).
**Fecha**: 2026-07-16 · **Autores**: OWASP Specialist / Ethical Hacker / DevSecOps / Cloud
Architect / Security Engineer (auditoría conjunta).
**Estado**: Todos los hallazgos con severidad Alta/Crítica de este documento tienen mitigación
implementada en este sprint (ver [Security-Report.md](Security-Report.md) para el detalle
línea por línea).

---

## 1. Diagrama de flujo de datos (confianza)

```
┌────────────────────┐        HTTPS (TLS terminado en el borde)       ┌──────────────────────┐
│  Navegador (SPA)    │ ───────────────────────────────────────────▶  │  nginx (frontera)     │
│  Zona: NO CONFIABLE │ ◀───────────────────────────────────────────  │  Zona: DMZ            │
└─────────┬───────────┘   Cookies: qaguardian_rt (httpOnly), csrf     └──────────┬────────────┘
          │ Bearer JWT (access, en memoria) + X-CSRF-Token                       │ proxy_pass (HTTP interno)
          │ WebSocket + ?access_token= (solo /hubs)                             ▼
          │                                                          ┌──────────────────────┐
          │                                                          │  QAGuardian.API       │
          │                                                          │  Zona: CONFIABLE      │
          │                                                          │  - JwtBearer + OIDC   │
          │                                                          │  - RBAC (8 roles)     │
          │                                                          │  - Rate limiter       │
          │                                                          │  - AuditMiddleware    │
          │                                                          └────┬────────┬────────┘
          │                                                               │        │
          │                                            ┌──────────────────┘        └───────────────┐
          │                                            ▼                                            ▼
          │                                 ┌──────────────────────┐                    ┌──────────────────────┐
          │                                 │ SQL Server / SQLite   │                    │ Integraciones externas│
          │                                 │ Zona: CONFIABLE       │                    │ SonarQube/GitHub/ZAP  │
          │                                 │ - Tokens cifrados     │                    │ Zona: NO CONFIABLE    │
          │                                 │   (AES-256-GCM)       │                    │ (credenciales cifradas│
          │                                 │ - Passwords (BCrypt)  │                    │  en reposo)           │
          │                                 └──────────────────────┘                    └──────────────────────┘
          │
          └── SignalR (WebSocket, mismo origen vía proxy) ──▶ TestRunHub (grupo por testRunId)
```

**Fronteras de confianza cruzadas por este sprint**:
- F1: Navegador ↔ nginx (borde público — TLS, headers, CSP).
- F2: nginx ↔ API (interno, HTTP plano dentro de la red docker — confiado por topología, no
  por autenticación mutua; ver hallazgo TM-05).
- F3: API ↔ Base de datos (secretos e integraciones cifradas en reposo).
- F4: API ↔ Servicios externos (SonarQube/GitHub/ZAP/Anthropic — credenciales salientes).

---

## 2. Activos a proteger

| Activo | Impacto si se compromete |
|---|---|
| Refresh token (7 días) | Toma de sesión persistente — acceso a todo lo que el usuario puede ver/hacer |
| Access token (30 min) | Toma de sesión temporal |
| Contraseña de administrador semilla | Control total de la plataforma desde el primer arranque |
| Clave de firma JWT (`Jwt:SigningKey`) | Forjar tokens válidos para cualquier usuario/rol sin credenciales |
| Clave de cifrado de integraciones (`Security:EncryptionKey`) | Descifrar tokens de SonarQube/GitHub/ZAP de todos los proyectos |
| Evidencias de test runs (screenshots, videos, logs) | Filtración de datos de negocio de los sistemas bajo prueba |
| Quality Gates (condiciones) | Manipularlas para aprobar despliegues que no cumplen el umbral real |
| Auditoría (`AuditLog`) | Ocultar acciones maliciosas si se pudiera alterar (integridad) |

---

## 3. Hallazgos STRIDE

Cada hallazgo indica: Categoría STRIDE · Severidad · Estado tras Sprint 2.

### TM-01 — Spoofing / Elevation of Privilege: RBAC sin ACL por proyecto
**STRIDE**: Spoofing (de "pertenencia a un proyecto"), Elevation of Privilege.
**Descripción**: Los 8 roles (`Administrador`, `QA`, `Desarrollador`, `LiderTecnico`, `DevOps`,
`ProductOwner`, `Auditor`, `Cliente`) son **globales**: no existe un concepto de "usuario
asignado al Proyecto X". Cualquier usuario autenticado con el rol `QA` puede leer/ejecutar
sobre **cualquier** proyecto de la plataforma, incluyendo `GET /testruns/{id}` y el hub de
SignalR.
**Severidad**: Media (multi-tenant débil, pero consistente en toda la superficie — no es una
grieta puntual en un solo endpoint).
**Estado**: ✅ **Mitigado (Sprint 11 VERIFY)** — `ProjectMember` + `IProjectAccessService`
(Admin global bypass; resto requiere membresía activa; denegación = **404** anti-enum).
Enforcement en handlers de proyectos, runs, casos, defectos, dashboard, trazabilidad, approvals,
catálogo, gates assign, integraciones, notificaciones, scripts, `TestRunHub` y reportes.
Evidencias: descarga por `Evidence.Id` (preferido) o `path` **solo** si coincide con
`Evidence.FilePath` del TestRun autorizado (`DownloadEvidenceQuery` / Sprint 11-C).
**Riesgo residual aceptado**: jobs Hangfire (`ExecuteTestRun`, `ExecuteDatabaseValidation`) no
re-validan ACL de usuario (confían en autorización al encolar); Quality Gate CRUD global sigue
siendo recurso de plataforma (policies de rol, no ProjectMember).

### TM-02 — Information Disclosure: JWT en query string (SignalR)
**STRIDE**: Information Disclosure.
**Descripción**: El navegador no permite enviar cabeceras custom en el *handshake* de
WebSocket, por lo que el patrón clásico de SignalR pone `access_token` en el query string.
**Severidad**: Alta (antes mitigada con nginx `access_log off` en `/hubs/`).
**Estado**: ✅ Cerrado (Sprint 15-B). El SPA autentica el hub con `accessTokenFactory`
(Authorization) y en Production fuerza **LongPolling** (sin JWT en URL). El API rechaza
`?access_token=` fuera de Development. Residual Dev: query token opcional + warning en log.
### TM-03 — Information Disclosure / Tampering: tokens en `localStorage`
**STRIDE**: Information Disclosure, Tampering (persistencia post-XSS).
**Descripción**: Antes de este sprint, el access token (30 min) y el refresh token (7 días)
vivían en `localStorage`, legible por cualquier script en el origen — un XSS de terceros
(dependencia de npm comprometida, futuro campo de texto no saneado) exfiltraría ambos y
otorgaría control de sesión por hasta 7 días, incluso después de cerrar la pestaña.
**Severidad**: Alta.
**Estado**: ✅ Resuelto. El refresh token ahora vive **solo** en una cookie `httpOnly` +
`Secure` + `SameSite=Strict`, con `Path` restringido a `/api/v1/auth` — inaccesible a
JavaScript en cualquier circunstancia. El access token vive en una variable de módulo en
memoria (nunca persistido); un XSS activo aún podría leerlo *mientras la pestaña está
abierta*, pero no hay nada que sobreviva al cierre de la pestaña ni al F5.
**Riesgo residual aceptado**: el access token en memoria, con XSS activo, sigue siendo
legible por 30 min como máximo. Cerrar completamente ese vector requiere CSP estricta sin
`'unsafe-inline'` en `style-src` (ver TM-07) o migrar a un modelo BFF (Backend-for-Frontend).

### TM-04 — Elevation of Privilege / Tampering: CSRF sobre el ciclo de auth
**STRIDE**: Tampering (rotación no solicitada de tokens), Denial of Service (logout forzado).
**Descripción**: Al introducir una cookie para el refresh token, `/auth/refresh` y
`/auth/logout` quedan expuestos a que el navegador la adjunte automáticamente en una petición
disparada desde un sitio malicioso (CSRF clásico). El impacto real es bajo (rotar tokens o
cerrar sesión de la víctima, no exfiltrar datos, porque CORS bloquea leer la respuesta desde
otro origen) pero se corrige como defensa en profundidad.
**Severidad**: Media (impacto bajo, pero explícitamente pedido por el equipo de seguridad).
**Estado**: ✅ Resuelto. Patrón *double-submit cookie*: `qaguardian_csrf` (no-httpOnly,
`SameSite=Strict`) debe coincidir con el header `X-CSRF-Token` en toda petición a
`/auth/refresh` y `/auth/logout`; comparación en tiempo constante
(`CryptographicOperations.FixedTimeEquals`). Un atacante cross-site no puede leer la cookie
de otro origen para igualarla.
**Nota de alcance**: el resto de la API sigue siendo Bearer-only (sin cookies ambientales),
por lo que **no tiene** superficie CSRF — el fix se limitó a los dos endpoints que ahora usan
cookie.

### TM-05 — Tampering: cifrado de tokens de integraciones sin autenticar (AES-CBC)
**STRIDE**: Tampering.
**Descripción**: Los tokens de SonarQube/GitHub/ZAP se cifraban con AES-256-**CBC** sin HMAC
ni modo autenticado. CBC sin autenticación es vulnerable a *padding oracle* (si algún camino
de código expone si el padding fue válido) y a *bit-flipping* (alterar bloques de ciphertext
de forma predecible sin necesidad de conocer la clave).
**Severidad**: Media (requiere acceso a la BD + un oráculo de error, no explotable trivialmente
hoy, pero es una debilidad criptográfica real y sin justificación para mantenerla).
**Estado**: ✅ Resuelto. Migrado a AES-256-**GCM** (cifrado autenticado: confidencialidad +
integridad en una operación). Verificado con test de regresión que confirma que **cualquier**
alteración de un bit del ciphertext hace fallar el descifrado (`AuthenticationTagMismatchException`).
**Impacto operativo**: el formato de salida cambió (nonce‖tag‖ciphertext vs IV‖ciphertext);
los tokens de integraciones ya guardados en BD **deben volver a configurarse una vez** tras
desplegar (ver runbook en Security-Report.md §6).

### TM-06 — Spoofing: clave de firma JWT sin longitud mínima
**STRIDE**: Spoofing (forjar tokens).
**Descripción**: `JwtTokenService` solo validaba que la clave no estuviera vacía, no que
tuviera entropía suficiente. Una clave corta (p. ej. `"a"` o `"clave123"`) permitiría a un
atacante con capacidad de fuerza bruta offline forjar tokens JWT válidos para cualquier rol.
**Severidad**: Media (requiere que un operador configure una clave débil; el código no lo
impedía).
**Estado**: ✅ Resuelto. Se exige un mínimo de 32 bytes (256 bits) en UTF-8, acorde a RFC 7518
§3.2 para HS256. El arranque falla con un mensaje explícito si la clave configurada es corta.

### TM-07 — Tampering / Information Disclosure: ausencia de CSP/HSTS/Permissions-Policy
**STRIDE**: Tampering (inyección de scripts/estilos de terceros), Information Disclosure
(fuga vía `Referrer`/`postMessage` de features del navegador no usadas).
**Descripción**: Solo existían `X-Content-Type-Options`, `X-Frame-Options` y `Referrer-Policy`.
Sin CSP, un XSS que lograra inyectar un `<script src="https://evil.com">` se ejecutaría sin
restricción; sin HSTS, un usuario que escriba `http://` en la barra de direcciones queda
expuesto a *downgrade*/*sslstrip* en la primera conexión.
**Severidad**: Media.
**Estado**: ✅ Resuelto (API y nginx). CSP con `default-src 'self'`, `script-src 'self'`
(sin `unsafe-inline`/`unsafe-eval`), `frame-ancestors 'none'`, `object-src 'none'`; HSTS
`max-age=31536000; includeSubDomains`; `Permissions-Policy` deshabilitando cámara/micrófono/
geolocalización/pago/USB/FLoC (`interest-cohort=()`); `Cross-Origin-Opener-Policy` y
`Cross-Origin-Resource-Policy` en `same-origin`.
**Nota**: `style-src` conserva `'unsafe-inline'` porque MUI/Emotion inyecta `<style>` en
runtime; es un riesgo aceptado y documentado (afecta solo inyección de CSS, no de JS).

### TM-08 — Elevation of Privilege / Spoofing: credenciales de administrador conocidas públicamente
**STRIDE**: Elevation of Privilege, Spoofing.
**Descripción**: `admin@qaguardian.local` / `QaGuardian.2026!` estaba (a) documentado en el
README público, (b) hardcodeado como fallback en `Program.cs`, (c) hardcodeado en
`docker-compose.yml`, (d) en `appsettings.json` versionado. Cualquier despliegue que omitiera
configurar `Seed:AdminPassword` explícitamente quedaba con un admin de contraseña pública.
**Severidad**: **Crítica** (compromiso total e inmediato de una instalación mal configurada,
sin necesidad de explotar nada — solo probar la credencial documentada).
**Estado**: ✅ Resuelto. Sin fallback en código; el arranque exige `Seed:AdminEmail`/
`Seed:AdminPassword` explícitos y **rechaza** el valor público conocido fuera de Development
(doble validación: `Program.cs` y, como defensa en profundidad, `DbInitializer`).
`docker-compose.yml`/`.env.example` ahora exigen el valor vía `:?` (fallo explícito si falta).

### TM-09 — Repudiation: trazabilidad de acciones administrativas
**STRIDE**: Repudiation.
**Descripción**: `AuditMiddleware` registra usuario, método, ruta, IP y timestamp para toda
escritura autenticada — ya existía antes de este sprint y se mantiene. Sprint 1 agregó el
endpoint de lectura (`GET /audit-log`) para consultarlo.
**Severidad**: Baja (control ya presente).
**Estado**: Sin cambios en este sprint; documentado aquí por completitud del modelo de amenazas.
**Limitación conocida**: el registro de auditoría no está firmado criptográficamente — un
atacante con acceso de escritura directo a la BD podría alterar el historial. Recomendado
para un sprint futuro (ver §5).

### TM-10 — Denial of Service: rate limiting
**STRIDE**: Denial of Service.
**Descripción**: Límite global de 300 req/min por usuario/IP y 10 req/min específico para
`/auth/*` (login, refresh, register, change-password, **logout** — agregado a la misma
política en este sprint). Ya existía antes del sprint; se revisó y confirmó que el nuevo
endpoint `logout` queda cubierto por la misma política de clase (`[EnableRateLimiting("auth")]`
a nivel de controller).
**Severidad**: Baja (control ya presente, extendido correctamente al nuevo endpoint).
**Estado**: Sin cambios funcionales; verificado que sigue aplicando correctamente.

### TM-11 — Information Disclosure: cabeceras de proxy no confiadas (IP real del cliente)
**STRIDE**: Information Disclosure / Repudiation (auditoría con IP incorrecta).
**Descripción**: nginx agrega `X-Forwarded-For`/`X-Forwarded-Proto`, pero la API **no** tiene
configurado `ForwardedHeadersMiddleware`. Esto significa que `Connection.RemoteIpAddress`
(usado por el rate limiter y por `AuditMiddleware` para registrar la IP) ve la IP del
contenedor de nginx, no la del cliente real, detrás de un despliegue con proxy.
**Severidad**: Baja-Media (no es una vulnerabilidad explotable directamente, pero degrada la
calidad forense de la auditoría y hace que el rate limiter por IP sea efectivamente "por
nginx", no por cliente real).
**Estado**: **No resuelto en este sprint** — requiere configurar rangos de proxy confiados
(`KnownProxies`/`KnownNetworks`) específicos del entorno de despliegue real, lo cual excede el
alcance de cambios de código genéricos. Ver recomendación en §5.

---

## 4. Matriz de riesgo residual

| ID | Amenaza | Severidad pre-sprint | Severidad post-sprint | Estado |
|---|---|---|---|---|
| TM-08 | Admin con password público | Crítica | — | ✅ Cerrado |
| TM-03 | Tokens en localStorage | Alta | Baja (residual: memoria + XSS activo) | ✅ Mitigado |
| TM-02 | JWT en query string | Alta | — (cerrado Sprint 15-B: header + LongPolling) | ✅ Cerrado |
| TM-05 | AES-CBC sin autenticar | Media | — | ✅ Cerrado |
| TM-06 | Clave JWT sin mínimo | Media | — | ✅ Cerrado |
| TM-07 | Sin CSP/HSTS | Media | Baja (residual: `style-src unsafe-inline`) | ✅ Mitigado |
| TM-04 | CSRF en auth cookie | Media | — | ✅ Cerrado |
| TM-01 | RBAC sin ACL por proyecto | Media | Baja (residual: jobs Hangfire) | ✅ Mitigado (Sprint 11) |
| TM-11 | Forwarded headers no confiados | Baja-Media | Baja-Media (sin cambios) | ⏳ Backlog |
| TM-09 | Auditoría sin firma | Baja | Baja (sin cambios) | ⏳ Backlog |
| TM-10 | Rate limiting | Baja | Baja (ya cubierto) | ✅ Verificado |

---

## 5. Recomendaciones fuera de alcance de este sprint

1. **ACL por proyecto (TM-01)**: ✅ cerrado en Sprint 11 (`ProjectMember` + `IProjectAccessService`).
   Residual: re-validación ACL en jobs Hangfire / UI de gestión de membresías.
2. **`ForwardedHeadersMiddleware` (TM-11)**: configurar `KnownProxies`/`KnownNetworks` con la
   IP real del proxy de cada entorno (dev/QA/staging/prod difieren) antes de habilitarlo —
   confiar en `X-Forwarded-For` sin restringir el origen permitiría spoofing de IP.
3. **Auditoría firmada (TM-09)**: HMAC o cadena de hashes (estilo blockchain simplificado)
   sobre `AuditLog` para detectar manipulación directa en BD.
4. **CSP sin `unsafe-inline`**: evaluar migrar de Emotion/MUI a una estrategia de CSS con
   nonces, o aceptar el riesgo residual documentado (impacto limitado a inyección de estilos).
