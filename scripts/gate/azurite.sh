#!/usr/bin/env bash
# Sourced, not run: `start_azurite <prefix>` starts the storage emulator when it is installed and not answering,
# and exports AZURITE_BLOB_ENDPOINT, AZURITE_QUEUE_ENDPOINT and AZURITE_TABLE_ENDPOINT for each service that
# answers. The SessionStart hook and scripts/verify.sh both start it, because the storage adapters' tests skip
# themselves without an emulator, and a skipped test is green.
#
# Azurite comes from the environment's setup script (npm install -g azurite); nothing here installs it, because
# installing it would cost every session.

azurite_answers() {
  local port="$1"
  curl -s -o /dev/null --max-time 1 "http://127.0.0.1:$port/devstoreaccount1"
  return $?
}

# Each service is probed on its own: a blob-only emulator already running would otherwise pass for all three,
# and the queue and table tests would still skip.
all_azurite_services_answer() {
  azurite_answers 10000 && azurite_answers 10001 && azurite_answers 10002
  return $?
}

start_azurite() {
  local prefix="$1"
  local data_dir="${AZURITE_DATA_DIR:-$HOME/.azurite}"
  local service port variable
  local missing=()

  if ! command -v azurite > /dev/null 2>&1; then
    echo "$prefix Azurite is not installed: the storage tests will be skipped. Say so when reporting the gate."
    return 0
  fi

  if ! all_azurite_services_answer; then
    mkdir -p "$data_dir"
    nohup azurite --silent --skipApiVersionCheck --location "$data_dir" > "$data_dir/azurite.log" 2>&1 &
    for _ in 1 2 3 4 5 6 7 8 9 10; do
      all_azurite_services_answer && break
      sleep 1
    done
  fi

  for service in blob:10000 queue:10001 table:10002; do
    port="${service#*:}"
    variable="AZURITE_$(echo "${service%%:*}" | tr '[:lower:]' '[:upper:]')_ENDPOINT"
    if azurite_answers "$port"; then
      export "$variable=http://127.0.0.1:$port/devstoreaccount1"
    else
      missing+=("${service%%:*}")
    fi
  done

  if [[ ${#missing[@]} -eq 0 ]]; then
    echo "$prefix Azurite is running; the storage tests will run."
  else
    echo "$prefix WARNING: Azurite's ${missing[*]} service(s) did not answer; their storage tests will be skipped."
  fi
  return 0
}
