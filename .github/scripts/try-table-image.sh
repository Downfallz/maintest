#!/usr/bin/env bash
# Runs the table image the way the container app will, and holds it to what ADR 0081 promises, before it is
# pushed anywhere: it runs as an unprivileged user, serves its pages, keeps the lobby behind the operator's
# door in both of its forms, and opens a table for whoever is let through.
#
# Usage: try-table-image.sh <image>. Needs docker and curl. Exits non-zero on the first broken promise.
set -euo pipefail

IMAGE="${1:?the image to try}"
PORT="${TRY_PORT:-18080}"
BASE="http://127.0.0.1:$PORT"
CONTAINER="try-table-$$"

stop() {
  docker rm -f "$CONTAINER" >/dev/null 2>&1 || true
  return 0
}
trap stop EXIT

fail() {
  echo "::error::$1" >&2
  docker logs "$CONTAINER" 2>&1 | tail -n 20 || true
  exit 1
}

# Starts the image with the arguments given, on the host port, and waits until it answers its page.
start() {
  stop
  docker run -d --name "$CONTAINER" -p "$PORT:8080" "$IMAGE" "$@" >/dev/null
  local attempt
  for ((attempt = 1; attempt <= 60; attempt++)); do
    if curl -fs -o /dev/null "$BASE/"; then
      return 0
    fi
    sleep 1
  done
  fail "The table never answered on $BASE/."
}

# The status a request is answered with; every other argument is handed to curl.
status() {
  curl -sS -o /dev/null -w '%{http_code}' "$@"
  return 0
}

expect() {
  local wanted="$1" what="$2"
  shift 2
  local got
  got="$(status "$@")"
  if [[ "$got" != "$wanted" ]]; then
    fail "$what: expected $wanted, got $got."
  fi
  echo "ok: $what ($got)"
  return 0
}

user="$(docker run --rm --entrypoint id "$IMAGE" -u)"
if [[ "$user" == "0" ]]; then
  fail "The image runs as root."
fi
echo "ok: runs as user $user"

# The image's own command: an admin panel behind the token the log prints, recording nothing.
start
expect 200 "the table page" "$BASE/"
expect 200 "the admin panel" "$BASE/admin"
expect 303 "the former lobby address redirects to the panel" "$BASE/lobby"
expect 200 "a module of the page" "$BASE/table.js"
expect 404 "a test module is not shipped" "$BASE/table.test.js"
expect 403 "the panel refuses a request without the operator's token" "$BASE/api/tables"
expect 403 "the sessions are the operator's too" "$BASE/api/sessions"
expect 200 "anybody may ask whether they are the operator" "$BASE/api/me"
expect 403 "the panel ignores the platform's header when no platform is trusted" \
  -H "X-MS-CLIENT-PRINCIPAL-NAME: stranger@example.test" "$BASE/api/tables"

token="$(docker logs "$CONTAINER" 2>&1 | sed -n 's/.*admin?token=\([0-9a-f]*\).*/\1/p' | head -n 1)"
if [[ -z "$token" ]]; then
  fail "The log does not print the operator's token."
fi
expect 201 "the operator opens a table against a searched set of weights" \
  -H "X-Seat-Token: $token" -H "Content-Type: application/json" \
  -d '{"player1":"person","player2":"heuristic:learning/weights/search-19.json"}' "$BASE/api/tables"

# The container app's form: the platform's sign-in is the door, and nothing is recorded here.
start table --lobby --platform-auth --bind 0.0.0.0 --port 8080 --rules docs/tabletop/playtest.rules.json --no-record
expect 401 "a stranger is told where to sign in" "$BASE/api/tables"
expect 201 "a signed-in operator opens a table" \
  -H "X-MS-CLIENT-PRINCIPAL-NAME: operator@example.test" -H "Content-Type: application/json" \
  -d '{"player1":"person","player2":"greedy"}' "$BASE/api/tables"

# Read the log whole before searching it: `grep -q` stops at the first match, and under pipefail the
# SIGPIPE that leaves `docker logs` would fail the check on a log that has the stamp.
log="$(docker logs "$CONTAINER" 2>&1)"
if ! grep -q '^Engine [0-9a-f]\{12\}\.' <<<"$log"; then
  fail "The engine the image was built from is not stamped on its log."
fi
echo "ok: the engine commit is stamped"
