#!/usr/bin/env bash
# The local gate (/verify): what CI's gate runs, chosen from the diff, with the storage emulator up.
#
#   scripts/verify.sh            # the steps this branch's changes call for
#   scripts/verify.sh --all      # every step, whatever the diff
#
# Always: the engine restored, built with warnings as errors, format-checked and tested, and the pages' own
# tests. Then what the changed paths call for (studio/browser/plan.js --gate): the battle bundle, the learning
# project, the content builder. The browser journeys the diff calls for are named at the end, for
# /browser-check: they take too long to run unasked. Stops at the first failure. Once every step passed, the
# tree it started on is recorded (scripts/gate/state.sh), and the Stop hook stays quiet until the files change again.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT" || exit 1

ALL=false
for argument in "$@"; do
  case "$argument" in
    --all) ALL=true ;;
    *)
      echo "usage: scripts/verify.sh [--all]" >&2
      exit 2
      ;;
  esac
done

SUMMARY=()
# The tree the gate tests, taken before the first step: a file edited while it runs is not what it passed on.
TREE="$("$ROOT/scripts/gate/state.sh" tree)" || TREE=""

# While it runs, the Stop hook waits for it instead of asking for a second gate on top of this one.
RUNNING="$(git rev-parse --git-path claude-gate/running)"
mkdir -p "$(dirname "$RUNNING")" && echo "$$ $TREE" > "$RUNNING"
trap 'rm -f "$RUNNING"' EXIT

# The paths this branch changed against main, committed or not, untracked files included. With no main to read,
# every step runs.
changed_paths() {
  local base
  base="$(git merge-base HEAD origin/main 2> /dev/null)" || return 1
  # NUL-separated, so a path with a quote or a non-ASCII letter reaches plan.js as it is, not quoted.
  { git diff -z --name-only "$base"; git ls-files -z --others --exclude-standard; } | tr '\0' '\n' | sort -u
  return 0
}

run_step() {
  local name="$1"
  shift
  local started status
  started="$(date +%s)"
  echo "==> $name"
  "$@"
  status=$?
  if [[ $status -ne 0 ]]; then
    SUMMARY+=("FAIL  $name ($(($(date +%s) - started)) s)")
    report
    echo "The gate failed at: $name. Nothing was recorded." >&2
    exit "$status"
  fi
  SUMMARY+=("pass  $name ($(($(date +%s) - started)) s)")
  return 0
}

skip_step() {
  local name="$1"
  local reason="$2"
  SUMMARY+=("skip  $name: $reason")
  return 0
}

report() {
  echo
  echo "Gate summary"
  printf '  %s\n' "${SUMMARY[@]}"
  return 0
}

# As CI runs it: from learning/, so scripts/ is linted under the project's own ruff configuration, and from the
# lock as it stands, so a stale uv.lock fails here as it does there instead of being rewritten.
learning_gate() {
  (cd learning \
    && uv sync --locked --no-build --no-install-project \
    && uv run --no-sync ruff check . ../scripts \
    && uv run --no-sync ruff format --check . ../scripts \
    && uv run --no-sync pytest -q)
  return $?
}

if [[ "$ALL" == true ]] || ! PATHS="$(changed_paths)"; then
  [[ "$ALL" == true ]] || echo "No origin/main to compare with (git fetch origin main): every step runs."
  PATHS=""
  STEPS=$'battle=true\nlearning=true\ncontent=true'
  JOURNEYS="journeys=all"
else
  echo "$(printf '%s' "$PATHS" | grep -c .) path(s) changed against origin/main."
  # A plan that cannot be read is not a diff that calls for nothing.
  if ! STEPS="$(printf '%s\n' "$PATHS" | node studio/browser/plan.js --gate)" \
    || ! JOURNEYS="$(printf '%s\n' "$PATHS" | node studio/browser/plan.js --pull-request | paste -sd ' ' -)"; then
    echo "studio/browser/plan.js could not read the diff: nothing was run." >&2
    exit 1
  fi
fi
called() {
  local step="$1"
  grep -qx "$step=true" <<< "$STEPS"
  return $?
}

# shellcheck source=gate/azurite.sh
source "$ROOT/scripts/gate/azurite.sh"
start_azurite "[verify]"

# The host's route tests read the bundle, so it is built when it is missing as well as when battle/ changed.
if called battle || [[ ! -d table/battle-build ]]; then
  run_step "battle bundle" bash -c 'npm --prefix battle ci --ignore-scripts && npm --prefix battle run build'
else
  skip_step "battle bundle" "built, and battle/ unchanged on this branch (rebuild it after merging main)"
fi

run_step "dotnet restore" dotnet restore
run_step "dotnet build" dotnet build --no-restore
run_step "dotnet format" dotnet format --verify-no-changes --no-restore
run_step "dotnet test" dotnet test --no-build
run_step "page tests" node --test studio/*.test.js table/*.test.js showcase/*.test.js

if called learning; then
  run_step "learning" learning_gate
else
  skip_step "learning" "nothing it reads changed"
fi

# data/dst/ is ignored by git, and the host refuses to start without its schema: a fresh clone builds it.
if called content || [[ ! -f data/dst/game.schema.json ]]; then
  run_step "content" dotnet run --project tools/DownfallArena.DataBuilder -- data data/dst
else
  skip_step "content" "built, and nothing it is built from changed"
fi

if [[ -z "${AZURITE_BLOB_ENDPOINT:-}" || -z "${AZURITE_QUEUE_ENDPOINT:-}" || -z "${AZURITE_TABLE_ENDPOINT:-}" ]]; then
  SUMMARY+=("note  storage tests: skipped for want of Azurite; report them as skipped, not passed")
fi
SUMMARY+=("note  browser, for /browser-check: $JOURNEYS")

report
if [[ -n "$TREE" ]] && "$ROOT/scripts/gate/state.sh" record "$TREE"; then
  echo "The gate passed; the tree it started on is recorded."
else
  echo "The gate passed, but its tree could not be recorded: the Stop hook will still ask for it." >&2
fi
exit 0
