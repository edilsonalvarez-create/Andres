# Prompts IA — Remediación Sprints 11–16

Uso: copiar **un prompt por sesión de IA**. No mezclar sprints. Completar DoD antes de abrir el siguiente sprint (salvo 14 ∥ 12/13).

## Contrato global (pegar al inicio de cada prompt)

```text
Eres un agente de implementación en el repo QAGuardian
(ruta local: QAGuardian / Clean Architecture .NET 9 + React/Vite).

Reglas:
- No asumas que el sistema está seguro; verifica en código.
- Cambios mínimos y enfocados al scope del prompt. Sin refactors colaterales.
- No borres qaguardian-dev.db ni secretos. No hagas force-push.
- No commits salvo que el usuario lo pida explícitamente.
- Al terminar: lista archivos tocados, cómo probar, residual risks, DoD checkboxes.
- Stack: Application (MediatR/CQRS) → Infrastructure → API; frontend en frontend/src.
- Idioma UI/docs existentes: español. Código/nombres: inglés consistente con el repo.
```

## Orden de ejecución

```text
11-A → 11-B → 11-C → 11-D → 11-VERIFY
12-A → 12-B → 12-C → 12-VERIFY
13-A → 13-B → 13-C → 13-VERIFY
14-A → 14-B → 14-C → 14-D → 14-VERIFY   (puede ∥ 12/13)
15-A → 15-B → 15-VERIFY
16-A → 16-B → 16-C → 16-VERIFY
→ Re-auditoría Sprint 10
```

## Anclas de código (mapa verificado)

Hechos confirmados en `src/` (no asumir lo contrario):

| Hecho | Ubicación |
|-------|-----------|
| **No existe** `ProjectMember` en Domain/DbContext | Solo recomendado en `docs/Security/Threat-Model.md` (TM-01) |
| Auth = policies de rol; `ViewReports` = autenticado | `src/QAGuardian.API/Program.cs` |
| Evidence download por `path` + `testRunId`; sin ownership Evidence↔Run | `TestRunsController.DownloadEvidence`, `DownloadEvidenceQuery.cs` |
| Evidence entity: `FilePath` vía `TestResult` | `Domain/Entities/Evidence.cs`, `FileEvidenceStorage.cs` |
| Conn strings del cliente → Hangfire args | `DatabaseValidationCommands.cs`, `HangfireJobScheduler.EnqueueDatabaseValidation` |
| SSRF: solo check HTTPS en notificaciones; sin block IP/metadata | `NotificationCommands.cs`, `NotificationDispatcher.cs` |
| Runners en proceso API (`ProcessExecutor`) | `Runners/ProcessExecutor.cs`, `TestRunners.cs`, `API/Dockerfile` (Node/Playwright en imagen API) |
| Link UserStory API sí; UI Casos no | `TestCasesController.LinkUserStory` vs `frontend/src/pages/TestCasesPage.tsx` |
| Approvals: GUID libre | `frontend/src/pages/ApprovalsPage.tsx` |
| `AiAnalysis` se persiste; sin API/UI lectura | `ExecuteTestRunCommandHandler`, `TestRunsPage.tsx` |
| Defects API create/status sí; UI read-only | `DefectsController` vs `DefectsPage.tsx` |
| README afirma Azure DevOps | `README.md`; enum sin client |
| List runs: `PagedWithDetailsAsync` + `Include(Results)` | `Repositories.cs`, `GetTestRunsQueryHandler` |
| SignalR JWT en query | `Program.cs` `OnMessageReceived`; `TestRunsPage` `?access_token=` |
| Redis = cache, **no** backplane SignalR | `DependencyInjection.cs` |
| Faltan indexes `TestCase.UserStoryId`, `TestResult.TestCaseId` | `QAGuardianDbContext.cs` |
| Boot: SQL `MigrateAsync` / else `EnsureCreatedAsync` | `DbInitializer.cs` vía `Program.cs` |
| Compose publica `1433` y `6379` | `docker-compose.yml` |
| E2E Playwright existen; **no** en CI | `frontend/e2e/*` vs `.github/workflows/ci.yml` |

