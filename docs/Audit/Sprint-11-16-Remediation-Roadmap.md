# Roadmap de remediación — Sprints 11–16

Origen: Auditoría Final Sprint 10 (NO-GO enterprise).  
Premisa: un ítem grande o un cluster cohesivo por sprint; Fase A se parte por riesgo y dependencia.

**Cierre (2026-07-17):** re-auditoría independiente ejecutada → [Sprint-16-Reaudit.md](./Sprint-16-Reaudit.md). Score **7.2/10** (antes 4.5). Enterprise: NO-GO condicional (bloqueadores B1–B5); interno single-tenant: GO.

**Prompts listos para IA (1 sesión = 1 prompt):** [AI-Prompts-Sprints-11-16.md](./AI-Prompts-Sprints-11-16.md)

| Sprint | Nombre | Duración | Ítems | Objetivo de salida |
|--------|--------|----------|-------|--------------------|
| **11** | Multi-tenant ACL & Evidence | 1–1.5 sem | 2, 4 | Cerrar IDOR de lectura y descarga de evidencias |
| **12** | Secrets & SSRF | 1–1.5 sem | 3, 5 | Sin conn strings del cliente; webhooks endurecidos |
| **13** | Runner Sandbox | 1.5–2 sem | 1 | Ejecución aislada (bloqueador RCE) |
| **14** | Producto honesto | 1–1.5 sem | 6, 7, 8, 9 | Claims = realidad en SPA + README |
| **15** | Escala & realtime | 1–1.5 sem | 10, 11 | Listados livianos + SignalR multi-réplica |
| **16** | Ops & CI release | 1–1.5 sem | 12, 13, 14 | Boot limpio, images seguras, CI listo para go-live |

**Go Live Enterprise:** solo tras **11 + 12 + 13** cerrados.  
**Go Live interno reforzado:** tras 11–14.  
**Barra ~9.5:** tras 11–16 + re-auditoría.

---

## Sprint 11 — Multi-tenant ACL & Evidence

**Ítems:** 2, 4  
**Por qué juntos:** ambos son autorización de datos; no dependen del sandbox.

### Scope
1. **ProjectMember + filtrado obligatorio** en repositorios/handlers (proyectos, runs, casos, evidencias, reportes).
2. **Evidence download** solo si `Evidence.Id` pertenece al `TestRun` autorizado y al proyecto del caller.

### DoD
- [x] **11-A:** entidad `ProjectMember`, tabla aditiva, seed admin, repo + tests add/list
- [x] **11-B:** `IProjectAccessService` + filtrado handlers (IDOR lectura); denegación = 404 anti-enum; Admin bypass
- [x] **11-C:** Evidence download por Id + ownership (path legacy solo si ∈ Evidence del run)
- [x] Tests de IDOR userA vs userB — `Sprint11IdorTests` + `ProjectAccessServiceTests` (projects, runs, cases, evidence)
- [x] Download con path/id ajeno → 404
- [x] Threat Model TM-01 → ✅ Mitigado (residual Hangfire documentado)
- [x] **11-VERIFY** cerrado (2026-07-16)

### Fuera de scope
Sandbox, vault DB, cambios de UI de producto.

---

## Sprint 12 — Secrets & SSRF

**Ítems:** 3, 5  
**Depende de:** preferible después de 11 (menos superficie que endurecer).

### Scope
1. **Eliminar conn strings del cliente** → entornos nombrados / vault (config server-side).
2. **SSRF harden:** block private IP, link-local, cloud metadata en webhooks e integraciones HTTP.

### DoD
- [x] **12-A:** API no acepta connection string del browser (`StartDatabaseValidationRequest` → 400); entornos nombrados `ProjectDatabaseEnvironment`; Hangfire args = runId+projectId+env names
- [x] **12-B:** `ISsrfGuard` / `SsrfGuard` + tests (`SsrfGuardTests`: metadata, RFC1918, loopback, ULA, HTTPS, DNS)
- [x] **12-C:** cablear SsrfGuard (dispatcher, validators, tester, SonarQube, HttpClient notifications) + `SsrfWiringTests`
- [x] Hangfire sin secretos de cliente en plaintext de jobs (validación BD)
- [x] Tests SSRF (metadata + RFC1918) verdes — `SsrfGuardTests` + `SsrfWiringTests`
- [x] **12-VERIFY** cerrado (2026-07-17): unit 280 + integration 11 OK

### Fuera de scope
Sandbox de runners.

### Residuales aceptados (post-VERIFY)
- Upsert de entorno BD aún recibe conn string (admin → cifrado en reposo); no viaja en Hangfire.
- Allowlist HTTP/privado solo en Development (`localhost` / `127.0.0.1`).
- DNS rebinding: validación al configurar/enviar, sin pinning de IP en el socket.
- Webhooks legacy ya guardados: bloqueados al despachar; no se auto-limpian.
- Bloqueador enterprise restante: **sandbox de runners (Sprint 13)**.

---

## Sprint 13 — Runner Sandbox

**Ítems:** 1  
**Depende de:** idealmente 12 (menos secretos en el host del runner).

### Scope
1. Contenedor/job **efímero** por ejecución
2. **Sin secretos** de plataforma montados en el sandbox
3. **Egress deny** (o allowlist mínima)
4. API orquesta; no ejecuta scripts en el proceso del API

