# Sprint 11 — Plan ORCH (solo diseño)

**Prompt:** 11-ORCH · **Modo:** planificar, no implementar  
**Fecha:** 2026-07-16  
**Estado TM-01 hoy:** Backlog (Threat-Model §3 / §5)

---

## 1) Diseño `ProjectMember` (alineado TM-01)

Threat-Model recomienda: `ProjectMember(UserId, ProjectId, Role)`.

### Entidad (Domain)

```text
ProjectMember : AuditableEntity (o BaseEntity + CreatedAt)
  Id              Guid
  ProjectId       Guid     (FK → Project, required)
  UserId          Guid     (FK → User, required)
  RoleInProject   enum/string   // ver abajo
  IsActive        bool     default true
  Unique index: (ProjectId, UserId) where not soft-deleted
```

### `RoleInProject` (Sprint 11 — mínimo viable)

No duplicar los 8 `SystemRoles` globales en el primer corte. Dos capas:

| Capa | Quién | Efecto |
|------|--------|--------|
| **Global** `SystemRoles` (JWT) | Ya existe | Capacidad de acción (`ExecuteTests`, `ManageDefects`, …) |
| **Project** membership | Nuevo | **Visibilidad / pertenencia** al proyecto |

Valores MVP de `RoleInProject`:

| Valor | Significado Sprint 11 |
|-------|------------------------|
| `Member` | Acceso de lectura/uso del proyecto (sujeto a policies globales) |
| `ProjectAdmin` | Puede gestionar membresía del proyecto (opcional Sprint 11; si no, solo `Administrador` global gestiona members) |

**Regla de autorización combinada (DoD IDOR):**

```text
puede_ver(proyecto) =
  (IsInRole(Administrator) && AllowGlobalAdminBypass)
  OR IsActiveProjectMember(userId, projectId)

puede_actuar(proyecto, policy) =
  puede_ver(proyecto) AND Authorize(policy)   // ExecuteTests, ManageDefects, etc.
```

**Decisión ORCH (recomendada):**

- `Administrator` global: bypass de membership (operación de plataforma). Documentar en Threat-Model.
- Todos los demás roles globales: **requieren** fila `ProjectMember` activa.
- No introducir UI de membresía en Sprint 11 (fuera de scope “UI producto”); seed + API interna mínima (`POST/GET members`) suficiente para tests IDOR. UI de assign puede ser Sprint 14+ o 11-extra si sobra tiempo.

### Persistencia

- `DbSet<ProjectMember>` en `QAGuardianDbContext`
- Índice único `(ProjectId, UserId)`
- Seed `DbInitializer`: admin → miembro de todos los proyectos; usuarios demo → proyectos demo conocidos
- Patrón de tabla: migration EF si SQL Server; parche aditivo EnsureCreated para SQLite (como Sprint 8)

### Relación con `Project`

- Navigation opcional `Project.Members` (collection) — útil; no obligatoria para enforcement.

---

## 2) `IProjectAccessService` vs filtros en repo

### Recomendación: **servicio de aplicación + enforcement en handlers** (no solo repo)

| Enfoque | Pros | Contras |
|---------|------|---------|
| Solo filtro en `IProjectRepository.List` | Simple para listados | Fácil olvidar en GetById por GUID de run/caso/evidencia; repos “puros” se ensucian con `ICurrentUser` |
| Solo policy ASP.NET `IAuthorizationHandler` | Centralizado HTTP | Hangfire/jobs y SignalR no pasan por el mismo pipeline fácilmente; MediatR handlers se saltan |
| **`IProjectAccessService` llamado en cada handler** | Un solo sitio de verdad; usable desde Hub, jobs, reports | Disciplina: hay que tocar cada handler |
| Híbrido: servicio + helper `EnsureCanAccessProjectAsync` | Mejor DX | — |

**Diseño elegido:**

```text
// Application.Abstractions
IProjectAccessService {
  Task EnsureCanAccessProjectAsync(Guid projectId, CancellationToken ct);
  // lanza ForbiddenAccessException o NotFoundException (ver §anti-enum)
  Task<bool> CanAccessProjectAsync(Guid projectId, CancellationToken ct);
  Task<IReadOnlyList<Guid>> ListAccessibleProjectIdsAsync(CancellationToken ct);
  Task EnsureCanAccessTestRunAsync(Guid testRunId, CancellationToken ct);
  // resuelve run.ProjectId internamente
}
```

