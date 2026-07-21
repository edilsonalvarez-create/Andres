# Sprint 21 · VERIFY (B6, B9, B10) — estado honesto

**Fecha:** 2026-07-19  
**Alcance:** verificación en código + ejecución de tests. **No se implementó** 21-A/B/C en esta sesión.  
**Veredicto:** **No-Go parcial** — B6 / B9 / B10 **abiertos** (implementación ausente o trivialmente incompleta).  
**GO enterprise:** **NO** (regla B1–B5 + 11/12/13; residual B1 proxy + gaps de Sprint 21).

## Checklist VERIFY

| Ítem | Estado | Evidencia |
|------|--------|-----------|
| Audit log y quality gates con ACL de proyecto; tests IDOR extendidos verdes | [ ] | **NO IMPLEMENTADO** (parcial mínimo en assign) |
| `RoleInProject` aplicado donde corresponde y documentado | [ ] | **NO IMPLEMENTADO** (enum + persistencia solamente) |
| Floors reales (backend ≥60 % o máximo honesto, frontend ≥30 %); CI falla si bajan | [ ] | Umbrales: Unit **44**, Integration **11**, frontend lines **2.5** — lejos de ≥60/≥30 |
| critical-path E2E completo en CI | [ ] | CI solo corre `smoke.spec.ts`; critical-path se corta en diálogo |
| Dashboard sin `Include(Results)`; frontend HEALTHCHECK; azure-pipelines alineado/marcado | [ ] | Los tres gaps siguen |
| docs/Audit y roadmap actualizados | [x] | Esta nota + addenda en Sprint-16/17-Reaudit |

## B6 — ACL audit / quality gates / RoleInProject

### Parcial (único cierre relacionado)
- `AssignGateToProjectCommandHandler` llama `EnsureCanAccessProjectAsync` — `QualityGateCommands.cs:101`.

### Abierto
- `GetAuditLogQueryHandler` pagina **todo** el audit sin filtro de proyecto ni `IProjectAccessService` — `GetAuditLogQuery.cs:18-27`. Cualquier caller con permiso de lectura de audit ve emails/rutas/IPs cross-tenant.
- CRUD global de gates **sin** membresía:
  - Create — `QualityGateCommands.cs:36-62`
  - List — `QualityGateCommands.cs:67-77`
  - Update — `UpdateQualityGateCommand.cs:26-55`
  - Delete — `DeleteQualityGateCommand.cs:11-23`
- `IProjectAccessService` solo comprueba membresía activa / admin bypass; **no** lee `RoleInProject` — `ProjectAccessService.cs:31-45`, `IProjectAccessService.cs:8-24`.
- `RoleInProject` existe en dominio (`Enums.cs:206-212`, `ProjectMember.cs:14-32`) y se asigna al crear proyecto (`ProjectCommands.cs:83`), pero **Member ≡ ProjectAdmin** a efectos de autorización (sin enforcement).
- Suite IDOR (`Sprint11IdorTests.cs`) no cubre audit log ni CRUD de quality gates; solo usa el enum al seedear miembros.

## B9 — Coverage floors + E2E critical-path

| Superficie | Umbral configurado | Meta sprint (≥60 % / ≥30 %) | Gap |
|------------|-------------------:|----------------------------:|-----|
| `tests/QAGuardian.UnitTests/QAGuardian.UnitTests.csproj:7` | **44** (line) | ≥60 | −16 pts |
| `tests/QAGuardian.IntegrationTests/QAGuardian.IntegrationTests.csproj:9` | **11** (line) | ≥60 (o máximo honesto documentado) | simbólico |
| `frontend/vite.config.ts:48-52` | lines/statements **2.5**, functions 20, branches 45 | lines ≥30 | −27.5 pts |

- CI (`.github/workflows/ci.yml:54-104`): job `e2e` ejecuta **solo** `npx playwright test e2e/smoke.spec.ts`. Comentario explícito: critical-path excluido.
- `frontend/e2e/critical-path.spec.ts:32-33`: tras “Nuevo script” solo `expect(dialog).toBeVisible()` — **no** completa creación de caso ni flujo punta a punta.

## B10 — Dashboard / HEALTHCHECK / Azure Pipelines

- Dashboard hot path sigue hidratando Results: `DashboardQueries.cs:121-123` → `ListWithResultsAsync` → `Repositories.cs:92-97` (`Include(r => r.Results)`).
- `frontend/Dockerfile:12-15`: termina en `EXPOSE 8080` **sin** `HEALTHCHECK`.
- `azure-pipelines.yml:14-57`: build/test sin coverage gates, sin e2e Playwright, sin deploy/GHCR — **desfasado** vs `.github/workflows/ci.yml`; **no** marcado como “no oficial”.

## Resultados de ejecución (2026-07-19)

| Suite | Resultado | Coverage medido | Umbral configurado | Meta Sprint 21 |
|-------|-----------|----------------:|-------------------:|----------------|
| Unit (`dotnet test` Debug) | **373** passed, 0 fail | — | — | — |
| Integration | **11** passed, 0 fail | — | — | — |
| Unit + CollectCoverage (Release) | 373 passed | **48.36%** line | **44** | ≥60 |
| Integration + CollectCoverage | 11 passed | **11.91%** line | **11** | ≥60 / máximo honesto |
| Frontend `npm run test:coverage` | **30** passed (5 files) | **3.07%** lines | **2.5** lines | ≥30 |

CI falla si baja del umbral *configurado* (sí); los umbrales **no** cumplen la meta ≥60/≥30 del DoD.

## Residual risks (incluye fuera de B6/B9/B10)

- **B1 residual:** proxy permite `POST /containers/create` con bind monts del host (cierre pleno → 17-B).
- **B5:** mitigado en Sprint 20-A (`PrepareSchemaIfNecessary` default false) — no re-auditado en profundidad aquí.
- **B6:** cross-tenant audit + QG CRUD global + `RoleInProject` decorativo.
- **B9:** floors simbólicos; E2E critical-path no es gate de CI.
- **B10:** N+1/`Include(Results)` en dashboard; orquestación ciega del frontend; falsa parity Azure vs GitHub Actions.
- Residuales conocidos de 18/19: allowlist SQL operativa, clients SSRF fuera del factory, sin allowlist scan-URL ZAP, egress `bridge` ops-only.

---

**Seguimiento:** re-auditoría del comité (B1–B10 + Criticals, score **7.5/10**, NO-GO) → [Sprint-21-Reaudit.md](./Sprint-21-Reaudit.md).

---

*VERIFY Sprint 21 — sin implementación de 21-A/B/C. Baseline: [Sprint-16-Reaudit.md](./Sprint-16-Reaudit.md), [Sprint-17-Reaudit.md](./Sprint-17-Reaudit.md).*
