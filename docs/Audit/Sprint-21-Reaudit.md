# Re-auditoría B1–B10 (Sprint 21) — Comité Independiente

**Comité:** Microsoft · Google · Amazon · Netflix · OpenAI · OWASP · ISTQB  
**Premisa:** no asumir calidad; todo hallazgo re-verificado en código fuente y ejecución real (desde cero).  
**Baselines:** Sprint 10 (**4.5/10**) · Sprint 16 (**7.2/10**) · Sprint 17 (**7.4/10**) · Sprint 21 VERIFY (B6/B9/B10 abiertos).  
**Fecha:** 2026-07-19  
**Evidencia de ejecución:** `dotnet test QAGuardian.sln` → **373 unit + 11 integration** en verde (0 fallos). Frontend `npm run test:coverage` → **30/30** en verde; lines/statements **3.07%** (umbral configurado 2.5%).

## Veredicto

| Pregunta | Respuesta |
|----------|-----------|
| Score global | **7.5 / 10** |
| Delta vs Sprint 17 (7.4) | **+0.1** |
| Delta vs Sprint 16 (7.2) | **+0.3** |
| Delta vs Sprint 10 (4.5) | **+3.0** |
| ¿Alcanza la barra 9.5? | **No** |
| **Go Live Enterprise / multi-tenant / datos de terceros** | **NO-GO** |
| Uso interno single-tenant, red privada | **GO** |
| Uso interno multi-proyecto reforzado | **GO condicional** (cerrar B6; no confiar en RoleInProject) |

> **Regla del roadmap:** GO enterprise solo con **B1–B5 cerrados** y Sprints 11/12/13 realmente completos.  
> **Clasificación estricta:** “mitigado con residual material” **≠** “cerrado”.  
> Hoy: **B5 cerrado** (residual operativo bajo); **B2/B3/B7 mitigados** (residual no explotable por defecto); **B4/B8 mitigados** (residual medio); **B1 mitigado, no cerrado** (residual material del proxy). **B6/B9/B10 abiertos.** → **NO-GO.**

## Scores por dimensión

| Dimensión | S10 | S16 | S17 | Ahora | Δ(S17) | Comité | Nota |
|-----------|----:|----:|----:|------:|-------:|--------|------|
| Código / Arquitectura | 5.5 | 7.0 | 7.2 | 7.4 | +0.2 | Microsoft | Hangfire off-DDL + SQL de esquema; seams sandbox/SSRF/SQL guard vivos; `IScriptExecutionEnvironment` sigue sin uso real; 17-B (`IContainerOrchestrator`) ausente |
| Seguridad | 3.5 | 6.5 | 6.9 | 7.5 | +0.6 | OWASP | B2/B3/B4/B5/B7/B8 confirmados en código; techo por residual B1 + B6 abierto |
| Performance | 5.0 | 8.0 | 8.0 | 7.8 | −0.2 | Netflix | Dashboard sigue `ListWithResultsAsync` → `Include(Results)` (B10) |
| UX | 5.5 | 7.5 | 7.5 | 7.5 | 0.0 | Google | Sin cambios materiales en este ciclo |
| Testing | 4.0 | 6.0 | 6.1 | 6.2 | +0.1 | ISTQB | 384 backend + 30 FE verdes; floors aún simbólicos vs meta ≥60/≥30 (B9) |
| Producto | 4.0 | 8.5 | 8.5 | 8.5 | 0.0 | ISTQB / Product | Claims verificables; sin cambio en este ciclo |
| DevOps | 4.0 | 7.5 | 7.7 | 8.1 | +0.4 | Amazon | B5 cerrado en path compose; FE sin HEALTHCHECK; azure-pipelines desfasado (B10) |
| IA | 4.5 | 7.0 | 7.0 | 7.0 | 0.0 | OpenAI | Sin cambios |
| **Global (seguridad ×2)** | **4.5** | **7.2** | **7.4** | **7.5** | **+0.1** | — | |

## Critical originales (re-verificados)

### Critical 1 — RCE por runners sin sandbox → **Parcial (7.0/10)**

**Confirmado mitigado en path principal:**
- API **no** monta `docker.sock` ni `group_add`; solo `docker-socket-proxy` tiene el socket RO (`docker-compose.yml:96-128`); API usa `DOCKER_HOST=tcp://docker-socket-proxy:2375` (`:148-151`) y volumes solo `evidence` (`:174-175`).
- ZAP vía `ISandboxedProcessExecutor` + `ValidateOutboundUri` + `QuoteArg`, sin `PreferHostDockerCli` (`TestRunners.cs:440-466`).
- `--memory` / `--cpus` / `--pids-limit`; red solo `none|bridge`; `host` vetado (`DockerSandboxedProcessExecutor.cs:153-160`, `219-228`).

**Residual material (impide “cerrado” en C1 / B1):**
- Proxy con `CONTAINERS=1` + `POST=1` (`docker-compose.yml:100-102`) → `POST /containers/create` puede montar FS del host. **No es aislamiento total** (comentario ADR en compose `:94-95`). Cierre pleno = 17-B (agente/daemon remoto). No existe `IContainerOrchestrator` en el árbol `src/`.

