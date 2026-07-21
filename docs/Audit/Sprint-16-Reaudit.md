# Re-auditoría Sprints 11–16 — Comité Independiente

**Comité:** Microsoft · Google · Amazon · Netflix · OpenAI · OWASP · ISTQB
**Premisa:** no asumir calidad; todo hallazgo verificado en código fuente y ejecución real.
**Baseline:** Auditoría Final Sprint 10 (score **4.5/10**, NO-GO enterprise).
**Fecha:** 2026-07-17
**Evidencia de ejecución:** backend 290 unit + 11 integration en verde (`dotnet test QAGuardian.sln`); frontend 25/25 Vitest en verde.

## Veredicto

| Pregunta | Respuesta |
|----------|-----------|
| Score global | **7.2 / 10** (antes 4.5) |
| Delta vs Sprint 10 | **+2.7** |
| ¿Alcanza la barra 9.5? | **No** |
| Go Live Enterprise / multi-tenant / datos de terceros | **NO-GO condicional** (bloqueadores nombrados abajo; distancia corta) |
| Uso interno single-tenant, red privada | **GO** (antes GO condicional) |
| Uso interno multi-proyecto reforzado | **GO condicional** (cerrar B1–B4) |

## Scores por dimensión (delta vs Sprint 10)

| Dimensión | Sprint 10 | Ahora | Δ | Comité | Nota |
|-----------|----------:|------:|---:|--------|------|
| Código / Arquitectura | 5.5 | 7.0 | +1.5 | Microsoft | Seams sandbox, ADR-008/011, migraciones EF únicas; `IScriptExecutionEnvironment` registrado pero muerto |
| Seguridad | 3.5 | 6.5 | +3.0 | OWASP | C1/C2/C3 pasan de "No mitigado" a Parcial; residuales reales (docker.sock, pivot SQL, redirects) |
| Performance | 5.0 | 8.0 | +3.0 | Netflix | `PagedSummaryAsync` sin `Include(Results)`, índices, Redis backplane; dashboard aún hidrata Results |
| UX | 5.5 | 7.5 | +2.0 | Google | Tema claro/oscuro, CommandPalette, DataTable, pickers en Approvals, formularios por herramienta |
| Testing | 4.0 | 6.0 | +2.0 | ISTQB | 301 tests backend + 25 frontend verdes; E2E smoke real en CI; critical-path superficial, floors simbólicos |
| Producto | 4.0 | 8.5 | +4.5 | ISTQB / Product | Claims = realidad: UserStory link UI, panel IA, Defects CRUD, README honesto ("No shipped: ADO/Jira/Xray") |
| DevOps | 4.0 | 7.5 | +3.5 | Amazon | Migrate off-boot con hard guard, non-root, sin puertos DB/Redis, CI test→e2e→deploy GHCR; Hangfire DDL residual |
| IA | 4.5 | 7.0 | +2.5 | OpenAI | GET AiAnalysis + panel SPA con confidence/evidence quote; sin evaluación de calidad de diagnósticos |
| **Global (seguridad ×2)** | **4.5** | **7.2** | **+2.7** | — | |

## Estado de los 3 hallazgos Critical de Sprint 10

### Critical 1 — RCE por runners sin sandbox → **Parcial (6/10)**

**Remediado:** Newman/Playwright/JMeter/Selenium IDE/Visual/codegen pasan por `DockerSandboxedProcessExecutor` con `--rm --network none --user 1000:1000 --read-only --cap-drop ALL --security-opt no-new-privileges --memory 512m`; fuera de Development se fuerza Docker sin fallback silencioso (`DependencyInjection.cs:102-125`); cleanup de huérfanos por prefijo de runId.