---

# Sprint 11 — Multi-tenant ACL & Evidence

**Objetivo:** cerrar IDOR de lectura y descarga.  
**Fuera:** sandbox, vault DB, UI producto.

### Prompt 11-ORCH (opcional — planificador)

```text
[Contrato global]

Actúa como tech lead. Solo planifica, NO implementes.

Sprint 11: ProjectMember ACL + Evidence download ownership.

Hecho verificado: NO existe ProjectMember en src/. Auth = Policies en Program.cs
(ViewReports = RequireAuthenticatedUser). TM-01 en docs/Security/Threat-Model.md.

Explora obligatoriamente:
- src/QAGuardian.API/Program.cs (Policies)
- Controllers: Projects, TestRuns, TestCases, Defects, Approvals, Traceability,
  Dashboard, QualityGates, Catalog, Integrations
- DownloadEvidenceQuery.cs + TestRunsController.DownloadEvidence
- Repositories.cs (IProjectRepository, ITestRunRepository, …)
- Domain/Entities/Project.cs, Evidence.cs, TestResult.cs
- Hubs/TestRunHub.cs (nota TM-01)

Entrega:
1) Diseño ProjectMember(UserId, ProjectId, Role) alineado a Threat-Model
2) IProjectAccessService vs filtros en repo
3) Download: preferir Evidence.Id; alt: path ∈ Evidence.FilePath del run
4) Lista handlers a tocar
5) Plan tests IDOR userA vs userB
6) Confirma split 11-A..C
```

### Prompt 11-A — Modelo ProjectMember + seed

```text
[Contrato global]

Sprint 11 · Prompt 11-A · Ítem 2 (parte dominio/persistencia)

Hecho: ProjectMember NO existe. Project en Domain/Entities/Project.cs sin collection de members.
Threat-Model TM-01 recomienda ProjectMember(UserId, ProjectId, Role).

Implementa membresía por proyecto:
1. Entidad Domain ProjectMember (ProjectId, UserId, RoleInProject, timestamps).
2. DbSet + fluent config en QAGuardianDbContext.
3. Crear tabla con el patrón del repo (EnsureCreated aditivo en DbInitializer y/o EF migration).
4. Seed en DbInitializer: admin miembro de todos los proyectos; demos si aplica.
5. IProjectMemberRepository en IRepositories.cs + Repositories.cs.

DoD parcial:
- Compila.
- Tabla existe en DB de desarrollo.
- Unit test básico add/list members.

Fuera: aún no filtres todos los handlers (eso es 11-B).
```

### Prompt 11-B — Enforcement ACL en queries/commands

```text
[Contrato global]

Sprint 11 · Prompt 11-B · Ítem 2 (enforcement)

Con ProjectMember ya existente (11-A):
1. Crea IProjectAccessService (CurrentUser + members): EnsureCanAccessProjectAsync(projectId), ListAccessibleProjectIdsAsync().
2. Aplica en handlers/repos de: Projects, TestRuns, TestCases, Defects, Dashboard, Reports/Traceability, Approvals, QualityGates, Catalog ligado a proyecto, Evidencias.
3. GetProjects debe listar SOLO proyectos accesibles (Admin global puede ver todos SOLO si el diseño lo documenta; default: membership).
4. GetById de recurso ajeno → 404 o 403 consistente (elige 404 para anti-enum y documéntalo).
5. Policies: no basta ViewReports=authenticated; la autorización real es por proyecto.

DoD parcial:
- Tests: usuario A no lee proyecto/run/caso de B.
- dotnet test en tests afectados en verde.

Fuera: download evidence (11-C), UI.
```

### Prompt 11-C — Evidence download atado al TestRun