### Critical 2 — Connection strings / SSRF → **Parcial fuerte (8.0/10)**

**Confirmado:**
- B2: `SqlHostGuard` en Upsert **antes** de cifrar (`DatabaseValidationCommands.cs:261-265`) y revalidación en `SqlServerSchemaValidator.cs:22-25`.
- B3: `AllowAutoRedirect=false` + revalidación por hop (`SsrfOutboundHandler.cs:10-60`, `MaxRedirectHops=3`) + pin IP (`SsrfHttpHandlerFactory` `:107-130`).
- B7: fail-fast `EncryptionKey` en `Program.cs:63-74` y ctor `AesTokenEncryptionService`; `launchSettings.json` sin secretos embebidos; DTO con `TargetHint` (`NotificationCommands.cs:11-48`).

**Residual:** allowlist SQL mal configurada (ops); HttpClients fuera del factory SSRF; URL posible en logs de webhooks.

### Critical 3 — ACL / IDOR → **Parcial fuerte (8.0/10)**

**Confirmado en camino crítico:** `ProjectAccessService` membresía + 404 anti-enum; `AssignGateToProject` con ACL (`QualityGateCommands.cs:101`).

**Abierto (B6 — sin implementación 21-A):**
- `GetAuditLogQueryHandler` pagina **todo** el audit sin filtro de proyecto ni `IProjectAccessService` (`GetAuditLogQuery.cs:21-27`).
- CRUD global de quality gates sin membresía: Create `:47-62`, List `:73-77`; Update/Delete sin access service.
- `RoleInProject` decorativo: `ProjectAccessService.cs:31-45` solo comprueba membresía activa / admin; Member ≡ ProjectAdmin.

## Tabla B1–B10 (estado estricto)

| # | Bloqueador | Estado | Evidencia (archivo:línea) | Residual |
|---|------------|--------|---------------------------|----------|
| **B1** | docker.sock en API → RCE = host | **Mitigado (no cerrado)** | `docker-compose.yml:96-128` (proxy), `:148-151` (`DOCKER_HOST`), API sin bind del socket (`:174-175`) | **Material:** `POST=1` + `CONTAINERS=1` (`:100-102`) permite create con bind mounts del host |
| **B2** | Pivot SQL Upsert sin allowlist | **Mitigado** | `SqlHostGuard.cs`; Upsert `DatabaseValidationCommands.cs:261-265`; `SqlServerSchemaValidator.cs:22-25` | Allowlist operativa mal configurada; rol con `ManageProjects` puede Upsert hosts listados |
| **B3** | SSRF redirects + DNS rebinding | **Mitigado** | `SsrfOutboundHandler.cs:23-60`; `SsrfHttpHandlerFactory.cs:107-130`; DI named clients | Clients HTTP fuera del factory; pin no cubre cambio DNS post-connect |
| **B4** | ZAP host-CLI / targetUrl | **Mitigado** | `TestRunners.cs:440-466` | Sin allowlist de scan-URL por proyecto; egress `bridge` hacia target validado |
| **B5** | Hangfire DDL vs least-privilege | **Cerrado** | Default `false` `DependencyInjection.cs:207-208`; `appsettings.json:11-13`; compose `:157-158`; esquema `database/05-hangfire-schema.sql` vía db-init (`docker-compose.yml:54-88`) | Operativo bajo: override `PrepareSchemaIfNecessary=true` o deploy fuera de compose sin SQL |
| **B6** | Audit / QG / RoleInProject | **Abierto (parcial mínimo)** | Audit `GetAuditLogQuery.cs:21-27`; QG CRUD global; solo assign con ACL `:101`; `ProjectAccessService.cs:31-45` ignora rol | Cross-tenant audit + gates globales + RBAC intra-proyecto decorativo |
| **B7** | EncryptionKey / secretos launch | **Mitigado** | `Program.cs:63-74`; `AesTokenEncryptionService` ctor; launchSettings limpio; `TargetHint` | URL de webhook en algunos logs |
| **B8** | cpus/pids / red host | **Mitigado** | `DockerSandboxedProcessExecutor.cs:153-160`, `219-241`; defaults `appsettings.json:23-28` | Egress app-level en `bridge` solo vía red dedicada ops; evidencia sin purge TTL |
| **B9** | Floors + E2E critical-path | **Abierto** | Unit floor **44** (`UnitTests.csproj:7`); Integration **11**; FE lines **2.5** (`vite.config.ts:48-52`); CI solo `smoke.spec.ts` (`.github/workflows/ci.yml:55,104`); `critical-path.spec.ts:32-33` corta en diálogo | Meta Sprint 21 ≥60/≥30 no alcanzada; critical-path no es gate |
| **B10** | Dashboard / HEALTHCHECK / Azure | **Abierto** | `DashboardQueries.cs:121-123` → `Repositories.cs:92-97` `Include(Results)`; `frontend/Dockerfile:12-15` sin HEALTHCHECK; `azure-pipelines.yml:14-57` sin coverage/e2e/GHCR | N+1 dashboard; orquestación FE ciega; falsa parity Azure vs GHA |

