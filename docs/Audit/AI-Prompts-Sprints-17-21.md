# Prompts IA — Ruta a ≥9.5 · Sprints 17–21

Origen: re-auditoría independiente 2026-07-17 → [Sprint-16-Reaudit.md](./Sprint-16-Reaudit.md) (score **7.2/10**).
Objetivo: cerrar los bloqueadores **B1–B10** y habilitar un **GO enterprise** (regla del roadmap: 11+12+13 realmente cerrados).

Uso: copiar **un prompt por sesión de IA**. No mezclar sprints. Cerrar el DoD (y su VERIFY) antes de abrir el siguiente. Cada prompt trae **anclas verificadas** (`archivo:línea`) de la re-auditoría; si el código ya cambió, re-verificar antes de asumir.

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

## Mapa bloqueador → sprint

| Bloqueador | Severidad | Sprint | Ítem de la ruta |
|-----------|-----------|--------|-----------------|
| **B1** docker.sock en el API → RCE = host | Critical residual | **17** | 1 · sacar orquestación Docker del proceso API |
| **B2** pivot SQL vía Upsert sin allowlist de hosts | Alta | **18** | 2 · allowlist SQL + SSRF por hop |
| **B3** SSRF por redirects 3xx + DNS rebinding | Alta | **18** | 2 |
| **B7** EncryptionKey vacía; secretos en launchSettings | Media | **18** | 2 (familia secretos) |
| **B4** ZAP host-CLI sin hardening + targetUrl interpolado | Alta | **19** | 3 · ZAP sandboxed |
| **B8** bridge/host sin allowlist egress; sin cpus/pids | Media | **19** | 3 (límites de sandbox) |
| **B5** Hangfire DDL vs usuario least-privilege | Alta operativa | **20** | 4 · esquema Hangfire off-runtime |
| **B6** audit log / quality gates sin ACL de proyecto | Media | **21** | 5 · ACL + calidad |
| **B9** coverage floors simbólicos + E2E critical-path superficial | Media | **21** | 5 |
| **B10** dashboard Include(Results); frontend sin HEALTHCHECK; azure-pipelines obsoleto | Baja | **21** | 5 (residuales) |

## Orden de ejecución

```text
17-A → 17-B → 17-VERIFY          (aislamiento Docker — habilita GO enterprise)
18-A → 18-B → 18-C → 18-VERIFY   (SSRF/SQL hardening + secretos)
19-A → 19-B → 19-VERIFY          (ZAP sandboxed + límites de recursos)
20-A → 20-VERIFY                 (esquema Hangfire fuera de runtime)
21-A → 21-B → 21-C → 21-VERIFY   (ACL restante + calidad + residuales)
→ Re-auditoría final (comité) → Go/No-Go enterprise
```

Paralelismo seguro: **21 (calidad/ACL)** puede correr en rama aparte mientras se ejecutan 18–20. **17 es prerequisito duro del GO enterprise** y no debe paralelizarse con cambios de runner (18/19 tocan la misma superficie).

## Anclas de código (re-auditoría 2026-07-17)

| Hecho verificado | Ubicación |
|------------------|-----------|
| `docker.sock` montado en el contenedor API | `docker-compose.yml:113-115` |
| Sandbox invoca `docker` CLI en el host desde el API | `src/QAGuardian.Infrastructure/Runners/DockerSandboxedProcessExecutor.cs:139-156` |
| Wiring sandbox (fuerza Docker fuera de Dev) | `src/QAGuardian.Infrastructure/DependencyInjection.cs:102-125` |
| Imagen API con Node/Playwright/Newman + docker CLI (legado) | `src/QAGuardian.API/Dockerfile:19-31` |
| Upsert entorno BD con conn string arbitraria (rol ManageProjects) | `src/QAGuardian.API/Controllers/IntegrationsController.cs:72-77`; `src/QAGuardian.Application/Features/DatabaseValidation/DatabaseValidationCommands.cs:254-259` |
| Validator abre `SqlConnection` sin validar host | `src/QAGuardian.Infrastructure/Validation/SqlServerSchemaValidator.cs:51-54` |
| Handler SSRF solo valida URI inicial (redirects no revalidan) | `src/QAGuardian.Infrastructure/Security/SsrfOutboundHandler.cs:15-22` |
| `IntegrationConnectionTester` usa `CreateClient()` sin handler SSRF | `src/QAGuardian.Infrastructure/Clients/IntegrationConnectionTester.cs:71-82` |
| `Security:EncryptionKey` vacía aceptada (`??` no rechaza `""`) | `src/QAGuardian.Infrastructure/Identity/IdentityServices.cs:98-102` |
| Boot valida `Jwt:SigningKey`, no `EncryptionKey` | `src/QAGuardian.API/Program.cs:407-424` |
| Secretos dev versionados | `src/QAGuardian.API/Properties/launchSettings.json:11-15` |
| ZAP `PreferHostDockerCli` + `targetUrl` interpolado en args | `src/QAGuardian.Infrastructure/Runners/TestRunners.cs:421-437` |
| Flags sandbox sin `--cpus`/`--pids-limit`; `NormalizeNetwork` acepta `host` | `DockerSandboxedProcessExecutor.cs:139-153, 208-214` |
| Hangfire `PrepareSchemaIfNecessary = true` con conn string del API | `src/QAGuardian.Infrastructure/DependencyInjection.cs:174-178` |
| `db-init` least-privilege + app user | `docker-compose.yml:55-74`; `database/00-app-user.sql:33-34` |
| Audit log sin filtro de proyecto | `GetAuditLogQuery.cs:21-27`; `AuditLogController.cs:12-21` |
| Quality gates globales sin membresía | `QualityGateCommands.cs:67-77` + update/delete |
| `RoleInProject` no enforced | `src/QAGuardian.Infrastructure/Identity/ProjectAccessService.cs:31-44` |
| Coverage floors bajos | `UnitTests.csproj:7` (44), `IntegrationTests.csproj:9` (11), `frontend/vite.config.ts:48-53` (2.5% líneas) |
| E2E critical-path superficial | `frontend/e2e/critical-path.spec.ts` |
| Frontend sin HEALTHCHECK | `frontend/Dockerfile` |
| Dashboard aún hidrata Results | `DashboardQueries.cs:123` (`ListWithResultsAsync` + `Include(Results)`) |
| `azure-pipelines.yml` desfasado del `ci.yml` | `azure-pipelines.yml:14-57` |

