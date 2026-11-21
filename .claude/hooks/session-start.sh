#!/bin/bash
set -euo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

export PATH="$HOME/.local/bin:$PATH"

command -v graphify >/dev/null 2>&1 || uv tool install graphifyy >/dev/null 2>&1 || pip install -q graphifyy
command -v strix >/dev/null 2>&1 || uv tool install strix-agent >/dev/null 2>&1 || pip install -q strix-agent

echo "export PATH=\"$HOME/.local/bin:\$PATH\"" >> "${CLAUDE_ENV_FILE:-/dev/null}"
