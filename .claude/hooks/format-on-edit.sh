#!/usr/bin/env bash
# PostToolUse hook: apply .editorconfig whitespace formatting to a C# file right after Claude edits it.
# Uses `dotnet format whitespace --folder`, which does not need a build, so it stays fast.
set -uo pipefail

INPUT="$(cat)"
FILE="$(printf '%s' "$INPUT" | sed -n 's/.*"file_path": *"\([^"]*\)".*/\1/p' | head -n 1)"

case "$FILE" in
  *.cs) ;;
  *) exit 0 ;;
esac

if ! command -v dotnet > /dev/null 2>&1; then
  exit 0
fi

PROJECT_DIR="${CLAUDE_PROJECT_DIR:-$(pwd)}"
case "$FILE" in
  "$PROJECT_DIR"/legacy/*) exit 0 ;;
esac

REL="$(realpath --relative-to="$PROJECT_DIR" "$FILE" 2> /dev/null || echo "$FILE")"
(cd "$PROJECT_DIR" && dotnet format whitespace . --folder --include "$REL" > /dev/null 2>&1) || true
exit 0