---

# Sprint 17 — Aislar la orquestación Docker (B1)

**Objetivo:** que un RCE en el proceso API **no** implique control del daemon Docker del host. Eliminar el bind de `/var/run/docker.sock` en el contenedor API.
**Por qué primero:** es el único **Critical residual**; su cierre es condición del GO enterprise.
**Fuera:** cambios funcionales de runners (flags, ZAP → 19), SSRF (→ 18).

### Prompt 17-ORCH (opcional — planificador)

```text
[Contrato global]

Actúa como tech lead de plataforma. Solo planifica, NO implementes.

Sprint 17: quitar docker.sock del proceso API sin perder el sandbox de runners.

Hechos verificados (re-auditoría):
- docker-compose.yml:113-115 monta /var/run/docker.sock en el servicio api.
- DockerSandboxedProcessExecutor.cs:139-156 ejecuta el binario `docker` (CLI) en el host,
  lo que exige el socket.
- DependencyInjection.cs:102-125 fuerza Docker fuera de Development.
- API/Dockerfile:19-31 aún instala Node/Playwright/Newman + docker CLI (legado).

Explora y compara opciones (con trade-offs, coste y reversibilidad):
  A) Runner-agent separado: un microservicio/worker con acceso al socket, que el API
     invoca por cola/HTTP autenticado (mTLS o token). El API pierde el socket.
  B) Docker daemon remoto autenticado (DOCKER_HOST tcp+TLS) hacia un host/VM de runners.
  C) Runtime de aislamiento reforzado (gVisor/runsc o Kata) para los contenedores runner,
     manteniendo el socket pero reduciendo el impacto de escape.
  D) Rootless Docker / socket proxy (tecnativa/docker-socket-proxy) con allowlist de
     endpoints (solo containers create/start/logs/rm), como mitigación intermedia.

Entrega:
1) Recomendación priorizada para este repo (single-host compose hoy, k8s futuro).
2) Diseño de la interfaz de orquestación (IContainerOrchestrator o similar) que aísle
   el "cómo se lanza el contenedor" del runner.
3) Plan de migración incremental sin romper Development (fallback local ya existe).
4) Impacto en docker-compose.yml, DependencyInjection.cs y Dockerfile.
5) Riesgos y cómo se probará el aislamiento (test negativo: el API no puede hablar con el daemon).
6) Confirma split 17-A / 17-B.

No toques código en este prompt.
```

### Prompt 17-A — Seam de orquestación + socket-proxy con allowlist