### DoD
- [x] **13-A:** seams `IScriptExecutionEnvironment` / `ISandboxedProcessExecutor` + flag `Runners:UseSandbox` + fallback local + ADR-011
- [x] **13-B:** `DockerSandboxedProcessExecutor` + imagen `docker/runner-newman` + Newman; red `none` default; prueba negativa secretos
- [x] **13-C:** Playwright / Newman / JMeter / ZAP / SeleniumIde / Visual / codegen → `ISandboxedProcessExecutor`; fuera de Dev se fuerza Docker
- [x] Prueba negativa: script no lee secretos del API (`DockerSandboxE2ETests` + asserts de args)
- [x] Timeout + cleanup huérfanos (`--rm` + `docker rm -f` por prefijo runId)
- [x] Docs Security (checklist RCE) + residual documentado
- [x] **13-VERIFY** cerrado (2026-07-17)

### Residuales aceptados (post-VERIFY)
- `docker.sock` en compose (orquestación privilegiada)
- `SandboxNetworkMode=bridge` sin allowlist de destinos
- ZAP usa CLI docker en host (aislamiento = contenedor ZAP oficial)
- Imagen API aún lleva Node/Playwright (legado; cleanup Ops/16)
- Fallback local solo Development + warning

### Riesgo
Sprint más largo; si se atrasa, **no** declarar enterprise Go.

---

## Sprint 14 — Producto honesto

**Ítems:** 6, 7, 8, 9  
**Puede paralelizarse** con 12–13 en equipo separado (UI/docs).

### Scope
1. UI vincular **UserStory** en Casos + pickers en Aprobaciones
2. API lectura **AiAnalysis** + panel en Ejecuciones
3. Defects: **create/transition en UI** **o** bajar el claim de “gestión de defectos”
4. README: quitar Azure DevOps / Jira / Xray hasta existir

### DoD
- [x] Matriz de cobertura usable sin curl manual
- [x] Diagnóstico IA visible cuando exista análisis
- [x] Defects: create/transition en UI (Opción A)
- [x] Marketing alineado con capacidades reales
- [x] **VERIFY 14**: checklist ítems 6–9 + smoke código/UI (login SPA OK; API requiere Seed user-secrets)

---

## Sprint 15 — Escala & realtime

**Ítems:** 10, 11  
**Depende de:** no bloquea seguridad; después de 11 ayuda (menos carga en queries ACL).

### Scope
1. Indexes `TestCaseId` / `UserStoryId`; list runs **sin** `Include(Results)` masivo
2. SignalR **Redis backplane**; token por negotiate/header (no query string)

### DoD
- [x] Listado de runs con payload acotado + índice verificado
- [x] Progress correcto con ≥2 réplicas API (backplane Redis cuando `ConnectionStrings:Redis` está set)
- [x] JWT no aparece en URLs de hub (SPA: `accessTokenFactory`; Prod: LongPolling; API rechaza query token fuera de Dev)

**15-A antes/después (listado GET /testruns):**
- Antes: `PagedWithDetailsAsync` → `Include(Results)` → N filas TestResult hidratadas por página.
- Después: `PagedSummaryAsync` → proyección SQL `COUNT` → **0** entidades TestResult; detail con Includes.

**15-B:** SignalR Redis backplane + auth por header. Redis obligatorio multi-réplica (documentado en Manual Técnico / Arquitectura).
- [x] **VERIFY 15**: ítems 10–11 — `PagedSummaryAsync` sin Include(Results); índices UserStoryId/TestCaseId; SignalR Redis + header/LongPolling (sin `?access_token=` en URL SPA)

---

## Sprint 16 — Ops & CI release

**Ítems:** 12, 13, 14  
**Cierre** hacia re-auditoría / RC-2.

### Scope
1. Migraciones EF **únicas**; migrate **fuera** del boot de la API
2. Images **non-root**; no publicar 1433/6379; SQL least-privilege
3. CI: E2E smoke + coverage floors reales + deploy/rollback

### DoD
- [x] Arranque API sin `EnsureCreated`/migrate destructivo en prod
- [x] Compose/prod sin puertos DB/Redis públicos
- [x] Pipeline: test → e2e smoke → deploy con rollback documentado

---

## Orden y paralelismo

```text
Sprint 11 (ACL + Evidence)
    └─► Sprint 12 (Secrets + SSRF)
            └─► Sprint 13 (Sandbox)     ──┐
Sprint 14 (Producto)  [// paralelo UI]    ├──► Sprint 15 (Escala)
                                          └──► Sprint 16 (Ops/CI)
                                                     └─► Re-auditoría → Go/No-Go
```

| Paralelo recomendado | Equipo |
|----------------------|--------|
| 14 ∥ 12 o 13 | Frontend/docs vs Backend/security |
| 15 ∥ inicio 16 | Perf vs DevOps si hay capacidad |

---

## Checklist ejecutivo

| # | Ítem | Sprint |
|---|------|--------|
| 1 | Sandbox runners | **13** |
| 2 | ProjectMember ACL | **11** |
| 3 | Vault / entornos DB | **12** |
| 4 | Evidence por Id del run | **11** |
| 5 | SSRF harden | **12** |
| 6 | UI UserStory + Approvals pickers | **14** |
| 7 | AiAnalysis API + panel | **14** |
| 8 | Defects UI o bajar claim | **14** |
| 9 | README honestidad | **14** |
| 10 | Indexes + list runs | **15** |
| 11 | SignalR Redis + token header | **15** |
| 12 | Migraciones EF / migrate off-boot | **16** |
| 13 | Non-root / puertos / SQL privileges | **16** |
| 14 | CI E2E + coverage + rollback | **16** |
