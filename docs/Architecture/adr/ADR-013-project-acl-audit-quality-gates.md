# ADR-013: ACL de proyecto para Audit Log y Quality Gates (B6)

**Estado**: Aceptado · **Fecha**: 2026-07-21 · **Sprint**: 21-A · **Cierra**: B6  
**Rama**: `pr/b6-acl` · **Base**: `pr/b5-hangfire-bootstrap`

## Contexto

Sprint 11 introdujo `IProjectAccessService` / `ProjectMember` (TM-01) en proyectos, runs,
casos y evidencias. Quedaron **fuera** del perímetro:

- `GET /api/v1/AuditLog` — cualquier rol con política `ManageProjects` ve **todo** el historial.
- CRUD de Quality Gates (`Create` / `Update` / `Delete`) — misma política global, sin membresía.
- `GET .../QualityGates/{id}/audit-log` — sin ACL de proyecto.
- `AssignGateToProject` ya llama `EnsureCanAccessProjectAsync`, pero **no** distingue
  `RoleInProject` (`Member` vs `ProjectAdmin`).

`QualityGate` es una entidad **global** (sin `ProjectId`); los proyectos solo referencian un
gate vía `Project.QualityGateId`. El audit middleware guarda la **ruta HTTP** en
`AuditLog.EntityName` (sin `ProjectId` tipado).

## Objetivo

Cerrar **B6**: impedir lectura cruzada de auditoría y mutaciones indebidas de quality gates /
asignaciones, reutilizando el modelo ACL existente (sin inventar un segundo sistema).

## Decisión — modelo de autorización

| Superficie | Quién | Regla |
|------------|-------|--------|
| **Audit log global** | Administrador global | Ve todas las filas (filtro `pathContains` opcional). |
| **Audit log global** | No admin con `ManageProjects` | Solo filas cuyo `EntityName` (ruta) contenga el GUID de un proyecto en `ListAccessibleProjectIdsAsync`. Filas **sin** GUID de proyecto → **solo Admin**. |
| **Quality Gate — lectura** (list/detail) | `ViewReports` | Sin cambio: plantillas globales visibles para poder asignar. |
| **Quality Gate — mutación** (create/update/delete) | Política **`Administer`** | Plantillas globales: solo Administrador del sistema. |
| **Quality Gate — audit-log por gate** | Política **`Administer`** | Historial de plantilla global = Admin. |
| **Assign gate → project** | `ManageProjects` + ACL | `EnsureCanAccessProjectAsync` **y** rol de proyecto ≥ `ProjectAdmin` (o Admin global). Un `Member` no asigna gates. |

### Extensión mínima de `IProjectAccessService`

- `EnsureCanAdministerProjectAsync(projectId)` — Admin global **o** `ProjectMember` activo con
  `RoleInProject.ProjectAdmin`. Denegación → `NotFoundException` (anti-enumeración, igual que hoy).

No se introduce un motor de claims nuevo ni se cambia JWT.

## Componentes afectados

| Capa | Archivos (previstos) |
|------|----------------------|
| Application | `GetAuditLogQuery.cs`, `Create/Update/DeleteQualityGate*`, `GetQualityGateAuditLogQuery.cs`, `AssignGateToProjectCommand`, `IProjectAccessService.cs` |
| Infrastructure | `ProjectAccessService.cs` (+ tests) |
| API | `AuditLogController.cs`, `QualityGatesController.cs` (políticas Administer donde aplique) |
| Tests | IDOR audit + QG (userA vs userB); `ProjectAccessService` ProjectAdmin; ajuste `QualityGateCommandsTests` |
| Docs | Este ADR; checkbox B6 en `docs/Security/Security-Checklist.md` |

**Fuera de alcance B6:** Hangfire, EncryptionKey/JWT, SSRF, UI/frontend, masking, sandbox/ZAP,
Docker, pipelines, cache, CVE, B9.

## Criterios de aceptación

1. Usuario `ManageProjects` miembro de proyecto A y **no** de B: no ve entradas de audit cuya
   ruta contenga el GUID de B; no obtiene datos de B vía filtros.
2. El mismo usuario **no** puede Create/Update/Delete quality gates (solo Admin).
3. `Member` de A **no** puede `AssignGateToProject` sobre A; `ProjectAdmin` de A sí.
4. `GetQualityGateAuditLog` exige Admin.
5. Tests unitarios IDOR verdes (audit + assign role); Release build sin warnings; format limpio.
6. Documentación (ADR + checklist) refleja la política `RoleInProject` / plantillas globales.

## Consecuencias

- **Positivas:** cierra el gap B6 con el mismo patrón 404 anti-enum; trazabilidad alineada a B1–B5.
- **Negativas / residuales:** filtrado de audit por substring de GUID en ruta depende de que el
  middleware registre rutas con el id de proyecto; entradas legacy opacas quedan solo-Admin.
  Listado global de plantillas de gate sigue visible a `ViewReports` (consciente: necesario para UX
  de asignación; la mutación queda cerrada).