```text
[Contrato global]

Sprint 11 · Prompt 11-C · Ítem 4

Hoy (verificado):
- GET evidence: TestRunsController.DownloadEvidence → DownloadEvidenceQuery(testRunId, path)
- Handler: rol allowlist + path traversal/ext; NO verifica path ∈ Evidences del run
- Entity Evidence.FilePath cuelga de TestResult; storage: FileEvidenceStorage.cs

Implementa:
1. Preferido: DownloadEvidenceQuery(testRunId, evidenceId) y resolver FilePath solo desde DB.
2. Alternativa: path debe match exacto Evidence.FilePath de Results del TestRun.
3. Caller con acceso al ProjectId del TestRun (IProjectAccessService de 11-B).
4. Actualiza frontend que descargue evidencias si usa path.

DoD:
- Evidence/path de otro run → 404/403.
- Path traversal sigue bloqueado.
- Tests unitarios/integración del handler.

Fuera: sandbox.
```

### Prompt 11-D / 11-VERIFY — Tests IDOR + Threat Model

```text
[Contrato global]

Sprint 11 · Prompt 11-VERIFY

Verifica DoD completo Sprint 11:
1. Suite IDOR: userA vs userB (projects, runs, cases, evidence download).
2. Actualiza docs/Security/Threat-Model.md TM-01 → Mitigated (o residual explícito).
3. Actualiza docs/Audit/Sprint-11-16-Remediation-Roadmap.md checkboxes Sprint 11.
4. Corre: dotnet test (unit + integration relevantes).
5. Resume hallazgos residuales.

Si algo falla, ARRÉGLALO en este prompt antes de cerrar.
No abras Sprint 12 con IDOR abierto.
```

---

# Sprint 12 — Secrets & SSRF

**Depende de:** Sprint 11 cerrado.  
**Fuera:** sandbox runners.

### Prompt 12-A — Entornos DB nombrados (sin conn string del cliente)

```text
[Contrato global]

Sprint 12 · Prompt 12-A · Ítem 3

Hoy (verificado):
- IntegrationsController.StartDatabaseValidation
- StartDatabaseValidationCommand / ExecuteDatabaseValidationCommand
  (SourceConnectionString, TargetConnectionString desde cliente)
- HangfireJobScheduler.EnqueueDatabaseValidation serializa sourceConn/targetConn
- SqlServerSchemaValidator abre SQL con esas strings
- DatabaseValidationRun persiste metadata (no necesariamente los secretos en entidad)

Implementa:
1. Config server-side: ProjectDatabaseEnvironment (Name, ConnectionString cifrada o IConfiguration/vault).
2. API acepta solo environment names (ej. "dev", "staging"), NUNCA connection strings del browser.
3. Resolver conn string solo en Infrastructure al ejecutar validación.
4. Migrar/adaptar UI de database validation si existe.
5. Hangfire: args = projectId + environment names; NUNCA conn strings en payload.

DoD parcial:
- POST validation con connectionString en body → 400.
- Job inspectable sin secretos en payload.

Fuera: SSRF webhooks (12-B).
```

### Prompt 12-B — Guard SSRF reutilizable

```text
[Contrato global]

Sprint 12 · Prompt 12-B · Ítem 5 (infra)

Hecho: NO hay guard SSRF en código; NotificationCommands solo exige HTTPS absoluto.
Checklist: docs/Security/Security-Checklist.md A10.

Crea UrlSafety / SsrfGuard en Infrastructure:
- Bloquear IPs privadas (RFC1918), loopback, link-local, IPv6 unique-local.
- Bloquear metadata cloud 169.254.169.254 / metadata.google.internal.
- Solo https (salvo allowlist explícita dev).
- DNS resolve + validar IP resultante (mitigar DNS rebinding básico).
- API clara: ValidateOutboundUri(uri) → Result.

Tests unitarios exhaustivos (casos arriba).

Fuera: aún no cablear todos los callers (12-C).
```

### Prompt 12-C — Aplicar SSRF a webhooks e integraciones