**Conteo estricto:** Cerrados **1/10** (B5). Mitigados **6/10** (B1–B4, B7, B8) — de los cuales B1 **no** cuenta como cerrado para GO. Parcial/Abierto **3/10** (B6, B9, B10).

## DoD sprints 11 / 12 / 13 (¿realmente completos?)

| Sprint | ¿Completo? | Motivo |
|--------|------------|--------|
| **11** ACL & Evidence | **No** | Camino crítico OK; B6 (audit/QG/RoleInProject) impide “completo” multi-tenant |
| **12** Secrets & SSRF | **Casi** | B2/B3/B7 mitigados en código; residuales ops/bajo |
| **13** Runner Sandbox | **No** | Path principal sandbox OK; B1 residual material del proxy + ausencia 17-B |

## Go / No-Go enterprise (estricto)

**NO-GO.**

Condición fallida:
1. **B1–B5 cerrados?** → **No.** B5 sí. B2/B3/B7/B4/B8 = mitigados (B4 residual medio). **B1 = mitigado ≠ cerrado** (residual `POST /containers/create` sigue siendo material).
2. **Sprints 11/12/13 realmente completos?** → **No** (11 y 13).
3. Además **B6/B9/B10 abiertos** (Sprint 21 no implementó 21-A/B/C) — no son prerequisito explícito del GO enterprise de B1–B5, pero confirman distancia a ≥9.5.

**Interno single-tenant / red privada:** **GO** (sandbox, ACL de camino crítico, secretos cifrados, Hangfire sin DDL en app, sin socket directo en API).

## Riesgos residuales priorizados

| Prioridad | Riesgo | Severidad | Estado |
|-----------|--------|-----------|--------|
| 1 | Proxy permite create+bind mount → RCE = host (B1) | Critical residual | Mitigado, no cerrado |
| 2 | Audit log + QG CRUD cross-tenant (B6) | Media-Alta | Abierto |
| 3 | Floors simbólicos + E2E no crítico en CI (B9) | Media | Abierto |
| 4 | ZAP sin allowlist URL por proyecto / egress bridge (B4/B8) | Media | Mitigado |
| 5 | Dashboard `Include(Results)` + FE sin HEALTHCHECK + azure desfasado (B10) | Baja-Media | Abierto |
| 6 | Allowlist SQL / clients SSRF fuera de factory (B2/B3) | Baja-Media ops | Mitigado |
| 7 | Hangfire override PrepareSchema (B5) | Baja ops | Cerrado con residual |

## Evidencia de tests (2026-07-19)

| Suite | Resultado | Notas |
|-------|-----------|-------|
| Unit | **373** passed, 0 fail | `dotnet test QAGuardian.sln` |
| Integration | **11** passed, 0 fail | mismo comando |
| Frontend coverage | **30** passed | lines/statements **3.07%**, branches 55.65%, functions 26.31%; umbrales 2.5/2.5/20/45 OK |
| Pre-test | Proceso `QAGuardian.API` detenido si existía | — |

Los umbrales **configurados** pasan; las **metas DoD Sprint 21** (≥60 % backend / ≥30 % FE lines) **no**.

## Ruta residual a ≥9.5

1. **17-B:** sacar orquestación Docker del API (agente/daemon remoto) → **cerrar B1**.
2. **21-A:** ACL en audit log + QG CRUD + enforcement `RoleInProject` + tests IDOR → **cerrar B6**.
3. **21-B:** floors reales (≥60/≥30 o máximo honesto documentado) + critical-path E2E en CI → **cerrar B9**.
4. **21-C:** dashboard sin `Include(Results)`; HEALTHCHECK frontend; azure-pipelines alineado o marcado no oficial → **cerrar B10**.
5. Opcional endurecimiento: allowlist scan-URL ZAP por proyecto; purge workspace evidencia; eliminar `PreferHostDockerCli` legacy.

Tras eso → re-auditoría final. GO enterprise exige **B1 cerrado de verdad** (no solo proxy) + B2–B5 estables + 11/12/13 completos.

## Delta narrativo

Desde Sprint 17 (7.4) el movimiento (+0.1) es deliberadamente pequeño: la mayor parte del trabajo de seguridad (18/19/20) ya estaba en código y se **confirma** aquí, no se inventa. B5 pasa de abierto a **cerrado**. B6/B9/B10 **siguen abiertos** (VERIFY 21 honesto). El residual del proxy en B1 sigue siendo el veto enterprise. No se infla el score: 7.5 refleja progreso real con techo de seguridad por B1+B6.

---

*Re-auditoría Sprint 21 — comité independiente simulado. Baselines: [Sprint-10-Final-Audit.md](./Sprint-10-Final-Audit.md), [Sprint-16-Reaudit.md](./Sprint-16-Reaudit.md), [Sprint-17-Reaudit.md](./Sprint-17-Reaudit.md), [Sprint-21-VERIFY.md](./Sprint-21-VERIFY.md). Canvas: `sprint-21-reaudit.canvas.tsx`.*
