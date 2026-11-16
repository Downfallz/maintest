#!/usr/bin/env bash
# Keeps one open issue while something that should be green is red (ADR 0182), so the next session starts
# from it instead of finding out from its own pull request.
#
#   health-issue.sh red <label> "<title>"     opens the issue, or adds this run to the one already open
#   health-issue.sh green <label>             closes it, saying which run was green again
#
# Inputs (environment): GH_TOKEN, GH_REPO, RUN_URL, COMMIT. Needs `issues: write`.
set -euo pipefail

state="$1"
label="$2"
title="${3:-}"

existing="$(gh issue list --label "$label" --state open --json number --jq '.[0].number // empty')"

if [[ "$state" == "green" ]]; then
  if [[ -n "$existing" ]]; then
    gh issue close "$existing" --comment "Green again at ${COMMIT}: ${RUN_URL}"
  fi
  exit 0
fi

body="Red at ${COMMIT}: ${RUN_URL}

The failed jobs and their logs are in that run. This issue closes itself at the next green run."

if [[ -n "$existing" ]]; then
  gh issue comment "$existing" --body "Still red at ${COMMIT}: ${RUN_URL}"
else
  gh label create "$label" --color B60205 --description "Opened by CI while it is red" --force
  gh issue create --title "$title" --label "$label" --body "$body"
fi