**Gaps verificados:**
- ~~`docker.sock` montado en el contenedor API~~ → **B1 mitigado (Sprint 17-A, 2026-07-17):** el `api` ya no monta el socket ni usa `group_add`/`DOCKER_GID`; el acceso al daemon pasa por `docker-socket-proxy` con allowlist (`CONTAINERS`/`IMAGES`/`POST`) en red interna `docker-control`, vía `DOCKER_HOST` (ADR-012). **Residual del proxy:** `POST /containers/create` sigue permitido → un contenedor nuevo aún puede montar rutas del host; cierre definitivo en 17-B (agente/daemon remoto).
- ~~ZAP host-CLI / `PreferHostDockerCli`~~ → **B4 mitigado (Sprint 19-A):** `ZapScanRunner` usa `ISandboxedProcessExecutor` + `SandboxImageZap` (`ghcr.io/zaproxy/zaproxy:stable`), sin `PreferHostDockerCli`; `targetUrl` pasa por `ISsrfGuard.ValidateOutboundUri` y se cita con `QuoteArg` en argv (`TestRunners.cs:440-466`). **Residual:** no hay allowlist de URLs de escaneo por proyecto (solo SSRF global + AllowHttp/PrivateHosts); con `bridge`, ZAP aún hace egress hacia el target validado.
- ~~`--cpus`/`--pids-limit` / red `host`~~ → **B8 mitigado (Sprint 19-B):** `BuildDockerRunArguments` emite `--memory` / `--cpus` / `--pids-limit` (`DockerSandboxedProcessExecutor.cs:153-160`); `NormalizeNetwork` solo admite `none`|`bridge` y veta `host` (`:219-228`); gancho `SandboxNetworkName` en `bridge` (`:235-241`). **Residual:** `bridge` sin firewall de destinos a nivel app (solo red Docker dedicada por ops); workspace de evidencia sin purge post-run.
- `IScriptExecutionEnvironment` registrado en DI pero no usado: workspace real = `storage/evidence/runs/{runId}` sin borrado post-run ni cuota.
- `DockerSandboxE2ETests` no ejercita el executor real (hardcodea `docker run`).
- Imagen del API aún instala Node + Playwright + Newman (legado, superficie CVE).

### Critical 2 — Connection strings del cliente / SSRF → **Parcial (6.5/10)**

**Remediado:** el vector original está cerrado — `StartDatabaseValidation` rechaza connection strings del browser (extension data + heurística), resolución server-side (`ProjectDatabaseConnectionResolver`), args Hangfire solo con runId/projectId/nombres de entorno; `SsrfGuard` bloquea metadata, RFC1918, loopback, ULA, mapped IPv6, decimal IP, DNS a privadas; AES-256-GCM para tokens y connection strings en reposo; GET de entornos sin secreto.

**Gaps verificados:**
- ~~Pivot SQL vía Upsert sin allowlist~~ → **B2 mitigado (Sprint 18-A):** `SqlHostGuard` + `DatabaseValidation:AllowedSqlHosts`; Upsert valida antes de cifrar (`DatabaseValidationCommands.cs`); `SqlServerSchemaValidator.CompareAsync` revalida origen/destino antes de abrir conexión. **Residual:** allowlist mal configurada sigue permitiendo SQL privados intencionalmente; rol `ManageProjects` puede Upsert hosts listados.
- ~~Redirects 3xx / DNS rebinding / tester sin SSRF~~ → **B3 mitigado (Sprint 18-B):** `AllowAutoRedirect=false` + revalidación por hop en `SsrfOutboundHandler`; `ConnectCallback` con pin de IP (`SsrfHttpHandlerFactory`); `IntegrationConnectionTester` usa named client `"notifications"`. **Residual:** clientes sin factory; pin no cubre cambios DNS post-connect.
- ~~`EncryptionKey` vacía / secretos en launchSettings / Target en claro~~ → **B7 mitigado (Sprint 18-C):** fail-fast en `Program.cs` + ctor `AesTokenEncryptionService`; `launchSettings.json` sin secretos; GET con `TargetHint`; catch de dispatcher sin Target. **Residual:** warnings de `PostJsonAsync` pueden incluir URL de webhook.

### Critical 3 — Sin ACL por proyecto (IDOR / TM-01) → **Parcial fuerte (8/10)**

**Remediado:** `ProjectMember` + `ProjectAccessService` (Admin bypass, denegación 404 anti-enum) cubren Projects, TestRuns, TestCases, Defects, Catalog, Evidence (por Id con ownership y path legacy acotado a `Evidence.FilePath` del run), Approvals, Dashboard, Traceability, Integrations y `TestRunHub` (membresía en `SubscribeToRun`). Tests `Sprint11IdorTests` + `ProjectAccessServiceTests` cubren A≠B.

**Gaps verificados:**
- `GetAuditLogQuery` sin filtro de proyecto: cualquier `ManageProjects` lee rutas/emails/IPs de todos los tenants (`GetAuditLogQuery.cs:21-27`).
- Quality Gates globales: CRUD sin membresía (`QualityGateCommands.cs:67-77` y update/delete).
- `RoleInProject` decorativo: no hay RBAC intra-proyecto (Member ≡ ProjectAdmin).
- No existe API de gestión de miembros (solo creador + seed) — riesgo de workarounds inseguros.

## DoD sprints 11–16 (re-verificación)