```text
[Contrato global]

Sprint 12 · Prompt 12-C · Ítem 5 (wiring)

Aplica SsrfGuard antes de HTTP outbound controlado por usuario:
- NotificationDispatcher.PostJsonAsync / SendTeams|Slack|Discord
- NotificationCommands (validator destino)
- IntegrationConnectionTester
- SonarQubeClient / GitHubApiClient si base URL es configurable por proyecto
- HttpClient "notifications" en DependencyInjection.cs

DoD:
- Webhook a http://169.254.169.254/ → rechazado.
- Tests unit/integration del dispatcher/validator.
- Documentar allowlist si hay excepciones.

Fuera: sandbox.
```

### Prompt 12-VERIFY

```text
[Contrato global]

Sprint 12 · VERIFY

Checklist:
- [ ] API no acepta connection string arbitraria del browser
- [ ] Tests SSRF (metadata + RFC1918) verdes
- [ ] Hangfire sin secretos de cliente en plaintext
- [ ] Roadmap Sprint 12 checkboxes

Corre tests. Resume residual. No commits salvo pedido.
```

---

# Sprint 13 — Runner Sandbox

**Depende de:** idealmente Sprint 12.  
**Un solo ítem grande:** partir en diseño → infra → wiring → verify.

### Prompt 13-A — Diseño e interfaz (sin romper prod aún)

```text
[Contrato global]

Sprint 13 · Prompt 13-A · Ítem 1 (diseño + seams)

Hoy (verificado): Hangfire TestExecutionJob → ExecuteTestRunCommand → TestRunnerFactory
→ ProcessExecutor en el host API. API/Dockerfile instala Node/newman/Playwright Chromium
(sin USER non-root). ProcessExecutor es singleton en DependencyInjection.

Explora: ProcessExecutor.cs, TestRunners.cs (Playwright/Newman/JMeter/Zap),
SeleniumIdeTestRunner.cs, VisualRegressionRunner.cs, PlaywrightRecorder.cs,
ITestRunner.cs, API/Dockerfile, docker-compose.yml service api.

Diseña e implementa seams:
1. IScriptExecutionEnvironment / ISandboxedProcessExecutor
2. Feature flag: Runners:UseSandbox (Dev puede false; prod compose true)
3. Contrato: workdir efímero, env allowlist, timeout, stdout/stderr/artifacts
4. ADR corto en docs/Architecture/adr/

NO cablees todos los runners aún si bloquea Dev; interfaz + fallback local.

DoD parcial: compila + ADR + flag.
```

### Prompt 13-B — Implementación contenedor efímero

```text
[Contrato global]

Sprint 13 · Prompt 13-B · Ítem 1 (runtime)

Implementa sandbox real (reemplazo de ProcessExecutor.RunAsync para scripts de usuario):
1. Por TestRun/script: contenedor efímero (Docker API o docker run --rm).
2. Sin montar secretos de plataforma (.env API, JWT, SQL SA, Redis).
3. Network: egress deny o allowlist mínima documentada.
4. Copiar solo script + deps; artifacts → FileEvidenceStorage.
5. Timeout + cleanup huérfanos (nombre con runId).
6. Non-root dentro del contenedor runner.

DoD parcial:
- Al menos Playwright o Newman E2E vía sandbox en Docker.
- Prueba negativa: script no lee secrets del contenedor API.

Fuera: UI.
```

### Prompt 13-C — Cablear todos los runners + VERIFY

```text
[Contrato global]

Sprint 13 · Prompt 13-C + VERIFY

Cablear cuando flag on:
PlaywrightTestRunner, NewmanTestRunner, JMeterTestRunner, ZapScanRunner,
SeleniumIdeTestRunner, VisualRegressionRunner (y codegen si aplica).

1. API process NO ejecuta scripts de usuario vía ProcessExecutor host.
2. Docs Security + residual RCE.
3. DoD checklist roadmap Sprint 13.
4. Dev sin Docker: fallback solo Development + warning log.
```

---

# Sprint 14 — Producto honesto

**Puede paralelizarse** con 12/13 (otra sesión/rama).

### Prompt 14-A — UI link UserStory + Approvals pickers