Implementación en Infrastructure (o Application + repo members): usa `ICurrentUserService` + `IProjectMemberRepository`.

**Anti-enumeración:** recurso inexistente **o** sin acceso → **404** (`NotFoundException`), no 403 con “existe pero no tuyo”. Documentar. Excepción: acciones de admin membership pueden devolver 403 explícito.

**Repos:** no meter `ICurrentUser` dentro de cada método genérico `ListAsync` del repo genérico — rompe jobs de sistema. Los jobs Hangfire que ejecutan tests ya “pertenecen” al proyecto del run; deben usar identity de sistema o saltar ACL con `IProjectAccessService` overload `forSystem: true` solo en Infrastructure jobs.

---

## 3) Download evidence — contrato

### Estado actual (gap)

`DownloadEvidenceQuery(testRunId, path)`:

- Verifica run existe
- Rol en allowlist (Admin/QA/TechLead)
- Path traversal + extensiones
- **No** comprueba `path ∈ Evidence.FilePath` del run
- **No** comprueba membership del `run.ProjectId`

### Contrato preferido (Sprint 11-C)

```text
GET /api/v1/testruns/{testRunId}/evidence/{evidenceId}
DownloadEvidenceQuery(Guid TestRunId, Guid EvidenceId)
```

Flujo:

1. `EnsureCanAccessTestRunAsync(testRunId)` (ACL + 404)
2. Cargar `Evidence` por Id
3. Verificar que `Evidence.TestResultId` ∈ `TestResult` del `TestRunId` (Include Results o query join)
4. Abrir `Evidence.FilePath` vía `IEvidenceStorage` (sigue validando path seguro)
5. Stream

**Deprecar** query `?path=` en el mismo PR o marcar obsolete + 1 release; frontend debe migrar a `evidenceId` (buscar usos de `/evidence` en SPA).

### Alternativa aceptable (si breaking change duele)

Mantener `path` pero:

```text
normalizedPath must equal some Evidence.FilePath under that TestRun's Results
```

ORCH recomienda **Evidence.Id** como primario.

---

## 4) Lista de handlers / superficies a tocar

### Must-have Sprint 11 (IDOR lectura/escritura de datos de proyecto)

| Área | Handler / superficie | Check |
|------|---------------------|-------|
| Projects | `GetProjects*` / `GetProjectById` / create-update | List filtrado; GetById access |
| TestRuns | `GetTestRunsQuery`, `GetTestRunDetailQuery`, `StartTestRun`, `CancelTestRun` | project / run |
| TestRuns | `DownloadEvidenceQuery` | run + evidence ownership |
| TestRuns | Report / execution-matrix en controller (`IReportGenerator`) | resolver ProjectId del run **antes** de generar |
| TestCases | List/Get/Create/Update/`LinkTestCaseToUserStory` | project |
| Defects | List/Get/Create/ChangeStatus | project |
| Dashboard | queries con `projectId` | project |
| Traceability | coverage matrix queries | project |
| Approvals | list/create/decide (si target es de un proyecto) | resolver project del target |
| Catalog | modules/requirements/stories/versions por project/module | chain → ProjectId |
| QualityGates | get/assign ligados a project | project (gates globales: solo Admin o documentar) |
| Integrations | settings / DB validation por project | project |
| Notifications | channels con `ProjectId` | project (null = global → solo Admin) |
| SignalR | `TestRunHub.SubscribeToRun` | **además** de existencia: `EnsureCanAccessTestRunAsync` |
| AuditLog | si filtra por project | project; si es global → Admin/Auditor only (ya policies) |

### Explícitamente fuera de Sprint 11

- Sandbox runners, vault conn strings, SSRF
- UI asignación de miembros / pickers Approvals / AiAnalysis panel
- Cambiar semántica de `Policies.ViewReports` a algo distinto de “authenticated” (la ACL real vive en handlers)

