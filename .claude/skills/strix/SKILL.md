---
name: strix
description: Pruebas de penetración automatizadas con Strix (agentes de IA que encuentran y validan vulnerabilidades con PoC). Usar cuando el usuario pida un pentest, auditoría de seguridad dinámica o escaneo de vulnerabilidades sobre una app, repo, API o dominio que le pertenezca o tenga autorización para probar.
---

# Strix: pentesting con agentes de IA

## Requisitos previos
- CLI instalado: `strix --version` (si falta: `uv tool install strix-agent`).
- Docker corriendo: `docker info`. Strix ejecuta sus herramientas en un sandbox de contenedor.
- Variables de entorno:
  - `STRIX_LLM`: modelo en formato LiteLLM, por ejemplo `anthropic/claude-sonnet-5-5`.
  - `LLM_API_KEY`: clave del proveedor del modelo.
  - Opcional: `LLM_API_BASE` (endpoint propio), `PERPLEXITY_API_KEY` (búsqueda web).

Si falta alguno, detenerse y decir cuál; no pedir la clave en el chat.

## Autorización
Antes de escanear, confirmar con el usuario que el objetivo es suyo o que tiene autorización escrita. No escanear terceros, IPs públicas ajenas ni producción sin confirmación explícita.

## Uso
```bash
# Código local (white-box), modo rápido sin TUI
strix -n -m quick --target ./

# Solo los cambios contra main (útil en PRs)
strix -n --scope-mode diff --diff-base origin/main --target ./

# Código + despliegue de staging, con foco y límite de costo
strix -n --target ./ --target https://staging.ejemplo.com \
  --instruction "Foco en autenticación, IDOR y XSS" --max-budget 5

# Especificación de API
strix -n --target ./openapi.yaml --target https://api.ejemplo.com
```

- Modos: `quick` (CI), `standard` (rutina), `deep` (por defecto, exhaustivo y más costoso).
- Recomendar siempre `--max-budget` para acotar el gasto de LLM.
- `--fail-on high` hace que el proceso salga con código 2 solo ante hallazgos altos o críticos.
- Reanudar: `strix --resume <run_name>`.

## Resultados
Los reportes quedan en `./strix_runs/<run_name>/`. Resumir para el usuario: severidad, componente afectado, evidencia/PoC y remediación sugerida. No agregar `strix_runs/` al control de versiones.