```text
[Contrato global]

Sprint 14 · Prompt 14-A · Ítems 6

Backend (existe): PUT TestCasesController.LinkUserStory → LinkTestCaseToUserStoryCommand;
TestCase.UserStoryId; Catalog CreateUserStory / GetUserStoriesByRequirement.

Frontend gaps: TestCasesPage sin link UI; ApprovalsPage targetEntityId free-text;
CatalogPage ya lista/crea stories.

1. types.ts: userStoryId en TestCase.
2. TestCasesPage: selector User Story del proyecto (datos Catalog).
3. ApprovalsPage: pickers (no GUID paste).
4. /trazabilidad usable tras linkear desde UI.

DoD: matriz sin curl. Smoke login OK.
```

### Prompt 14-B — AiAnalysis API + panel Ejecuciones

```text
[Contrato global]

Sprint 14 · Prompt 14-B · Ítem 7

Hoy: ExecuteTestRunCommandHandler escribe AiAnalysis; DbSet AiAnalyses;
ClaudeAiAnalysisService / AiHeuristicEngine. TestRunsPage: SignalR progress, SIN panel IA.
No hay endpoint de lectura.

1. Query GetAiAnalysisForTestRun (o embeber en detalle) + ACL proyecto.
2. TestRunsPage: panel Diagnóstico IA (summary, confidence, evidenceQuote, recommendations).
3. Empty state si no hay análisis. Solo datos persistidos.

DoD: run con AiAnalysis visible en SPA.
```

### Prompt 14-C — Defects UI o bajar claims

```text
[Contrato global]

Sprint 14 · Prompt 14-C · Ítem 8

Hecho: DefectsController tiene Create + ChangeStatus; DefectCommands existen.
DefectsPage es tabla read-only titulada “Gestión de defectos”.

Opción A (preferida): UI create + transition usando API existente.
Opción B: bajar claim a “listado/consulta” en README/Manual y documentar gap.

No mientas capacidades.
```

### Prompt 14-D — README honestidad

```text
[Contrato global]

Sprint 14 · Prompt 14-D · Ítem 9

README.md afirma Azure DevOps. IntegrationType.AzureDevOps=2 en Enums.cs sin client.
TraceabilityQueries menciona Xray/qTest/TestRail en comentarios/docs.

Quita o marca “No implementado” ADO/Jira/Xray como shipped.
Deja integraciones reales (GitHub, Sonar si aplica).

DoD: grep sin promesas falsas de features shipped.
```

### Prompt 14-VERIFY

```text
[Contrato global]

Sprint 14 · VERIFY
Checklist roadmap 6–9. Smoke manual: link historia → matriz; run con IA → panel; README limpio.
```

---

# Sprint 15 — Escala & realtime

### Prompt 15-A — Indexes + list runs liviano

```text
[Contrato global]

Sprint 15 · Prompt 15-A · Ítem 10

Hoy: GetTestRunsQueryHandler → PagedWithDetailsAsync con Include(Results)
(+ GateEvaluation) en Repositories.cs. Indexes faltan: TestCase.UserStoryId,
TestResult.TestCaseId (ver QAGuardianDbContext).

1. List summary DTO sin Results; detail GetById con Includes.
2. HasIndex UserStoryId / TestCaseId en fluent + migration/EnsureCreated aditivo.
3. Ajustar handlers que solo necesitan conteos.
4. Reporta antes/después (tamaño respuesta o filas hidratadas).

DoD: GET list no hidrata Results. Tests verdes.
```

### Prompt 15-B — SignalR Redis + token fuera de query

```text
[Contrato global]

Sprint 15 · Prompt 15-B · Ítem 11

Hoy: Program.cs ExtractToken/OnMessageReceived lee ?access_token= para /hubs/*.
TestRunsPage: withUrl(`/hubs/testruns?access_token=${token}`).
Redis en DI = distributed cache only — NO SignalR backplane. Hub: TestRunHub.

1. AddStackExchangeRedis backplane cuando Redis connection set.
2. Frontend: negotiate/header auth — sin access_token en query.
3. Production: quitar o rechazar query token; Dev opcional + warning.
4. Documentar Redis obligatorio multi-réplica.

DoD: JWT no en URLs hub; progress multi-réplica documentado/probado.
```