```text
[Contrato global]

Sprint 17 · Prompt 17-A · B1 (aislamiento — paso 1: mínimo viable)

Objetivo: que el proceso API NO tenga acceso directo al daemon Docker, manteniendo el
sandbox de runners funcionando en compose.

Hechos verificados:
- docker-compose.yml:113-115 → bind de /var/run/docker.sock en `api`; group_add DOCKER_GID (90-91).
- DockerSandboxedProcessExecutor.cs:71-77 (ruta normal) y :111-117 (ruta ZAP PreferHostDockerCli)
  invocan el binario `docker` vía ProcessExecutor. RunnerSandboxOptions.DockerCli = "docker".
- ProcessExecutor.cs:36-38 SOLO añade claves a psi.Environment; NUNCA lo resetea. Con
  UseShellExecute=false, psi.Environment se inicializa con el entorno del proceso API, así que
  el CLI `docker` hijo HEREDA DOCKER_HOST del API en AMBAS rutas (normal y ZAP), sin código extra.
- OJO: el filtrado de allowlist (LocalSandboxedProcessExecutor.FilterEnvironment) controla los
  `-e KEY=VALUE` del CONTENEDOR, no el entorno del proceso CLI. No afecta a DOCKER_HOST.
- El comentario en DockerSandboxedProcessExecutor.cs:76 ("NUNCA heredar env del proceso API")
  se refiere a los `-e` del contenedor, NO al proceso CLI (que sí hereda). No lo tomes como
  invariante de seguridad del proceso CLI.
- DependencyInjection.cs:102-126 fuerza Docker fuera de Development.

Implementa la mitigación intermedia (opción D del ORCH), reversible y de bajo riesgo:
1. Introduce un servicio `docker-socket-proxy` (imagen tipo tecnativa/docker-socket-proxy)
   en docker-compose.yml, en una red interna dedicada, con allowlist SOLO de:
   CONTAINERS=1, POST=1, IMAGES=1 (lo mínimo para create/start/logs/wait/rm/pull),
   y todo lo demás en 0 (INFO/EXEC/SWARM/SECRETS/VOLUMES/NETWORKS=0…).
2. Quita el bind directo de /var/run/docker.sock del servicio `api`, y también el
   group_add DOCKER_GID (90-91): con transporte TCP al proxy ya no se necesita el GID del socket.
3. Apunta el API al proxy con `DOCKER_HOST: tcp://docker-socket-proxy:2375` en el `environment`
   del servicio `api`. NO se requiere cambio en DockerSandboxedProcessExecutor ni en ProcessExecutor:
   el CLI `docker` lee DOCKER_HOST del entorno heredado (verificado arriba) en la ruta normal y en
   la de ZAP. Confirma que ninguna otra capa filtre/borre DOCKER_HOST del proceso API.
   (Alternativa equivalente: pasar `-H tcp://...` en args, pero DOCKER_HOST es menos invasivo.)
4. docker-compose.override.yml (Dev): mantener el comportamiento actual documentado
   (Dev usa fallback local; la ruta de contenedor no se ejercita).
5. Documenta en docs/Architecture/adr/ un ADR nuevo (ADR-012) y actualiza
   docs/Security/Security-Checklist.md y el residual de ADR-011. Deja constancia de que el proxy
   NO es aislamiento total: POST /containers/create sigue permitiendo montar rutas del host, por
   lo que el cierre definitivo es el agente/daemon remoto (17-B / Sprint futuro).

DoD parcial:
- El servicio `api` NO monta docker.sock (grep en docker-compose.yml sin la línea del bind).
- `docker compose config` válido; el API arranca y puede lanzar un run en QA/compose.
- Un run NORMAL (Newman) y un run ZAP (PreferHostDockerCli) funcionan vía el proxy — verifica
  explícitamente ZAP, porque es la ruta que pasa entorno filtrado (aunque DOCKER_HOST se herede).
- Test/negativo documentado: desde el contenedor API, comandos peligrosos del daemon
  (p.ej. `docker info`, `docker exec`, o montar el FS del host en un contenedor nuevo) quedan
  bloqueados por el proxy; el socket local ya no existe en el contenedor.

Fuera: agente separado / daemon remoto (17-B), gVisor.
```

### Prompt 17-B — Abstracción `IContainerOrchestrator` (camino a agente/daemon remoto)

```text
[Contrato global]

Sprint 17 · Prompt 17-B · B1 (aislamiento — paso 2: arquitectura)

Con el socket-proxy ya en su lugar (17-A), extrae el "cómo se lanza el contenedor" a una
abstracción para permitir a futuro un runner-agent separado o un daemon remoto sin tocar
la lógica de runners.

Hechos verificados:
- DockerSandboxedProcessExecutor.cs concentra armado de args + ejecución del CLI.
- DependencyInjection.cs:102-125 hace el wiring por entorno.

Implementa:
1. Interfaz `IContainerOrchestrator` en Application/Abstractions con operaciones mínimas:
   RunEphemeralAsync(spec) → resultado (exit code, stdout/stderr, artifacts path), y
   un `spec` que capture imagen, args, límites, red, mounts, env allowlist, timeout, runId.
2. `LocalDockerOrchestrator` (implementación actual vía CLI/DOCKER_HOST) detrás de la interfaz.
3. `DockerSandboxedProcessExecutor` pasa a construir el `spec` y delegar en el orquestador,
   sin conocer el transporte (socket/proxy/tcp).
4. Wiring por configuración: `Runners:Orchestrator` = local|remote (default local),
   dejando el enganche para un `RemoteAgentOrchestrator` futuro (solo interfaz + stub + ADR,
   NO implementar el agente completo aquí).
5. Actualiza ADR-012 con el diseño final.

