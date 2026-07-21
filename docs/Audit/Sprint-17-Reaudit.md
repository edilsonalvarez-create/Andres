# Re-auditoría B1–B10 (post-Sprint 17) — Comité Independiente

**Comité:** Microsoft · Google · Amazon · Netflix · OpenAI · OWASP · ISTQB
**Premisa:** no asumir calidad; todo hallazgo verificado en código fuente y ejecución real.
**Baselines:** Auditoría Final Sprint 10 (**4.5/10**) · Re-auditoría Sprint 16 (**7.2/10**).
**Fecha:** 2026-07-18
**Alcance ejecutado hasta hoy:** Sprint 17-A + 17-VERIFY (B1). **Nota 2026-07-19:** Sprint 18 (B2/B3/B7) implementado y verificado en código — ver actualización al final; este documento conserva el snapshot post-17.
**Evidencia de ejecución (snapshot 17):** `dotnet test QAGuardian.sln` → **290 unit + 11 integration en verde** (0 fallos, 0 omitidos). Verificación en vivo del socket-proxy contra Docker real (daemon 29.6.1) durante 17-VERIFY.

## Veredicto

| Pregunta | Respuesta |
|----------|-----------|
| Score global | **7.4 / 10** (antes 7.2) |
| Delta vs Sprint 16 | **+0.2** (solo B1 cerrado de 10 bloqueadores) |
| Delta vs Sprint 10 | **+2.9** |
| ¿Alcanza la barra 9.5? | **No** |
| **Go Live Enterprise / multi-tenant / datos de terceros** | **NO-GO** (exige B1–B5 cerrados + 11/12/13 completos; solo B1 mitigado, con residual) |
| Uso interno single-tenant, red privada | **GO** |
| Uso interno multi-proyecto reforzado | **GO condicional** (cerrar B6 audit log) |

> **Regla del roadmap:** GO enterprise solo con **B1–B5 cerrados** y Sprints 11/12/13 realmente completos. Hoy: B1 **mitigado** (residual medio del proxy), **B2, B3, B4, B5 abiertos**; Critical 2 y 3 siguen en Parcial. **Falla la condición → NO-GO.**

## Scores por dimensión (delta vs Sprint 16)

| Dimensión | S10 | S16 | Ahora | Δ(S16) | Comité | Nota |
|-----------|----:|----:|------:|-------:|--------|------|
| Código / Arquitectura | 5.5 | 7.0 | 7.2 | +0.2 | Microsoft | ADR-012 (socket-proxy); `IContainerOrchestrator` (17-B) aún pendiente; `IScriptExecutionEnvironment` sigue muerto |
| Seguridad | 3.5 | 6.5 | 6.9 | +0.4 | OWASP | B1 pasa de Critical residual a residual medio; B2/B3/B4/B7 intactos |
| Performance | 5.0 | 8.0 | 8.0 | 0.0 | Netflix | Sin cambios; dashboard aún hidrata `Results` (B10) |
| UX | 5.5 | 7.5 | 7.5 | 0.0 | Google | Sin cambios en este ciclo |
| Testing | 4.0 | 6.0 | 6.1 | +0.1 | ISTQB | 301 backend verdes; floors simbólicos y E2E critical-path superficial siguen (B9) |
| Producto | 4.0 | 8.5 | 8.5 | 0.0 | ISTQB / Product | Sin cambios |
| DevOps | 4.0 | 7.5 | 7.7 | +0.2 | Amazon | Compose sin socket directo en API; Hangfire DDL residual (B5) sigue |
| IA | 4.5 | 7.0 | 7.0 | 0.0 | OpenAI | Sin cambios |
| **Global (seguridad ×2)** | **4.5** | **7.2** | **7.4** | **+0.2** | — | |

## Estado de los 3 Critical originales

