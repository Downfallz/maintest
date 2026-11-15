#!/usr/bin/env bash
# Stop hook: when code changed since the last green gate, say so once, before the turn ends, so a report never
# passes for verified when it was not. The gate is scripts/verify.sh (/verify), which records the tree it saw;
# scripts/gate/state.sh compares the working tree with it.
#
# "Once" is per tree: the warning is not repeated until the files change again, and a turn the warning itself
# continued (stop_hook_active) is let go, so the hook never holds a session in a loop.
set -uo pipefail

INPUT="$(cat)"
if printf '%s' "$INPUT" | grep -Eq '"stop_hook_active"[[:space:]]*:[[:space:]]*true'; then
  exit 0
fi

PROJECT_DIR="${CLAUDE_PROJECT_DIR:-$(pwd)}"
cd "$PROJECT_DIR" || exit 0
STATE="$PROJECT_DIR/scripts/gate/state.sh"
[[ -x "$STATE" ]] || exit 0

CHANGED="$("$STATE" changed 2> /dev/null)" || exit 0
[[ -n "$CHANGED" ]] || exit 0

TREE="$("$STATE" tree 2> /dev/null)" || exit 0
WARNED="$(git rev-parse --git-path claude-gate/warned 2> /dev/null)" || exit 0
if [[ -f "$WARNED" && "$(cat "$WARNED")" == "$TREE" ]]; then
  exit 0
fi
echo "$TREE" > "$WARNED"

COUNT="$(printf '%s\n' "$CHANGED" | wc -l | tr -d ' ')"
# A path is quoted into JSON by hand: backslashes and quotes escaped, control characters dropped.
SAMPLE="$(printf '%s\n' "$CHANGED" | head -n 3 | tr -d '\000-\011\013-\037' | sed 's/\\/\\\\/g; s/"/\\"/g' | paste -sd ',' - | sed 's/,/, /g')"
REASON="$COUNT path(s) changed since the last green gate ($SAMPLE). Run /verify before reporting this work as done, or say plainly that the gate was not run on these changes."
printf '{"decision": "block", "reason": "%s"}\n' "$REASON"
exit 0