DoD:
- Runners siguen ejecutando (Newman/Playwright) vía la nueva abstracción en compose.
- dotnet test verde; tests de seams (args/spec) actualizados.
- Ningún runner referencia el socket directamente.

Fuera: implementar el agente remoto real (backlog); gVisor.
```

### Prompt 17-VERIFY

```text
[Contrato global]

Sprint 17 · VERIFY (B1)

Checklist:
- [ ] El servicio `api` NO monta /var/run/docker.sock ni conserva group_add DOCKER_GID.
- [ ] El API habla con Docker solo vía proxy con allowlist (DOCKER_HOST=tcp://docker-socket-proxy:2375).
- [ ] Run NORMAL (Newman) funciona en QA/compose vía el proxy.
- [ ] Run ZAP (PreferHostDockerCli) funciona vía el proxy (ruta de entorno filtrado; DOCKER_HOST heredado).
- [ ] Test negativo: `docker info`/`exec` y montar el FS del host quedan bloqueados por el proxy;
      no existe socket local en el contenedor API.
- [ ] ADR-012 + Security-Checklist + residual ADR-011 actualizados (proxy = mitigación, no aislamiento total).
- [ ] docs/Audit/Sprint-16-Reaudit.md: B1 marcado como mitigado (con residual del proxy explícito).

Corre dotnet test. Resume residual. No declares GO enterprise aquí; eso lo hace la re-auditoría final.
```

---

# Sprint 18 — SSRF/SQL hardening + secretos (B2, B3, B7)

**Objetivo:** cerrar el pivot SQL vía Upsert y los bypass clásicos de SSRF (redirects, rebinding), y endurecer el manejo de la clave de cifrado y secretos de dev.
**Fuera:** ZAP (→ 19), Hangfire schema (→ 20).

### Prompt 18-A — Allowlist de hosts SQL en validación de BD (B2)

```text
[Contrato global]

Sprint 18 · Prompt 18-A · B2

Hoy (verificado):
- IntegrationsController.cs:72-77 → UpsertDatabaseEnvironment con [Authorize(ManageProjects)]
  (rol ManageProjects incluye QA), acepta ConnectionString arbitraria.
- DatabaseValidationCommands.cs:254-259 → Handle cifra y persiste la conn string.
- SqlServerSchemaValidator.cs:51-54 → abre new SqlConnection(connectionString) sin validar host.
Efecto: un QA puede apuntar Server=169.254.169.254 / 10.x / SQL internos y el job Hangfire hace el pivot.

Implementa una allowlist de destinos SQL server-side:
1. Config `DatabaseValidation:AllowedSqlHosts` (lista de hosts/patrones permitidos por entorno;
   vacío en Development = permitir localhost documentado; vacío en prod = denegar todo).
2. Al hacer Upsert y ANTES de abrir la conexión en SqlServerSchemaValidator, extrae el host
   del connection string (usa SqlConnectionStringBuilder.DataSource) y valida:
   - Resuelve DNS y aplica el mismo criterio de SsrfGuard (bloquea RFC1918/loopback/link-local/
     metadata/ULA) salvo que el host esté en la allowlist explícita.
   - Rechaza con error claro (400 en Upsert; fallo controlado en el job) si no cumple.
3. Reutiliza el guard existente (ISsrfGuard / IHostAddressResolver) para no duplicar lógica;
   añade un método específico p.ej. ValidateSqlHostAsync(host).
4. Loguea el host rechazado SIN exponer credenciales.

DoD parcial:
- Upsert/validación con Server apuntando a 169.254.169.254 o 10.x (no allowlisted) → rechazado.
- Host allowlisted → sigue funcionando.
- Tests unit del validador de host SQL (metadata, RFC1918, allowlist hit/miss).

Fuera: redirects/rebinding HTTP (18-B).
```

### Prompt 18-B — SSRF por hop: no seguir redirects + pin de IP (B3)

```text
[Contrato global]

Sprint 18 · Prompt 18-B · B3

Hoy (verificado):
- SsrfOutboundHandler.cs:15-22 valida solo request.RequestUri inicial; el HttpClient interno
  sigue redirects 3xx sin revalidar (no hay AllowAutoRedirect=false en ningún lado).
- IntegrationConnectionTester.cs:71-82 usa _httpClientFactory.CreateClient() SIN el handler SSRF.
- IHostAddressResolver resuelve DNS una vez; la conexión real re-resuelve → DNS rebinding (TOCTOU).

Implementa:
1. En todos los HttpClient con SsrfOutboundHandler, configura AllowAutoRedirect=false
   (en el primary handler / SocketsHttpHandler del cliente en DependencyInjection.cs).
2. Manejo de redirects controlado: si una respuesta es 3xx, revalida la Location con
   ISsrfGuard antes de seguirla; limita el nº de hops (p.ej. 3) y revalida en CADA hop.
   Encapsula esto (p.ej. en el propio SsrfOutboundHandler o un RedirectFollowingHandler).
3. Cablea IntegrationConnectionTester para usar un named client con el handler SSRF
   (no CreateClient() anónimo).
4. Mitiga rebinding: pin de IP post-resolución — resuelve el host una vez, valida la IP y
   conecta a esa IP fijada (ConnectCallback de SocketsHttpHandler que use la IP validada y
   preserve el SNI/Host header), evitando una segunda resolución divergente.

DoD parcial:
- Test: 302 hacia http://169.254.169.254/ NO se sigue (bloqueado en el hop).
- Test: IntegrationConnectionTester rechaza destino privado igual que el dispatcher.
- Test: rebinding simulado (DNS que cambia entre validate y connect) no alcanza IP privada.

Fuera: EncryptionKey/secretos (18-C).
```

### Prompt 18-C — Fail-fast de EncryptionKey + secretos fuera del repo (B7)

```text
[Contrato global]

Sprint 18 · Prompt 18-C · B7

Hoy (verificado):
- IdentityServices.cs:98-102 → AesTokenEncryptionService acepta "" (el `??` no rechaza vacío);
  SHA256("") produce una clave determinista.
- Program.cs:407-424 valida Jwt:SigningKey contra null pero NO Security:EncryptionKey ni longitud.
- launchSettings.json:11-15 versiona Jwt__SigningKey, Security__EncryptionKey, Seed__AdminPassword.
- NotificationChannelDto.Target se devuelve completo por GET (URLs con secretos, botToken|chatId);
  NotificationDispatcher.cs:64-65 loguea channel.Target en fallos.

Implementa:
1. Fail-fast: en el boot (Program.cs) valida Security:EncryptionKey igual que Jwt:SigningKey —
   no vacío y longitud mínima (p.ej. ≥32 chars de material). Fuera de Development, aborta el
   arranque si no cumple. Aplica el mismo check en el ctor del servicio de cifrado.
2. Saca los secretos de launchSettings.json versionado: usa dotnet user-secrets para Development
   (documenta en Manual-Instalacion) y deja placeholders/None en el archivo, o muévelo a un
   launchSettings.Local.json ya ignorado. NO borres el flujo de arranque de Dev; solo los valores.
3. Redacción de secretos en salida:
   - GET de canales de notificación: no devolver Target en claro (enmascarar o omitir; devolver
     solo un booleano "configured" o los últimos caracteres). Ajusta el DTO y el frontend que lo consuma.
   - Logs del dispatcher: no registrar channel.Target; usa un identificador no sensible.

DoD:
- Boot en no-Development con EncryptionKey vacía/corta → falla con mensaje claro.
- git grep de secretos: launchSettings sin valores reales.
- GET notification channels no expone secretos; logs sin Target.
- Tests: fail-fast del cifrado; DTO enmascarado.

Fuera: nada más de este sprint.
```

### Prompt 18-VERIFY

```text
[Contrato global]

Sprint 18 · VERIFY (B2, B3, B7)

Checklist:
- [ ] Upsert/validación SQL a host privado/metadata no allowlisted → rechazado.
- [ ] Redirects 3xx no alcanzan destinos privados (revalidación por hop).
- [ ] IntegrationConnectionTester pasa por SsrfGuard.
- [ ] Rebinding TOCTOU mitigado (pin de IP).
- [ ] Boot falla sin EncryptionKey válida fuera de Development.
- [ ] Sin secretos reales en launchSettings.json; GET/logs sin Target en claro.
- [ ] docs/Security actualizado; Sprint-16-Reaudit.md B2/B3/B7 mitigados.

Corre dotnet test (unit + integration). Resume residual.
```

---

# Sprint 19 — ZAP sandboxed + límites de recursos (B4, B8)

**Objetivo:** que ZAP corra bajo el mismo `ISandboxedProcessExecutor` que el resto de runners, con `targetUrl` validado; añadir límites de recursos y cerrar el modo de red `host`.
**Fuera:** aislamiento del socket (ya en 17), SSRF de otros callers (ya en 18).

### Prompt 19-A — ZAP vía ISandboxedProcessExecutor + targetUrl validado (B4)

```text
[Contrato global]

Sprint 19 · Prompt 19-A · B4

Hoy (verificado):
- TestRunners.cs:421-437 → el runner ZAP usa PreferHostDockerCli: true y ejecuta `docker run`
  en el host SIN --user / --cap-drop / --read-only / --network none / --memory, y con targetUrl
  interpolado en los args (riesgo de inyección de flags si la URL es controlada).
- El resto de runners ya pasan por DockerSandboxedProcessExecutor con flags de hardening.

Implementa:
1. Enruta el runner ZAP por ISandboxedProcessExecutor / el orquestador (Sprint 17), con los
   mismos flags: --rm --user 1000:1000 --read-only --cap-drop ALL --security-opt no-new-privileges
   --memory (límite), red controlada (ver 19-B), workspace efímero.
2. Valida y normaliza targetUrl ANTES de construir args:
   - Pasa por ISsrfGuard (bloquea privado/metadata) salvo allowlist explícita del proyecto.
   - Escapa/parametriza: nunca concatenar la URL cruda en la línea de comandos; pásala como
     argumento posicional separado (argv), no interpolada en un string de shell.
3. Elimina PreferHostDockerCli para ZAP (o justifícalo y aíslalo igual que el resto).

DoD parcial:
- ZAP se lanza con flags de hardening equivalentes a los otros runners (assert en test de seams).
- targetUrl a 169.254.169.254 / privado → rechazado antes de lanzar el contenedor.
- targetUrl con intento de inyección de flags no altera el comando.

Fuera: límites cpus/pids y red (19-B).
```

### Prompt 19-B — Límites de recursos + cerrar red `host` (B8)

```text
[Contrato global]

Sprint 19 · Prompt 19-B · B8

Hoy (verificado):
- DockerSandboxedProcessExecutor.cs:139-153 aplica --memory pero NO --cpus ni --pids-limit.
- NormalizeNetwork (DockerSandboxedProcessExecutor.cs:208-214) acepta "host" como modo de red.
- bridge no tiene allowlist de egress.

Implementa:
1. Añade límites configurables por defecto: --cpus (p.ej. "1.0") y --pids-limit (p.ej. 256),
   junto al --memory existente. Config en las options del sandbox (Runners:*).
2. Prohíbe "host" en NormalizeNetwork: valores permitidos = none|bridge (default none);
   cualquier otro → error de configuración. Documenta que host queda vetado.
3. Egress: cuando el modo sea bridge (necesario para algunos runners), documenta y, si es viable
   sin sobre-ingeniería, ofrece un gancho de allowlist (p.ej. red docker dedicada con reglas)
   como recomendación operativa en docs/Security. No implementar un firewall completo aquí;
   sí dejar el default en none y bridge como opt-in consciente.
4. Cuota de workspace: documenta el riesgo de disco (workspace no se borra post-run) y añade,
   si es de bajo riesgo, limpieza del workspace del run tras copiar artifacts a evidencia.

DoD:
- Contenedores runner arrancan con --cpus/--pids-limit/--memory (assert en test de seams).
- NormalizeNetwork("host") → rechazado; default none.
- Docs Security actualizadas con el modelo de red y egress.

Fuera: nada más.
```

### Prompt 19-VERIFY

```text
[Contrato global]

Sprint 19 · VERIFY (B4, B8)

Checklist:
- [ ] ZAP corre bajo el executor sandbox con flags de hardening.
- [ ] targetUrl validado por SsrfGuard y pasado como argv (sin inyección).
- [ ] --cpus/--pids-limit/--memory presentes; "host" prohibido.
- [ ] Tests de seams actualizados (asserts de flags).
- [ ] Sprint-16-Reaudit.md B4/B8 mitigados.

Corre dotnet test. Un run ZAP real en compose (si hay Docker) como smoke. Resume residual.
```

---

# Sprint 20 — Esquema Hangfire fuera de runtime (B5)

**Objetivo:** que el primer arranque de producción con usuario SQL de mínimo privilegio no dependa de que Hangfire haga DDL en runtime.
**Fuera:** el resto de la cadena de despliegue (ya cubierta en Sprint 16).

### Prompt 20-A — PrepareSchemaIfNecessary=false + esquema Hangfire en db-init

```text
[Contrato global]

Sprint 20 · Prompt 20-A · B5

Hoy (verificado):
- DependencyInjection.cs:174-178 → Hangfire configura SqlServerStorage con
  PrepareSchemaIfNecessary = true, usando la MISMA connection string del API.
- docker-compose.yml:55-74 → servicio db-init crea el app user least-privilege
  (database/00-app-user.sql:33-34: db_datareader + db_datawriter, sin DDL).
Efecto: en el primer boot prod, el API (usuario sin DDL) intentará crear el esquema Hangfire y fallará.

Implementa:
1. Cambia PrepareSchemaIfNecessary a false en producción (configurable por entorno; en
   Development puede seguir true para no fricción local).
2. Genera el esquema Hangfire fuera del runtime del API:
   - Añade un script SQL de esquema Hangfire (extraído/estándar de Hangfire.SqlServer) a database/
     (p.ej. database/03-hangfire-schema.sql) ejecutado por el servicio db-init / paso de despliegue
     con el usuario privilegiado (sa/DBA), ANTES de arrancar el API.
   - Alternativa aceptable: un paso en el servicio `migrate` que prepare el esquema Hangfire una vez.
3. Otorga al app user least-privilege los permisos mínimos sobre el esquema/tablas Hangfire
   (lectura/escritura + ejecución de los SP que Hangfire usa), documentado en 00-app-user.sql
   o un script hermano.
4. Actualiza docs/Manual-Instalacion.md y docs/Release/DEPLOY-ROLLBACK.md con el nuevo paso.
5. Revisa appsettings.json:3 (default User Id=sa) para que Production use el app user y no sa.

DoD:
- Con PrepareSchemaIfNecessary=false y el esquema pre-creado, el API arranca y los jobs corren
  bajo el usuario least-privilege (probado en compose: db-init/migrate → api).
- Sin el paso de esquema, el fallo es explícito y documentado (no un crash silencioso en readiness).
- docs actualizadas.

Fuera: nada más.
```

### Prompt 20-VERIFY

```text
[Contrato global]

Sprint 20 · VERIFY (B5)

Checklist:
- [ ] PrepareSchemaIfNecessary=false en prod.
- [ ] Esquema Hangfire creado por db-init/migrate con usuario privilegiado.
- [ ] App user least-privilege ejecuta jobs sin necesitar DDL.
- [ ] Primer arranque limpio en compose (secuencia db-init → migrate → api → jobs OK).
- [ ] Manual-Instalacion + DEPLOY-ROLLBACK actualizados; Sprint-16-Reaudit.md B5 mitigado.

Corre el flujo en compose (o documenta el resultado si no hay Docker local). Resume residual.
```

---

# Sprint 21 — ACL restante + calidad + residuales (B6, B9, B10)

**Objetivo:** cerrar las fugas cross-tenant restantes (audit log, quality gates), subir los pisos de cobertura a valores reales, completar el E2E del camino crítico y limpiar residuales operativos.
**Paralelizable** con 18–20 (rama aparte).

### Prompt 21-A — ACL de proyecto en audit log y quality gates (B6)

```text
[Contrato global]

Sprint 21 · Prompt 21-A · B6

Hoy (verificado):
- GetAuditLogQuery.cs:21-27 + AuditLogController.cs:12-21 → cualquier rol ManageProjects
  (QA/TechLead/Admin) lee rutas/emails/IPs de TODOS los proyectos (info disclosure cross-tenant).
- QualityGateCommands.cs:67-77 (+ Update/Delete/GetQualityGateDetailQuery) → CRUD de quality gates
  global, sin verificar membresía de proyecto.
- ProjectAccessService.cs:31-44 → RoleInProject no se enforce (Member ≡ ProjectAdmin).

Implementa (reutilizando IProjectAccessService del Sprint 11):
1. Audit log: filtra por proyectos accesibles del caller (ListAccessibleProjectIdsAsync) salvo
   Admin global. Si un registro de auditoría no tiene proyecto asociado, decide y documenta la
   política (solo Admin global lo ve).
2. Quality gates: si son entidades por proyecto, aplica EnsureCanAccessProjectAsync en list/get/
   create/update/delete. Si son plantillas globales por diseño, restringe la mutación a Admin
   global (o a ProjectAdmin del proyecto dueño) y documenta la decisión.
3. RoleInProject: aplica RBAC intra-proyecto donde tenga sentido (p.ej. update/delete de proyecto
   y de sus quality gates requieren ProjectAdmin, no cualquier Member). Cambio mínimo y documentado;
   no rehagas el modelo de permisos.

DoD:
- Usuario con ManageProjects que NO es miembro de B no ve el audit log de B ni muta sus quality gates.
- Tests IDOR extendidos: audit log y quality gates userA vs userB.
- Documenta la política de RoleInProject aplicada.

Fuera: cobertura/E2E (21-B), residuales ops (21-C).
```

### Prompt 21-B — Pisos de cobertura reales + E2E critical-path completo (B9)

```text
[Contrato global]

Sprint 21 · Prompt 21-B · B9

Hoy (verificado):
- Coverage floors bajos: UnitTests.csproj:7 (44% líneas), IntegrationTests.csproj:9 (11%),
  frontend/vite.config.ts:48-53 (líneas/statements 2.5%). Son simbólicos: la CI pasa en verde
  con muy poca red de seguridad.
- frontend/e2e/critical-path.spec.ts solo verifica que un diálogo sea visible; no completa el flujo.

Implementa por pasos honestos (no inflar con tests triviales):
1. Añade tests significativos en las áreas más críticas y menos cubiertas (prioriza seguridad:
   ProjectAccessService, SsrfGuard/wiring, database validation, runners sandbox seams) hasta poder
   SUBIR los floors sin trampas.
2. Sube los umbrales a las metas de la ruta: backend líneas ≥60% (ajusta unit/integration según
   el reparto real), frontend líneas ≥30%. Mide primero el baseline real y sube en consecuencia;
   si 60/30 no es alcanzable con esfuerzo razonable en un sprint, sube al máximo honesto y deja
   documentado el plan para llegar a la meta (no bajes de lo actual).
3. Completa frontend/e2e/critical-path.spec.ts para cubrir un flujo real de punta a punta:
   login → crear proyecto → crear caso → vincular UserStory → (mock o backend de prueba) →
   ver resultado/panel. Mantén el smoke y visual existentes.
4. Asegura que la CI (.github/workflows/ci.yml) ejecute el critical-path (o al menos smoke+critical)
   en el job e2e.

DoD:
- Floors: backend ≥60% (o máximo honesto documentado), frontend ≥30%; CI falla si bajan.
- critical-path.spec.ts cubre el flujo completo y pasa en CI.
- Reporta baseline antes/después de cobertura.

Fuera: residuales ops (21-C).
```

### Prompt 21-C — Residuales operativos: dashboard, HEALTHCHECK, Azure Pipelines (B10)

```text
[Contrato global]

Sprint 21 · Prompt 21-C · B10

Hoy (verificado):
- DashboardQueries.cs:123 → ListWithResultsAsync con Include(Results): hidrata Results en el hot path
  del dashboard (puede reintroducir la carga que Sprint 15 quitó del listado de runs).
- frontend/Dockerfile → termina en EXPOSE 8080 sin HEALTHCHECK (orquestación ciega ante nginx caído).
- azure-pipelines.yml:14-57 → build/test sin coverage, sin e2e, sin push/deploy: desfasado del ci.yml,
  puede dar falsa sensación de parity si alguien lo usa en ADO.

Implementa:
1. Dashboard: sustituye el Include(Results) por una proyección/conteo equivalente (patrón
   PagedSummaryAsync de Sprint 15) para no materializar entidades TestResult; conserva el detalle
   donde de verdad se necesite. Reporta antes/después.
2. frontend/Dockerfile: añade HEALTHCHECK (curl/wget a http://localhost:8080/ o a un endpoint de
   salud del nginx) coherente con nginx-unprivileged; refleja el healthcheck también en
   docker-compose.yml para el servicio frontend.
3. azure-pipelines.yml: o bien alinéalo con el ci.yml (coverage + e2e + gates) o márcalo/retíralo
   claramente como "no es el pipeline de release oficial; ver .github/workflows/ci.yml" para evitar
   falsa parity. Elige lo de menor riesgo y documenta.

DoD:
- Dashboard no hidrata Results (proyección); tests verdes.
- Frontend con HEALTHCHECK en imagen y compose.
- azure-pipelines.yml alineado o explícitamente marcado como no-oficial.
- Sprint-16-Reaudit.md B10 cerrado/mitigado.

Fuera: nada más.
```

### Prompt 21-VERIFY

> **Estado 2026-07-19:** ejecutado como VERIFY-only → **No-Go parcial**. B6/B9/B10 **no implementados** (ver [Sprint-21-VERIFY.md](./Sprint-21-VERIFY.md)). Checklist abajo refleja el DoD objetivo; ítems de código siguen `[ ]` salvo docs.

```text
[Contrato global]

Sprint 21 · VERIFY (B6, B9, B10)

Checklist:
- [ ] Audit log y quality gates con ACL de proyecto; tests IDOR extendidos verdes.
- [ ] RoleInProject aplicado donde corresponde y documentado.
- [ ] Floors reales (backend ≥60% o máximo honesto, frontend ≥30%); CI falla si bajan.
- [ ] critical-path E2E completo en CI.
- [ ] Dashboard sin Include(Results); frontend con HEALTHCHECK; azure-pipelines alineado/marcado.
- [x] docs/Audit y roadmap actualizados.  (VERIFY 2026-07-19: estado honesto documentado)

Corre dotnet test + frontend test:coverage. Resume residual.
```

---

# Prompt de re-auditoría final (post 17–21)

```text
[Contrato global]

Actúa como el mismo comité (Microsoft, Google, Amazon, Netflix, OpenAI, OWASP, ISTQB).
No asumas calidad. Re-audita desde cero el estado de los bloqueadores B1–B10 y de los
tres Critical originales, verificando en código y con ejecución de tests.

Entrega:
- Hallazgos con archivo:línea, scores por dimensión, riesgos residuales.
- Delta vs 7.2/10 (re-auditoría Sprint 16) y vs 4.5/10 (Sprint 10).
- Go/No-Go enterprise: exige B1–B5 cerrados + 11/12/13 realmente completos.
- Actualiza docs/Audit/ y el canvas sprint-16-reaudit (o crea sprint-21-reaudit).
```

---

# Cómo usar en Cursor

| Sesión | Pegar |
|--------|--------|
| 1 | Contrato + **17-A** |
| 2 | Contrato + **17-B** |
| 3 | Contrato + **17-VERIFY** |
| 4… | igual por sprint (18-A, 18-B, …) |
| Paralelo | rama `sprint-21/...` (calidad/ACL) mientras otra sesión hace 18–20 |

**Regla de oro:** si un prompt no cierra su DoD parcial, no abras el siguiente; usa un prompt
"FIX residual Sprint N-X: …" citando el fallo exacto. **No declares GO enterprise** sin la
re-auditoría final con B1–B5 cerrados.