| Sprint | DoD declarado | Re-verificado |
|--------|---------------|---------------|
| 11 ACL & Evidence | ✅ | **Confirmado** con gaps menores (audit log, QG globales fuera del scope original) |
| 12 Secrets & SSRF | ✅ | **Confirmado según redacción**; el impacto (pivot SQL interno) sigue posible vía Upsert |
| 13 Runner Sandbox | ✅ | **Confirmado en path principal**; residuales aceptados siguen siendo explotables (sock, bridge, ZAP) |
| 14 Producto honesto | ✅ | **Confirmado**: UserStory link, panel IA, Defects create/transition, pickers, README honesto |
| 15 Escala & realtime | ✅ | **Confirmado**: proyección paginada, índices (migración + SQL + fluent), Redis condicional, token fuera de query en prod |
| 16 Ops & CI | ✅ | **Confirmado con matices**: migrate off-boot con hard guard; Hangfire `PrepareSchemaIfNecessary` chocará con usuario least-privilege en primer boot; frontend sin HEALTHCHECK; floors de cobertura simbólicos (frontend 2.5% líneas); `azure-pipelines.yml` desfasado del `ci.yml` |

## Riesgos residuales priorizados

| # | Riesgo | Severidad | Origen |
|---|--------|-----------|--------|
| ~~B1~~ | `docker.sock` montado en API → RCE = host — **mitigado en Sprint 17-A** (socket-proxy con allowlist + `DOCKER_HOST`, sin socket ni `group_add` en `api`). Residual del proxy: `POST /containers/create` (cierre en 17-B) | ~~Critical residual~~ → **Mitigado (residual medio)** | Sprint 13 → 17-A |
| ~~B2~~ | Pivot SQL vía Upsert sin allowlist — **mitigado en Sprint 18-A** (`SqlHostGuard` + `AllowedSqlHosts` en Upsert y `SqlServerSchemaValidator`). Residual: allowlist operativa mal configurada | ~~Alta~~ → **Mitigado (residual medio-operativo)** | Sprint 12 → 18-A |
| ~~B3~~ | SSRF por redirects 3xx + DNS rebinding — **mitigado en Sprint 18-B** (`AllowAutoRedirect=false`, revalidación por hop, pin de IP, tester con named client SSRF). Residual: clients fuera del factory | ~~Alta~~ → **Mitigado (residual bajo)** | Sprint 12 → 18-B |
| ~~B4~~ | ZAP host-CLI sin hardening + `targetUrl` interpolado — **mitigado en Sprint 19-A** (`ISandboxedProcessExecutor` + imagen ZAP oficial, sin `PreferHostDockerCli`; `ValidateOutboundUri` + `QuoteArg`). Residual: sin allowlist de scan-URL por proyecto; egress en `bridge` hacia target validado | ~~Alta~~ → **Mitigado (residual medio)** | Sprint 13 → 19-A |
| B5 | Hangfire DDL (`PrepareSchemaIfNecessary`) vs usuario least-privilege en primer boot prod | **Alta (operativa)** | Sprint 16 |
| B6 | Audit log y Quality Gates globales sin ACL de proyecto | Media | Sprint 11 |
| ~~B7~~ | `EncryptionKey` vacía / secretos en launchSettings / Target en claro — **mitigado en Sprint 18-C** (fail-fast boot+ctor, launchSettings limpio, `TargetHint`, catch sin Target). Residual: URL en logs de `PostJsonAsync` | ~~Media~~ → **Mitigado (residual bajo)** | Sprint 12 → 18-C |
| ~~B8~~ | `bridge`/`host` sin allowlist egress; sin `--cpus`/`--pids-limit` — **mitigado en Sprint 19-B** (`--memory`/`--cpus`/`--pids-limit`; `host` vetado; `SandboxNetworkName` en bridge). Residual: egress app-level en `bridge` solo vía red dedicada ops; disco de evidencia sin TTL | ~~Media~~ → **Mitigado (residual medio-operativo)** | Sprint 13 → 19-B |
| B9 | Coverage floors simbólicos + E2E critical-path superficial | Media | Sprint 16 |
| B10 | Dashboard con `Include(Results)`; frontend sin HEALTHCHECK; `azure-pipelines.yml` obsoleto | Baja | Sprints 15–16 |

## Go / No-Go

- **Enterprise / multi-tenant / datos de terceros: NO-GO condicional.** La regla del roadmap (Go solo con 11+12+13 cerrados) no se cumple: Sprint 13 queda en Parcial por residuales (proxy `POST /containers/create`, egress `bridge` sin allowlist app) y **B5** operativo abierto. **B1, B2, B3, B4, B7, B8 mitigados** (Sprint 17-A / 18 / 19); bloqueadores restantes para re-evaluación: **B5** (+ residual del proxy, cierre pleno en 17-B).
- **Interno single-tenant, scripts de confianza, red privada: GO** (mejora desde GO condicional: sandbox activo, ACL, secretos cifrados, boot limpio; ahora además sin socket Docker directo en el API; ZAP bajo sandbox endurecido).
- **Interno multi-proyecto (tenants internos): GO condicional** — cerrar B6 (audit/QG); B1 ya no es un residual asumido sino mitigado.

