# ADR-012: Aislar el daemon Docker del proceso API (socket-proxy)

**Estado**: Aceptado · **Fecha**: 2026-07-17 · **Sprint**: 17-A · **Cierra**: B1 (re-auditoría Sprint 16)

## Contexto

El servicio `api` montaba `/var/run/docker.sock` directamente (`docker-compose.yml`, y
`group_add` con `DOCKER_GID` para que el usuario non-root pudiera usarlo). La re-auditoría
Sprint 16 lo marcó como **Critical residual (B1)**: un RCE en el proceso API — expuesto a la
red — equivale a **control total del daemon Docker del host** (crear un contenedor que monte
`/` del host y escribir como root ⇒ escape). El sandbox de Sprint 13 aísla el *contenido* del
script (usuario no-root, `--read-only`, `--cap-drop ALL`, `--network none`), pero no el
**plano de control**: quien controla el proceso API controla el socket.

## Decisión

Interponer **`tecnativa/docker-socket-proxy`** entre el API y el daemon:

- El proxy es el **único** servicio que monta `/var/run/docker.sock` (montado **solo lectura**).
- Vive en una red compose **interna** dedicada (`docker-control`, `internal: true`); **no publica
  puertos al host**. Solo el API está también en esa red.
- Expone una **allowlist mínima** de endpoints, lo justo para `docker run --rm` de los sandboxes:
  `CONTAINERS=1`, `IMAGES=1`, `POST=1`, `PING/VERSION=1`. Todo lo demás **denegado**
  (`INFO`, `EXEC`, `SWARM`, `SECRETS`, `CONFIGS`, `VOLUMES`, `NETWORKS`, `SERVICES`, `TASKS`,
  `NODES`, `PLUGINS`, `SYSTEM`, `AUTH`, `BUILD`, `COMMIT`, `DISTRIBUTION`, `SESSION` = 0).
- El API deja de montar el socket y de usar `group_add`/`DOCKER_GID`. Se conecta con
  `DOCKER_HOST=tcp://docker-socket-proxy:2375`.

### Por qué no requiere cambio de código

El CLI `docker` respeta `DOCKER_HOST` de forma nativa. `DockerSandboxedProcessExecutor` invoca
el binario `docker` vía `ProcessExecutor`, que **añade** claves a `ProcessStartInfo.Environment`
sin resetearlo (`ProcessExecutor.cs:36-38`). Con `UseShellExecute=false`, ese entorno se
inicializa con el del proceso API, de modo que el CLI hijo **hereda `DOCKER_HOST`** en las dos
rutas: la normal y la de ZAP (`PreferHostDockerCli`). El filtrado de allowlist de entorno
(`LocalSandboxedProcessExecutor.FilterEnvironment`) controla los `-e` que ve el **contenedor**,
no el entorno del **proceso CLI**, así que no interfiere con `DOCKER_HOST`.

> Invariante a preservar: si en el futuro se endurece `ProcessExecutor` reseteando el entorno del
> proceso hijo (para no filtrar secretos del API al CLI), hay que **inyectar `DOCKER_HOST`
> explícitamente** o pasar `-H tcp://...` en los argumentos, o los runners perderán el daemon.

## Alternativas consideradas

| Opción | Veredicto |
|--------|-----------|
| **A · Runner-agent separado** (worker con el socket; API invoca por cola/HTTP autenticado) | Objetivo final. Saca por completo la orquestación del proceso expuesto. Se habilita como seam en **17-B** (`IContainerOrchestrator`). |
| **B · Daemon remoto TCP+TLS** (`DOCKER_HOST` a un host de runners con mTLS) | Válida; encaja en el mismo seam de 17-B. Requiere host/VM + gestión de certificados. Evolución. |
| **C · gVisor / Kata** (runtime reforzado del contenedor runner) | Complementaria: endurece el escape del contenedor, **no** resuelve B1 (el API seguiría con el socket). Backlog de defensa en profundidad. |
| **D · socket-proxy con allowlist** | **Elegida ahora**: mejor relación impacto/coste, reversible, sin reescribir runners. |

## Consecuencias

**Positivas**: el proceso API ya no puede hablar directamente con el daemon; los verbos
peligrosos (`info`, `exec`, `swarm`, gestión de volúmenes/redes) quedan bloqueados por el proxy;
un RCE en el API ya no da control del daemon de forma trivial.

**Residual (no es aislamiento total)**: la allowlist mantiene `POST /containers/create`, que
**todavía permite crear un contenedor que monte rutas del host** (incluida `/`) y así escalar.
Por eso B1 queda **mitigado, no cerrado**: el cierre definitivo es sacar la orquestación del
proceso API (opción A/B vía 17-B). Documentado también en `docs/Security/Security-Checklist.md`.

## Verificación

- El servicio `api` no monta `docker.sock` ni usa `group_add` (grep en `docker-compose.yml`).
- `DOCKER_HOST` apunta al proxy; un run **normal** (Newman) y uno **ZAP** funcionan vía el proxy.
- Test negativo: desde el contenedor API, `docker info` / `docker exec` fallan (403 del proxy) y
  no existe `/var/run/docker.sock` local.
