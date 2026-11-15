#!/usr/bin/env bash
# The readings of the hosted table that ops-report.yml prints (ADR 0185), one JSON file each in <dir>, for
# scripts/ops/report.mjs to turn into the report. A reading that fails is written as {"error": "..."} and the next
# one is read all the same, so one refused permission costs one line of the report, not the report.
#
#   scripts/ops/read.sh <dir>
#
# Inputs (environment): AZURE=1 once `az` is signed in as the ops identity (anything else reads the table
# alone), RESOURCE_GROUP, NAME_PREFIX (default downfall), TABLE_DOMAIN (the table's own host name, optional).
# It only reads: every Azure call is a GET, or a POST that the Cost Management and Log Analytics query APIs take.
set -uo pipefail

dir="$1"
mkdir -p "$dir"
prefix="${NAME_PREFIX:-downfall}"
arm="https://management.azure.com"
logs_api="https://api.loganalytics.io"

# One reading into <dir>/<name>.json: the command's output, or its error.
reading() {
  local name="$1" out
  shift
  if out="$("$@" 2>"$dir/.err")" && [[ -n "$out" ]]; then
    printf '%s\n' "$out" > "$dir/$name.json"
  else
    jq -n --arg error "$(tr '\n' ' ' < "$dir/.err" | head -c 300)" '{error: $error}' > "$dir/$name.json"
  fi
  rm -f "$dir/.err"
  return 0
}

get() {
  az rest --method get --url "$arm$1" --output json
}

post() {
  az rest --method post --url "$1" --body "$2" --output json "${@:3}"
}

# A metric over the last 24 hours, by hour; a filter, URL-encoded, narrows it to one dimension.
metric() {
  local resource="$1" name="$2" aggregation="$3" filter="${4:-}"
  get "$resource/providers/Microsoft.Insights/metrics?api-version=2023-10-01&metricnames=$name&aggregation=$aggregation&interval=PT1H&timespan=PT24H${filter:+&%24filter=$filter}"
}

# The console lines of the last 24 hours that say something failed, counted by app and line; "deliveries, onto"
# is the worker's line for a turn it gave up on (not "given up on", which every host's banner says).
read -r -d '' ERRORS <<'KQL'
ContainerAppConsoleLogs_CL
| where TimeGenerated > ago(1d)
| extend app = coalesce(column_ifexists('ContainerAppName_s', ''), column_ifexists('ContainerJobName_s', '')), stream = column_ifexists('Stream_s', '')
| where stream == 'stderr' or Log_s has_any ('error', 'failed', 'could not', 'exception', 'unhandled') or Log_s has 'deliveries, onto'
| summarize n = count() by app, line = substring(Log_s, 0, 300)
| top 200 by n desc
KQL

# The month to date, as Cost Management reads it for the resource group.
COST='{"type":"ActualCost","timeframe":"MonthToDate","dataset":{"granularity":"None","aggregation":{"totalCost":{"name":"Cost","function":"Sum"}}}}'

url=""
if [[ -n "${TABLE_DOMAIN:-}" ]]; then
  url="https://${TABLE_DOMAIN}"
fi

if [[ "${AZURE:-0}" == "1" ]]; then
  echo '{"signedIn": true}' > "$dir/azure.json"
  subscription="$(az account show --query id --output tsv)"
  group="/subscriptions/$subscription/resourceGroups/$RESOURCE_GROUP"
  app="$group/providers/Microsoft.App/containerApps/$prefix-table"
  worker="$group/providers/Microsoft.App/containerApps/$prefix-worker"

  reading app get "$app?api-version=2024-03-01"
  if [[ -z "$url" ]]; then
    fqdn="$(jq -r '.properties.configuration.ingress.fqdn // empty' "$dir/app.json")"
    url="${fqdn:+https://$fqdn}"
  fi
  reading revisions-table get "$app/revisions?api-version=2024-03-01"
  reading revisions-worker get "$worker/revisions?api-version=2024-03-01"
  reading restarts-table metric "$app" RestartCount Maximum
  reading restarts-worker metric "$worker" RestartCount Maximum
  reading 5xx metric "$app" Requests Total "statusCodeCategory%20eq%20%275xx%27"
  reading executions get "$group/providers/Microsoft.App/jobs/$prefix-tend/executions?api-version=2024-03-01"

  storage="$(get "$group/providers/Microsoft.Storage/storageAccounts?api-version=2023-05-01" 2>/dev/null | jq -r '.value[0].id // empty')"
  if [[ -n "$storage" ]]; then
    reading queue metric "$storage/queueServices/default" QueueMessageCount Average
  else
    echo '{"error": "no storage account in the resource group"}' > "$dir/queue.json"
  fi

  workspace="$(get "$group/providers/Microsoft.OperationalInsights/workspaces?api-version=2023-09-01" 2>/dev/null | jq -r '.value[0].properties.customerId // empty')"
  if [[ -n "$workspace" ]]; then
    reading logs post "$logs_api/v1/workspaces/$workspace/query" "$(jq -n --arg query "$ERRORS" '{query: $query, timespan: "P1D"}')" --resource "$logs_api"
  else
    echo '{"error": "no Log Analytics workspace in the resource group"}' > "$dir/logs.json"
  fi

  reading cost post "$arm$group/providers/Microsoft.CostManagement/query?api-version=2023-03-01" "$COST"
  reading budget get "$group/providers/Microsoft.Consumption/budgets?api-version=2023-05-01"
else
  echo '{"signedIn": false}' > "$dir/azure.json"
fi

# The table itself, as a player's first request reaches it: a cold start can take most of a minute.
if [[ -n "$url" ]]; then
  answer="$(curl -sS -o /dev/null -m 90 -w '%{http_code} %{time_total}' "$url/api/lobby" 2>/dev/null)"
  read -r code seconds <<<"${answer:-000 0}"
  jq -n --arg url "$url" --argjson status "$((10#${code:-0}))" --argjson seconds "${seconds:-0}" \
    '{url: $url, status: $status, seconds: $seconds}' > "$dir/lobby.json"
else
  echo '{"error": "no address: neither TABLE_DOMAIN nor the app was read"}' > "$dir/lobby.json"
fi
exit 0