## Delta narrativo vs 4.5/10

Lo que en Sprint 10 eran tres Critical sin mitigación es hoy: ACL efectiva en el camino crítico (8/10), secretos fuera del browser y cifrados (6.5/10) y sandbox real para el path principal de scripts (6/10). Producto pasó de "marketed, not implemented" a claims verificables en UI y README (+4.5). El salto no llega a 9.5 porque los residuales de seguridad no son teóricos: `docker.sock`, pivot SQL por Upsert y ZAP sin hardening mantienen viva la cadena "compromiso de cuenta QA → red interna / host".

## Ruta a ≥9.5 (Sprints 17–21)

Prompts listos para ejecutar por la IA (1 sesión = 1 prompt): [AI-Prompts-Sprints-17-21.md](./AI-Prompts-Sprints-17-21.md).

| Sprint | Cierra | Foco |
|--------|--------|------|
| **17** | B1 | Sacar la orquestación Docker del proceso API (socket-proxy con allowlist → `IContainerOrchestrator` hacia agente/daemon remoto). **Prerequisito del GO enterprise.** |
| **18** | B2, B3, B7 | Allowlist de hosts SQL en validación de BD; `AllowAutoRedirect=false` + revalidación por hop + pin de IP; fail-fast de `EncryptionKey` y secretos fuera del repo. |
| **19** | B4, B8 | ZAP vía `ISandboxedProcessExecutor` con `targetUrl` validado; `--cpus`/`--pids-limit`, veto de red `host`. |
| **20** | B5 | `PrepareSchemaIfNecessary=false` + esquema Hangfire creado en `db-init`/`migrate` con usuario privilegiado. |
| **21** | B6, B9, B10 | ACL en audit log/quality gates + `RoleInProject`; floors reales (backend ≥60 %, frontend ≥30 %) + E2E critical-path completo; dashboard sin `Include(Results)`, HEALTHCHECK frontend, `azure-pipelines.yml` alineado. |

Tras 17–21 → re-auditoría final: GO enterprise exige **B1–B5 cerrados** y 11/12/13 realmente completos.

**Actualización 2026-07-18:** re-auditoría de seguimiento tras Sprint 17 (B1) → [Sprint-17-Reaudit.md](./Sprint-17-Reaudit.md) (score **7.4/10**; B1 mitigado, B2–B10 pendientes; sigue NO-GO enterprise).

**Actualización 2026-07-19 (Sprint 18 VERIFY):** B2, B3 y B7 **mitigados en código** (allowlist SQL, redirects+pin SSRF, EncryptionKey fail-fast / secretos fuera de launchSettings / TargetHint). Residuales explícitos en la tabla de riesgos. Bloqueadores abiertos relevantes entonces: **B4, B5, B6, B8–B10**. Sigue **NO-GO enterprise**.

**Actualización 2026-07-19 (Sprint 19 VERIFY):** B4 y B8 **mitigados en código** (ZAP vía sandbox + SSRF/`QuoteArg`; `--cpus`/`--pids-limit`/`--memory`; `host` vetado; `SandboxNetworkName`). Residuales: sin allowlist de scan-URL por proyecto; `bridge` sin firewall app; evidencia sin purge. Bloqueadores abiertos relevantes: **B5, B6, B9, B10** (+ residual B1 del proxy). Sigue **NO-GO enterprise**.

**Actualización 2026-07-19 (Sprint 21 VERIFY):** B6, B9 y B10 **siguen abiertos** — no hay implementación 21-A/B/C. Evidencia y checklist: [Sprint-21-VERIFY.md](./Sprint-21-VERIFY.md). Único matiz B6: `AssignGateToProject` con ACL (`QualityGateCommands.cs:101`); audit log, CRUD de gates y `RoleInProject` sin enforcement. Floors: Unit 44 / Integration 11 / FE lines 2.5. CI e2e = smoke only. Dashboard aún `Include(Results)`; frontend sin HEALTHCHECK; `azure-pipelines.yml` desfasado sin marcar. Sigue **NO-GO enterprise** / **No-Go parcial** para el DoD de Sprint 21.

**Actualización 2026-07-19 (Sprint 21 Reaudit):** re-auditoría completa del comité → [Sprint-21-Reaudit.md](./Sprint-21-Reaudit.md) (score **7.5/10**; B5 cerrado; B1 mitigado≠cerrado; B6/B9/B10 abiertos; **NO-GO enterprise**).

---

*Re-auditoría Sprints 11–16 — comité independiente simulado. Baseline: [Sprint-10-Final-Audit.md](./Sprint-10-Final-Audit.md). Canvas interactivo: `sprint-16-reaudit.canvas.tsx`.*
