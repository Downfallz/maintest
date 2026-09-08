#!/usr/bin/env bash
# Waits for the SonarCloud analysis submitted by `dotnet sonarscanner end`, then prints the quality gate
# status, every failed condition, and every open issue and hotspot on new code, so the result can be read
# from the CI log alone. Exits 1 when the quality gate failed.
#
# Inputs (environment): SONAR_TOKEN (required), SONAR_HOST_URL (default https://sonarcloud.io),
# SONAR_PULL_REQUEST (PR number, optional), SONAR_BRANCH (used when no PR number is given).
set -euo pipefail

SONAR_HOST="${SONAR_HOST_URL:-https://sonarcloud.io}"
REPORT_TASK="${SONAR_REPORT_TASK:-.sonarqube/out/.sonar/report-task.txt}"
MAX_ATTEMPTS=60
POLL_SECONDS=5

if [[ -z "${SONAR_TOKEN:-}" ]]; then
  echo "SONAR_TOKEN is not set; nothing to report."
  exit 0
fi

if [[ ! -f "$REPORT_TASK" ]]; then
  echo "No analysis report found at $REPORT_TASK. Did 'dotnet sonarscanner end' run?"
  exit 1
fi

read_property() {
  local name="$1"
  sed -n "s/^$name=//p" "$REPORT_TASK" | head -n 1
  return 0
}

api() {
  local path="$1"
  curl --proto '=https' --tlsv1.2 -fsS -H "Authorization: Bearer $SONAR_TOKEN" "$SONAR_HOST/api/$path"
  return $?
}

wait_for_task() {
  local task_id="$1"
  local attempt status
  for ((attempt = 1; attempt <= MAX_ATTEMPTS; attempt++)); do
    status="$(api "ce/task?id=$task_id" | jq -r '.task.status')"
    case "$status" in
      SUCCESS)
        return 0
        ;;
      FAILED | CANCELED)
        echo "SonarCloud analysis task ended with status $status."
        return 1
        ;;
      *)
        sleep "$POLL_SECONDS"
        ;;
    esac
  done
  echo "Timed out waiting for SonarCloud analysis task $task_id."
  return 1
}

CE_TASK_ID="$(read_property ceTaskId)"
PROJECT_KEY="$(read_property projectKey)"

wait_for_task "$CE_TASK_ID"

ANALYSIS_ID="$(api "ce/task?id=$CE_TASK_ID" | jq -r '.task.analysisId')"
GATE_JSON="$(api "qualitygates/project_status?analysisId=$ANALYSIS_ID")"
GATE_STATUS="$(jq -r '.projectStatus.status' <<< "$GATE_JSON")"

if [[ -n "${SONAR_PULL_REQUEST:-}" ]]; then
  SCOPE="pullRequest=$SONAR_PULL_REQUEST"
else
  SCOPE="branch=${SONAR_BRANCH:-main}"
fi

echo "::group::SonarCloud quality gate: $GATE_STATUS"
jq -r '.projectStatus.conditions[] | select(.status != "OK")
  | "  FAILED \(.metricKey): \(.actualValue) (required \(.comparator) \(.errorThreshold))"' <<< "$GATE_JSON"
echo "::endgroup::"

ISSUES_JSON="$(api "issues/search?componentKeys=$PROJECT_KEY&$SCOPE&issueStatuses=OPEN,CONFIRMED&ps=500")"
echo "::group::Open issues on new code: $(jq -r '.total' <<< "$ISSUES_JSON")"
jq -r '.issues[]
  | "  [\(.severity)] \(.rule) \(.component | sub("^[^:]*:"; "")):\(.line // 0) \(.message)"' <<< "$ISSUES_JSON"
echo "::endgroup::"

HOTSPOTS_JSON="$(api "hotspots/search?projectKey=$PROJECT_KEY&$SCOPE&status=TO_REVIEW&ps=500")"
echo "::group::Security hotspots to review: $(jq -r '.paging.total' <<< "$HOTSPOTS_JSON")"
jq -r '.hotspots[]
  | "  [\(.vulnerabilityProbability)] \(.ruleKey) \(.component | sub("^[^:]*:"; "")):\(.line // 0) \(.message)"' <<< "$HOTSPOTS_JSON"
echo "::endgroup::"

if [[ "$GATE_STATUS" != "OK" ]]; then
  echo "::error::SonarCloud quality gate failed ($GATE_STATUS). See the issues above." >&2
  exit 1
fi

exit 0
