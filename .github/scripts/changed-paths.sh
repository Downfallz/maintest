#!/usr/bin/env bash
# Prints the paths a run has to answer for, one a line, or nothing with exit 3 when there is no diff to read
# and the run plays everything (ADR 0182).
#
# A pull request answers for its own diff. A push to main answers for everything since main was last green,
# not for its own commit alone: a change whose run went red is still undeployed and unverified when the next
# commit lands, and a run that a newer push replaced in the queue never looked at its commit at all.
#
# Inputs (environment): EVENT (github.event_name), BEFORE (github.event.before, a push's previous head),
# GH_TOKEN and GH_REPO for the query of the last green run. Needs the history (fetch-depth: 0).
set -euo pipefail

known() {
  [[ -n "$1" && ! "$1" =~ ^0+$ ]] && git cat-file -e "$1^{commit}" 2> /dev/null
  return $?
}

case "${EVENT:-}" in
  pull_request)
    # The checkout is the merge of the pull request into its base: its first parent is the base.
    git diff --name-only HEAD^1 HEAD
    ;;
  push)
    base="$(gh api "repos/${GH_REPO}/actions/workflows/ci.yml/runs?branch=main&event=push&status=success&per_page=1" \
      --jq '.workflow_runs[0].head_sha // empty' 2> /dev/null || true)"
    if ! known "$base"; then
      base="${BEFORE:-}"
    fi
    if ! known "$base"; then
      exit 3
    fi
    echo "Changed since $base" >&2
    git diff --name-only "$base" HEAD
    ;;
  *)
    exit 3
    ;;
esac