### Prompt 15-VERIFY

```text
[Contrato global]
Sprint 15 VERIFY: indexes + list liviano + SignalR. Actualiza roadmap.
```

---

# Sprint 16 — Ops & CI release

### Prompt 16-A — Migraciones EF / migrate off-boot

```text
[Contrato global]

Sprint 16 · Prompt 16-A · Ítem 12

Hoy: DbInitializer.InitializeAsync — SQL Server → MigrateAsync; else EnsureCreatedAsync + seed.
Program.cs llama DbInitializer en boot salvo Database:SkipInitialization.
Ya existe Migrations/20260709052909_InitialCreate.cs.

1. Migrations EF = fuente de verdad (sin parches ad-hoc divergentes).
2. Production: NO Migrate/EnsureCreated en boot de API.
3. Job/contenedor migrate aparte (dotnet ef database update).
4. Dev helper documentado.
5. Manual-Instalacion + GO-LIVE-CHECKLIST.

DoD: arranque prod sin EnsureCreated/Migrate en proceso API.
```

### Prompt 16-B — Hardening images & compose

```text
[Contrato global]

Sprint 16 · Prompt 16-B · Ítem 13

Hoy: docker-compose publica sqlserver 1433:1433 y redis 6379:6379.
API/Dockerfile sin USER non-root; HEALTHCHECK → /health/live.

1. API + frontend images non-root.
2. Perfil prod: NO publicar 1433/6379 al host (red interna).
3. SQL least-privilege (no SA en app) documentado + ejemplo.
4. Secrets vía env/files; sin defaults inseguros en prod.

DoD: compose prod sin DB/Redis públicos.
```

### Prompt 16-C — CI E2E + coverage + rollback

```text
[Contrato global]

Sprint 16 · Prompt 16-C · Ítem 14

Hoy: .github/workflows/ci.yml = backend test+coverage, frontend test+build, Docker build;
SIN e2e/deploy/rollback. E2E locales: frontend/e2e/*.spec.ts + playwright.config.ts.
También: azure-pipelines.yml, pipelines/templates/quality-gate-pipeline.yml.

1. Job E2E smoke Playwright en CI (main/master).
2. Coverage floors honestos (fail si bajan del baseline real).
3. docs/Release: deploy + rollback concretos.
4. Flujo: test → e2e smoke → deploy → rollback doc.

DoD: CI reproducible; GO-LIVE-CHECKLIST actualizado.
```

### Prompt 16-VERIFY + cierre

```text
[Contrato global]

Sprint 16 · VERIFY + prep re-auditoría
- Roadmap 12–14 ítems cerrados
- Lista evidencia para re-auditoría tipo Sprint 10
- NO declarar 9.5 sin nueva auditoría independiente
```

---

# Prompt de re-auditoría (post 11–16)

```text
[Contrato global]

Actúa como el mismo comité Sprint 10 (Microsoft, Google, Amazon, Netflix, OpenAI, OWASP, ISTQB).
No asumas calidad. Re-audita desde cero focos Critical 1–3 y DoD de sprints 11–16.
Entrega: hallazgos, scores, riesgos, Go/No-Go, delta vs 4.5/10 anterior.
Actualiza docs/Audit/ y canvas si existe.
```

---

# Cómo usar en Cursor (recomendado)

| Sesión | Pegar |
|--------|--------|
| 1 | Contrato + **11-A** |
| 2 | Contrato + **11-B** |
| 3 | Contrato + **11-C** |
| 4 | Contrato + **11-VERIFY** |
| … | igual por sprint |
| Paralelo | rama `sprint-14/...` con 14-A mientras otra sesión hace 12-A |

**Regla de oro:** si un prompt no cierra DoD parcial, no abras el siguiente; usa un prompt “FIX residual Sprint N-X: …” citando el fallo.
