#!/usr/bin/env bash
# SessionStart hook: make `dotnet build` / `dotnet test` work in Claude Code on the web.
# Local machines are expected to have the SDK from global.json installed already, so this only runs remotely.
# It never fails the session: if the SDK cannot be installed, it prints why and exits 0.
set -uo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

PROJECT_DIR="${CLAUDE_PROJECT_DIR:-$(pwd)}"
INSTALL_DIR="${DOTNET_INSTALL_DIR:-$HOME/.dotnet}"
CHANNEL="$(sed -n 's/.*"version": *"\([0-9]*\.[0-9]*\)\..*/\1/p' "$PROJECT_DIR/global.json" | head -n 1)"
CHANNEL="${CHANNEL:-10.0}"

persist_env() {
  if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
    {
      echo "export DOTNET_ROOT=\"$INSTALL_DIR\""
      echo "export PATH=\"$INSTALL_DIR:$INSTALL_DIR/tools:\$PATH\""
      echo "export DOTNET_CLI_TELEMETRY_OPTOUT=1"
      echo "export DOTNET_NOLOGO=1"
      echo "export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1"
    } >> "$CLAUDE_ENV_FILE"
  fi
  export DOTNET_ROOT="$INSTALL_DIR"
  export PATH="$INSTALL_DIR:$INSTALL_DIR/tools:$PATH"
}

if command -v dotnet > /dev/null 2>&1 && dotnet --version > /dev/null 2>&1; then
  echo "[session-start] dotnet SDK already available: $(dotnet --version)"
elif [ -x "$INSTALL_DIR/dotnet" ]; then
  persist_env
  echo "[session-start] dotnet SDK found in $INSTALL_DIR: $(dotnet --version)"
else
  echo "[session-start] Installing .NET SDK channel $CHANNEL into $INSTALL_DIR"
  SCRIPT="$(mktemp)"
  if curl -fsSL --max-time 60 https://dot.net/v1/dotnet-install.sh -o "$SCRIPT" \
    && bash "$SCRIPT" --channel "$CHANNEL" --install-dir "$INSTALL_DIR" --no-path > /dev/null; then
    persist_env
    echo "[session-start] Installed dotnet SDK $(dotnet --version)"
  else
    echo "[session-start] WARNING: could not download the .NET SDK (network policy?)."
    echo "[session-start] Builds and tests will not run in this session. Say so instead of assuming they pass."
    rm -f "$SCRIPT"
    exit 0
  fi
  rm -f "$SCRIPT"
fi

cd "$PROJECT_DIR" || exit 0
if dotnet restore > /dev/null 2>&1; then
  echo "[session-start] NuGet packages restored."
else
  echo "[session-start] WARNING: dotnet restore failed. Check network access to api.nuget.org."
fi

exit 0
