#!/usr/bin/env bash
# SessionStart hook: make `dotnet build` / `dotnet test` work in Claude Code on the web.
# Local machines are expected to have the SDK from global.json installed already, so this only runs remotely.
# It never fails the session: if the SDK cannot be installed, it prints why and exits 0.
#
# Two sources are tried, because a session's network policy decides which one is reachable:
#   1. dotnet-install.sh from builds.dotnet.microsoft.com, which gives exactly the channel global.json asks for;
#   2. the distribution's own dotnet-sdk-<channel> package, which some policies allow when (1) is blocked.
set -uo pipefail

if [[ "${CLAUDE_CODE_REMOTE:-}" != "true" ]]; then
  exit 0
fi

PROJECT_DIR="${CLAUDE_PROJECT_DIR:-$(pwd)}"
INSTALL_DIR="${DOTNET_INSTALL_DIR:-$HOME/.dotnet}"
CHANNEL="$(sed -n 's/.*"version": *"\([0-9]*\.[0-9]*\)\..*/\1/p' "$PROJECT_DIR/global.json" | head -n 1)"
CHANNEL="${CHANNEL:-10.0}"
# Direct URL (dot.net/v1/dotnet-install.sh redirects here); no redirect following, HTTPS only.
INSTALL_SCRIPT_URL="https://builds.dotnet.microsoft.com/dotnet/scripts/v1/dotnet-install.sh"

# Where the SDK ended up, filled in by whichever install succeeded. Empty means "already on PATH".
DOTNET_BIN_DIR=""
DOTNET_HOME=""
DOTNET_TOOLS_DIR=""

# True when the dotnet on PATH satisfies global.json (dotnet --version fails otherwise).
sdk_satisfies_global_json() {
  (cd "$PROJECT_DIR" && dotnet --version > /dev/null 2>&1)
  return $?
}

sdk_version() {
  (cd "$PROJECT_DIR" && dotnet --version 2> /dev/null)
  return $?
}

use_sdk() {
  DOTNET_BIN_DIR="$1"
  DOTNET_HOME="$2"
  DOTNET_TOOLS_DIR="$3"
  export DOTNET_ROOT="$DOTNET_HOME"
  export PATH="$DOTNET_BIN_DIR:$DOTNET_TOOLS_DIR:$PATH"
  hash -r
  return 0
}

persist_env() {
  if [[ -z "${CLAUDE_ENV_FILE:-}" || -z "$DOTNET_BIN_DIR" ]]; then
    return 0
  fi

  {
    echo "export DOTNET_ROOT=\"$DOTNET_HOME\""
    echo "export PATH=\"$DOTNET_BIN_DIR:$DOTNET_TOOLS_DIR:\$PATH\""
    echo "export DOTNET_CLI_TELEMETRY_OPTOUT=1"
    echo "export DOTNET_NOLOGO=1"
    echo "export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1"
  } >> "$CLAUDE_ENV_FILE"
  return 0
}

install_from_microsoft() {
  local script
  script="$(mktemp)"

  echo "[session-start] Trying $INSTALL_SCRIPT_URL"
  if curl --proto '=https' --tlsv1.2 -fsS --max-time 60 "$INSTALL_SCRIPT_URL" -o "$script" \
    && bash "$script" --channel "$CHANNEL" --install-dir "$INSTALL_DIR" --no-path > /dev/null; then
    rm -f "$script"
    use_sdk "$INSTALL_DIR" "$INSTALL_DIR" "$INSTALL_DIR/tools"
    return 0
  fi

  rm -f "$script"
  return 1
}

# The distribution ships the SDK in its own archive, which a policy that blocks Microsoft's CDN may still
# allow. The package tracks the channel, so `dotnet-sdk-10.0` for a global.json asking for 10.0.x.
install_from_packages() {
  local package="dotnet-sdk-$CHANNEL"
  local apt_get=(env DEBIAN_FRONTEND=noninteractive apt-get)
  local host="" root=""
  local candidate

  if ! command -v apt-get > /dev/null 2>&1; then
    return 1
  fi

  if [[ "$(id -u)" != "0" ]]; then
    if ! command -v sudo > /dev/null 2>&1; then
      echo "[session-start] Cannot install $package: not root and sudo is not available."
      return 1
    fi
    apt_get=(sudo -n "${apt_get[@]}")
  fi

  echo "[session-start] Falling back to $package from the distribution archive."
  if ! "${apt_get[@]}" install -y "$package" > /dev/null 2>&1; then
    # A stale package list is the usual reason, and updating it is slow enough to be worth avoiding first.
    "${apt_get[@]}" update > /dev/null 2>&1 || return 1
    "${apt_get[@]}" install -y "$package" > /dev/null 2>&1 || return 1
  fi

  # Where the package puts its host, looked for by path rather than through PATH: whatever failed global.json
  # above may still be first there, and the point is to reach the SDK that was just installed.
  for candidate in /usr/bin/dotnet /usr/lib/dotnet/dotnet "$(command -v dotnet 2> /dev/null)"; do
    if [[ -x "$candidate" ]]; then
      host="$candidate"
      break
    fi
  done

  if [[ -z "$host" ]]; then
    return 1
  fi

  # That host is a symlink into the real root, which is what DOTNET_ROOT wants.
  root="$(dirname "$(readlink -f "$host")")"
  use_sdk "$(dirname "$host")" "$root" "$HOME/.dotnet/tools"
  return 0
}

# Prefer a previous install of this hook over whatever the image ships, then validate against global.json.
if [[ -x "$INSTALL_DIR/dotnet" ]]; then
  export DOTNET_ROOT="$INSTALL_DIR"
  export PATH="$INSTALL_DIR:$INSTALL_DIR/tools:$PATH"
fi

if command -v dotnet > /dev/null 2>&1 && sdk_satisfies_global_json; then
  echo "[session-start] dotnet SDK $(sdk_version) satisfies global.json"
  case "$(command -v dotnet)" in
    "$INSTALL_DIR"/*) use_sdk "$INSTALL_DIR" "$INSTALL_DIR" "$INSTALL_DIR/tools" && persist_env ;;
    *) ;;
  esac
else
  echo "[session-start] No SDK satisfying global.json found. Installing channel $CHANNEL."
  if install_from_microsoft || install_from_packages; then
    if sdk_satisfies_global_json; then
      persist_env
      echo "[session-start] Installed dotnet SDK $(sdk_version) in $DOTNET_HOME"
    else
      echo "[session-start] WARNING: installed a .NET SDK, but none of them satisfies global.json (channel $CHANNEL)."
      echo "[session-start] Builds and tests will not run in this session. Say so instead of assuming they pass."
      exit 0
    fi
  else
    echo "[session-start] WARNING: could not install the .NET SDK from $INSTALL_SCRIPT_URL or from the distribution archive (network policy?)."
    echo "[session-start] Builds and tests will not run in this session. Say so instead of assuming they pass."
    exit 0
  fi
fi

cd "$PROJECT_DIR" || exit 0
if dotnet restore > /dev/null 2>&1; then
  echo "[session-start] NuGet packages restored."
else
  echo "[session-start] WARNING: dotnet restore failed. Check network access to api.nuget.org."
fi

exit 0
