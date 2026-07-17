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
  - `--network none` por defecto (egress deny); `bridge` documentado si se prueban APIs externas
  - `--user 1000:1000`, `--read-only`, `--cap-drop ALL`, `--rm`, nombre `qaguardian-run-{runId}-…`
  - cleanup de huérfanos por filtro de nombre

**13-C:** todos los runners de usuario + codegen usan `ISandboxedProcessExecutor`.

| Flag / entorno | Implementación |
|----------------|----------------|
| `UseSandbox=true` | `DockerSandboxedProcessExecutor` |
| `UseSandbox=false` + Development | `LocalSandboxedProcessExecutor` + warning |
| `UseSandbox=false` + no-Development | **fuerza** Docker (no host) |

ZAP: `PreferHostDockerCli=true` (contenedor oficial ZAP; sin anidar).

Imágenes: `SandboxImage` (Newman), `SandboxImagePlaywright`, `SandboxImageJMeter`.

### Red / allowlist

| Modo | Uso |
|------|-----|
| `none` (default) | Máximo aislamiento; scripts sin salida a red |
| `bridge` | APIs externas bajo prueba. Sin allowlist de destinos (residual). |

Compose monta `docker.sock` (residual: privilegio de orquestación).

**Sprint 16-B**: el proceso API corre como usuario non-root (`qaguardian`, UID 1000,
igual al UID de los contenedores sandbox). Esto exige que el UID/GID tenga acceso al
`docker.sock` montado desde el host; compose agrega el contenedor al GID configurable
`DOCKER_GID` (grupo `docker` del host) vía `group_add`. El privilegio de orquestación
sobre `docker.sock` en sí sigue siendo residual — non-root reduce, pero no elimina,
el impacto de un RCE en el proceso API.

## Consecuencias

**Positivas**: scripts de usuario no corren en el proceso API fuera de Development.

**Residuales**: `docker.sock`; bridge sin allowlist; imagen API con Node legado.