#!/usr/bin/env bash
# One iteration of the learning loop (learning phase L7, docs/learning/training.md): rebuild the content,
# check the benchmark digest, play the baselines, record a greedy self-play dataset, train the value and clone
# policies, evaluate them against the baselines, replay the previous run's policy when its feature schema
# still applies, and write runs/<run-id>/report.json with what moved since the previous run.
#
# Usage: scripts/iterate.sh [--run <run-id>] [--against <run-id>] [--matches N] [--seed S]
#   --run      the run directory under runs/ (default: a UTC timestamp)
#   --against  a previous run to compare with and whose value policy is replayed on this content
#   --matches  matches of the recorded greedy self-play dataset (default 200)
#   --seed     base seed of that dataset (default 1)
# Needs the .NET SDK (global.json) and uv. Every evaluation plays the benchmark seeds, mirrored.
set -euo pipefail

usage() {
  sed -n '2,11p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

run_id="$(date -u +%Y%m%d-%H%M%S)"
against=""
matches=200
seed=1
while [[ $# -gt 0 ]]; do
  case "$1" in
    --run) run_id="$2"; shift 2 ;;
    --against) against="$2"; shift 2 ;;
    --matches) matches="$2"; shift 2 ;;
    --seed) seed="$2"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown option '$1'." >&2; usage >&2; exit 2 ;;
  esac
done

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"
run="runs/$run_id"
seeds="benchmarks/benchmark-seeds.json"
cli=(dotnet run --project src/DownfallArena.Cli --no-build --configuration Release --)
learning=(uv run --project learning)

if [[ -e "$run" ]]; then
  echo "Run directory '$run' exists; each iteration gets its own." >&2
  exit 1
fi
mkdir -p "$run/evaluations"

step() {
  printf '\n== %s\n' "$*"
}

evaluate() {
  # evaluate <name> <agent A> <agent B>: one mirrored evaluation on the benchmark seeds into the run.
  "${cli[@]}" evaluate --p1 "$2" --p2 "$3" --seeds "$seeds" --out "$run/evaluations/$1.json"
}

step "1. Build the engine and the content"
dotnet build --configuration Release --nologo --verbosity quiet
dotnet run --project tools/DownfallArena.DataBuilder --no-build --configuration Release -- data data/dst

step "2. Check the benchmark digest (engine change detector)"
"${cli[@]}" benchmark

step "3. Baselines"
evaluate random-vs-random random random
evaluate greedy-vs-greedy greedy greedy
evaluate greedy-vs-random greedy random

step "4. Record a greedy self-play dataset ($matches matches from seed $seed)"
"${cli[@]}" simulate --p1 greedy --p2 greedy --matches "$matches" --seed "$seed" --record "$run/dataset" --out "$run/dataset.csv"

step "5. Train the value and clone policies"
"${learning[@]}" train-value "$run/dataset" -o "$run/value"
"${learning[@]}" train-clone "$run/dataset" -o "$run/clone"

step "6. Evaluate the policies against the baselines"
for model in value clone; do
  "${learning[@]}" evaluate-policy "$run/$model" --opponent greedy --seeds "$seeds" --output "$run/evaluations/$model-vs-greedy.json"
  "${learning[@]}" evaluate-policy "$run/$model" --opponent random --seeds "$seeds" --output "$run/evaluations/$model-vs-random.json" --no-log
done

if [[ -n "$against" ]]; then
  step "7. Replay the previous value policy of '$against' on this content"
  if [[ -f "runs/$against/value/policy.json" ]]; then
    if ! "${learning[@]}" evaluate-policy "runs/$against/value" --opponent greedy --seeds "$seeds" --output "$run/evaluations/previous-value-vs-greedy.json" --no-log; then
      echo "The previous policy could not run here (its feature schema no longer applies): only the baselines compare."
      rm -f "$run/evaluations/previous-value-vs-greedy.json"
    fi
  else
    echo "No value policy under 'runs/$against'; nothing to replay."
  fi
fi

step "8. Report"
if [[ -n "$against" ]]; then
  "${learning[@]}" report "$run" --against "runs/$against"
else
  "${learning[@]}" report "$run"
fi
echo
echo "Report written to '$run/report.json'. Add a journal entry (docs/learning/journal.md) if a number moved."