### Controllers (punto de entrada; enforcement en MediatR)

`ProjectsController`, `TestRunsController`, `TestCasesController`, `DefectsController`, `ApprovalsController`, `TraceabilityController`, `DashboardController`, `QualityGatesController`, `CatalogController`, `IntegrationsController` (+ hub).

`AdministrationController` / `AuthController` / `VersionController`: sin cambio ACL proyecto (salvo listar users-for-member si se añade API mínima).

---

## 5) Plan de tests IDOR (userA vs userB)

### Fixtures

| Actor | Rol global | Membership |
|-------|------------|------------|
| `admin@…` | Administrador | bypass |
| `userA` | QA | solo `ProjectA` |
| `userB` | QA | solo `ProjectB` |
| `orphan` | QA | ningún proyecto |

Datos: `ProjectA` con `TestRunA`, `EvidenceA`; `ProjectB` con `TestRunB`, `EvidenceB`.

### Casos mínimos (integration o API tests)

| # | Acción | Actor | Esperado |
|---|--------|-------|----------|
| 1 | `GET projects` | userA | solo ProjectA |
| 2 | `GET projects/{ProjectB}` | userA | 404 |
| 3 | `GET testruns?projectId=B` | userA | 404 o lista vacía + deny (preferir 404 en Ensure) |
| 4 | `GET testruns/{RunB}` | userA | 404 |
| 5 | `GET evidence` RunB + EvidenceB | userA | 404 |
| 6 | `GET evidence` RunA + EvidenceB (cruzado) | userA | 404 |
| 7 | `GET evidence` RunA + path inventado | userA | 404 |
| 8 | `GET evidence` RunA + EvidenceA | userA | 200 |
| 9 | `SubscribeToRun(RunB)` SignalR | userA | HubException / deny |
| 10 | `GET projects` | orphan | vacía |
| 11 | `GET projects/{A}` | admin | 200 |
| 12 | Start run en ProjectB | userA | 404/403 |

Unit: `IProjectAccessService` + `DownloadEvidenceQueryHandler` con fakes.

---

## 6) Confirmación split 11-A … 11-C (+ VERIFY)

| Prompt | Scope | DoD parcial |
|--------|--------|-------------|
| **11-A** | Entidad `ProjectMember`, EF, repo, seed, tests add/list | Tabla + seed admin; **sin** filtrar handlers aún |
| **11-B** | `IProjectAccessService` + enforcement en handlers/listas/hub + tests IDOR lectura | userA no lee ProjectB |
| **11-C** | Download por `Evidence.Id` (+ ACL run) + ajuste frontend download | path/id ajeno → 404 |
| **11-VERIFY** | Suite IDOR completa, TM-01 → Mitigated (o residual doc), roadmap checkboxes | Sprint 11 cerrado |

**Ajuste vs prompts originales:** válido. No hace falta 11-D separado; VERIFY absorbe Threat-Model + suite.

**Orden estricto:** A → B → C → VERIFY. No abrir 12.

**API membresía mínima en 11-A o 11-B:**  
`GET/POST /projects/{id}/members` (ManageProjects o Admin) — necesaria para tests y ops; UI fuera de scope.

---

## Decisiones cerradas por ORCH

1. Membership = **visibilidad**; policies globales = **capacidad**.
2. Admin global = bypass; resto requiere `ProjectMember`.
3. Denegación = **404** anti-enum.
4. Evidence download = **Evidence.Id** (preferido).
5. Enforcement en **handlers + hub + report generators**, vía `IProjectAccessService`.
6. Split **11-A / 11-B / 11-C / VERIFY** confirmado.

## Riesgos residuales tras Sprint 11 (aceptar o documentar)

- Jobs Hangfire sin usuario: deben confiar en `testRunId` ya autorizado al encolar.
- Quality gates “globales” sin ProjectId: política Admin-only.
- Sin UI de membership: riesgo ops (olvidar asignar users) — mitigar con seed + endpoint API.
- `ViewReports` sigue siendo “authenticated”; la seguridad real es el servicio ACL.

---

*Fin 11-ORCH. Siguiente sesión: Prompt 11-A (implementación).*
