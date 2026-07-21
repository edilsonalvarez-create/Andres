# ADR-011: Seams de sandbox para ejecución de scripts de usuario

**Estado**: Aceptado (actualizado 13-B) · **Fecha**: 2026-07-17 · **Sprint**: 13-A/B

## Contexto

Hoy `Hangfire` → `ExecuteTestRunCommand` → `TestRunnerFactory` → runners
invocaban `ProcessExecutor` **en el proceso del API**. Un script malicioso compartía
privilegios y secretos con el host API.

## Decisión

| Puerto | Rol |
|--------|-----|
| `IScriptExecutionEnvironment` | Workspace efímero por `TestRunId` |
| `ISandboxedProcessExecutor` | Ejecutar con contrato sandbox |

Feature flag `Runners:UseSandbox`:

- **false** (Development): `LocalSandboxedProcessExecutor`
- **true**: `DockerSandboxedProcessExecutor` — `docker run --rm` con:
  - imagen `Runners:SandboxImage` (default `qaguardian/runner-newman:local`)
  - montaje **solo** del workspace del TestRun → `/workspace`
  - **sin** heredar env del proceso API; solo `-e` allowlist
  - `--network none` por defecto (egress deny); `bridge` opt-in si se prueban APIs externas
  - `--memory` / `--cpus` / `--pids-limit` configurables (defaults 512m / 1.0 / 256) — Sprint 19-B
  - `--user 1000:1000`, `--read-only`, `--cap-drop ALL`, `--rm`, nombre `qaguardian-run-{runId}-…`
  - cleanup de huérfanos por filtro de nombre

**13-C:** todos los runners de usuario + codegen usan `ISandboxedProcessExecutor`.

| Flag / entorno | Implementación |
|----------------|----------------|
| `UseSandbox=true` | `DockerSandboxedProcessExecutor` |
| `UseSandbox=false` + Development | `LocalSandboxedProcessExecutor` + warning |
| `UseSandbox=false` + no-Development | **fuerza** Docker (no host) |

ZAP (Sprint 19-A): vía sandbox + imagen oficial; sin `PreferHostDockerCli`.

Imágenes: `SandboxImage` (Newman), `SandboxImagePlaywright`, `SandboxImageJMeter`, `SandboxImageZap`.

### Red / egress (Sprint 19-B)

| Modo | Uso |
|------|-----|
| `none` (default) | Máximo aislamiento; scripts sin salida a red |
| `bridge` | APIs externas / ZAP. Opt-in consciente. |
| `host` | **Vetado** (`InvalidOperationException`) |

Gancho de egress (no firewall app): `Runners:SandboxNetworkName` con mode `bridge`
→ `--network <nombre>` (red Docker dedicada con reglas ops).

**Sprint 16-B**: el proceso API corre como usuario non-root (`qaguardian`, UID 1000,
igual al UID de los contenedores sandbox).

**Sprint 17-A (supersede el residual `docker.sock`)**: el API **ya no monta**
`/var/run/docker.sock` ni usa `group_add`/`DOCKER_GID`. El acceso al daemon pasa por
`docker-socket-proxy` con allowlist en una red interna, vía `DOCKER_HOST` (ver **ADR-012**).
El CLI `docker` hereda `DOCKER_HOST` del proceso API sin cambios de código. Esto **mitiga**
B1 (ya no hay control directo del daemon), pero **no es aislamiento total**: `POST
/containers/create` sigue permitido por la allowlist, por lo que el cierre definitivo es
sacar la orquestación del proceso API (agente/daemon remoto, Sprint 17-B).

## Consecuencias

**Positivas**: scripts de usuario no corren en el proceso API fuera de Development.

**Residuales**: acceso a `docker-socket-proxy` con `create/start` (mitigado vs socket directo,
ver ADR-012); bridge sin allowlist; imagen API con Node legado.