### Critical 1 — RCE por runners sin sandbox → **Parcial (6.5/10, +0.5)**
**Nuevo (verificado):** el contenedor `api` ya **no** monta `/var/run/docker.sock` ni usa `group_add`/`DOCKER_GID`; habla con el daemon vía `docker-socket-proxy` con allowlist `CONTAINERS/IMAGES/POST` (`docker-compose.yml:82-114, 137`), en red interna. Confirmado en vivo: `docker info`/`docker exec` → **403**; `docker run -v /:/host` → **aún permitido** (residual del proxy).
**Gaps abiertos (snapshot 17; addendum Sprint 19):**
- ~~**B4** ZAP host-CLI~~ → **mitigado (Sprint 19-A):** sandbox + `ValidateOutboundUri` + `QuoteArg` (`TestRunners.cs:440-466`). Residual: sin allowlist de scan-URL por proyecto.
- ~~**B8** red/`cpus`/`pids`~~ → **mitigado (Sprint 19-B):** `--memory`/`--cpus`/`--pids-limit`; `host` vetado (`DockerSandboxedProcessExecutor.cs:153-160, 219-228`). Residual: egress `bridge` solo con red dedicada ops.
- Residual del proxy: `POST /containers/create` permite montar el FS del host → cierre pleno en 17-B (agente/daemon remoto).
- `IScriptExecutionEnvironment` registrado en DI pero no usado; imagen API con Node/Playwright/Newman legado.

### Critical 2 — Connection strings del cliente / SSRF → **Parcial fuerte (post-18; snapshot 17 era 6.5/10)**
**Gaps al cierre de Sprint 17 (históricos):** B2/B3/B7 abiertos — ver tabla de bloqueadores.
**Addendum Sprint 18 VERIFY:** B2/B3/B7 **mitigados** (`SqlHostGuard`+allowlist; redirects+pin; EncryptionKey fail-fast / launchSettings limpio / TargetHint). Residuales: allowlist operativa, clients fuera del factory SSRF, URL de webhook en algunos logs.

### Critical 3 — Sin ACL por proyecto (IDOR) → **Parcial fuerte (8.2/10, +0.2)**
**Nuevo (verificado):** `AssignGateToProjectCommand` ahora llama `EnsureCanAccessProjectAsync` (`QualityGateCommands.cs:101`).
**Gaps abiertos:**
- **B6** `GetAuditLogQuery` sin filtro de proyecto: cualquier `ManageProjects` lee rutas/emails/IPs de todos los tenants (`GetAuditLogQuery.cs:21-27`). `CreateQualityGate`/`GetQualityGates` siguen globales (`QualityGateCommands.cs:47-77`). `RoleInProject` no se enforce.

## Estado de los bloqueadores B1–B10

| # | Bloqueador | Estado | Evidencia (archivo:línea) |
|---|-----------|--------|---------------------------|
| **B1** | docker.sock en API → RCE = host | **Mitigado (residual medio)** | `docker-compose.yml:82-114` (proxy), `:137` (`DOCKER_HOST`), sin bind ni `group_add` en `api`. Residual: `POST /containers/create` |
| **B2** | Pivot SQL vía Upsert sin allowlist de hosts | **Mitigado (Sprint 18)** — residual allowlist operativa | `SqlHostGuard.cs`; Upsert `DatabaseValidationCommands.cs`; `SqlServerSchemaValidator.cs:22-25` |
| **B3** | SSRF por redirects 3xx + DNS rebinding | **Mitigado (Sprint 18)** — residual clients fuera del factory | `SsrfOutboundHandler.cs` + `SsrfHttpHandlerFactory`; `IntegrationConnectionTester` named client `"notifications"` |
| **B4** | ZAP host-CLI sin hardening + targetUrl interpolado | **Mitigado (Sprint 19)** — residual sin allowlist scan-URL por proyecto | `TestRunners.cs:440-466`; tests `ZapSandboxHardeningTests` |
| **B5** | Hangfire DDL vs usuario least-privilege | **Abierto** | `DependencyInjection.cs:176` (`PrepareSchemaIfNecessary = true`) |
| **B6** | Audit log / quality gates sin ACL de proyecto | **Parcial** | audit abierto (`GetAuditLogQuery.cs:21-27`); QG assign cerrado (`QualityGateCommands.cs:101`), CRUD global abierto |
| **B7** | EncryptionKey vacía; secretos en launchSettings | **Mitigado (Sprint 18)** — residual URL en logs webhook | `Program.cs` fail-fast; `AesTokenEncryptionService` ctor; `launchSettings.json` limpio; `NotificationChannelDto.TargetHint` |
| **B8** | bridge/host sin allowlist egress; sin cpus/pids | **Mitigado (Sprint 19)** — residual egress bridge ops-only | `DockerSandboxedProcessExecutor.cs:153-160, 219-241`; `RunnerSandboxSeamsTests` |
| **B9** | Coverage floors simbólicos + E2E superficial | **Abierto** | `UnitTests.csproj:7` (44); `frontend/vite.config.ts` (2.5%); `critical-path.spec.ts` |
| **B10** | Dashboard Include(Results); frontend sin HEALTHCHECK; azure-pipelines obsoleto | **Abierto** | `DashboardQueries.cs:123`; `frontend/Dockerfile` (sin HEALTHCHECK) |

**Snapshot post-17:** cerrados 1/10 (B1). **Actualización 2026-07-19:** B2/B3/B7 (Sprint 18) + B4/B8 (Sprint 19) → **6/10 mitigados** (B1+B2+B3+B4+B7+B8, todos con residual). Parciales: 1 (B6). Abiertos: B5, B9, B10.

## Go / No-Go enterprise

**NO-GO.** La condición del comité (B1–B5 cerrados + 11/12/13 realmente completos) no se cumple:
- B1 **mitigado, no cerrado** (residual `POST /containers/create` demostrado en vivo — se puede montar el FS del host en un contenedor nuevo).
- **B2, B3, B7 mitigados (Sprint 18)**; **B4, B8 mitigados (Sprint 19)** — residuales documentados.
- **B5 abierto** → Hangfire DDL sigue; no hay GO enterprise.

**Distancia:** corta pero real. Ejecutar Sprint 20 (B5) y 17-B (cierre de B1) según [AI-Prompts-Sprints-17-21.md](./AI-Prompts-Sprints-17-21.md), luego re-auditoría final.

## Delta narrativo

Desde la re-auditoría 7.2 solo cerró un bloqueador (B1) y de forma mitigada: el API ya no monta el socket Docker, lo que corta el salto trivial "RCE en API = daemon del host", pero el proxy con `POST` habilitado todavía permite crear un contenedor que monte `/` del host — verificado ejecutando el ataque contra el proxy real. El resto de la cadena de seguridad (pivot SQL por Upsert, SSRF por redirects, ZAP sin hardening, Hangfire DDL, EncryptionKey vacía) permanece intacta. El movimiento de score (+0.2) es honesto y pequeño: es un sprint de aislamiento parcial, no el cierre del vector.

**Addendum 2026-07-19 (Sprint 18 VERIFY):** B2/B3/B7 cerrados en código (ver [Sprint-16-Reaudit.md](./Sprint-16-Reaudit.md) y `docs/Security/Security-Checklist.md`).

**Addendum 2026-07-19 (Sprint 19 VERIFY):** B4/B8 cerrados en código (ZAP sandbox + flags de recursos; `host` vetado). Sigue **NO-GO enterprise** por B5 y residual B1 del proxy. No se declara nuevo score global aquí (snapshot post-17).

**Addendum 2026-07-19 (Sprint 21 VERIFY):** B6/B9/B10 **abiertos** (sin 21-A/B/C). Detalle: [Sprint-21-VERIFY.md](./Sprint-21-VERIFY.md). Tabla bloqueadores (filas B6/B9/B10) permanece válida: audit sin ACL, floors simbólicos, dashboard/`HEALTHCHECK`/azure sin remediar. **No-Go parcial** Sprint 21; **NO-GO enterprise**.

**Addendum 2026-07-19 (Sprint 21 Reaudit):** score actualizado **7.5/10** (+0.1 vs este snapshot). B5 **cerrado**; B1–B4/B7/B8 mitigados (B1 residual material); B6/B9/B10 abiertos. Informe: [Sprint-21-Reaudit.md](./Sprint-21-Reaudit.md). Sigue **NO-GO enterprise**.

---

*Re-auditoría post-Sprint 17 — comité independiente simulado. Baselines: [Sprint-10-Final-Audit.md](./Sprint-10-Final-Audit.md), [Sprint-16-Reaudit.md](./Sprint-16-Reaudit.md). Canvas interactivo: `sprint-17-reaudit.canvas.tsx`.*
