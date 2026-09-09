#!/usr/bin/env bash
# One iteration of the learning loop (learning phase L7, docs/learning/explained.md): rebuild the content,
# check the benchmark digest, play the baselines, record a greedy self-play dataset, train the value and clone
# policies, evaluate them against the baselines, replay the previous run's policy when its feature schema
# still applies, and write runs/<run-id>/report.json and report.html with what moved since the previous run.
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/iterate.sh [options]

One turn of the learning loop into runs/<run-id>/: baselines, a recorded dataset, two trained policies, their
evaluations, and the report. Needs the .NET SDK (global.json) and uv. Every evaluation plays the 200 benchmark
seeds, mirrored. Around ten minutes.

Where things go
  --run <id>             name of the run directory under runs/ (default: a UTC timestamp)
  --against <id>         a previous run to compare with; its value policy is replayed on this content
  --open                 try to open runs/<id>/report.html in a browser at the end (best effort)

How much data
  --matches <n>          matches of the recorded greedy self-play dataset (default 200). More matches means
                         rarer moves get enough examples; the first thing to raise when a policy learns
                         something odd from too few of them.
  --seed <n>             base seed of that dataset (default 1); change it to record different matches

The value policy (train-value: predicts the return of an action, plays the best predicted)
  --value-alpha <x>      how strongly the fit is pulled toward zero (default 1.0). Higher means more
                         cautious: with 383 features per action, raise it (10, 100) when the policy trusts
                         a handful of examples too much.
  --value-min-samples <n> examples an action needs before it gets its own fit (default 5); below that, it
                         keeps the average return of the whole dataset. Raise it (50) so a rare move cannot
                         be scored on almost nothing.

The clone policy (train-clone: imitates the recorded bot's choices)
  --clone-epochs <n>     passes over the dataset (default 20); the best pass on held-out matches is kept
  --clone-alpha <x>      the same pull toward zero, for the classifier (default 0.0001)

Both
  --validation <share>   share of matches held out to check the models (default 0.2)

Every knob is explained in docs/learning/explained.md ("The knobs"). The report is read in
docs/learning/explained.md ("How to read a report").
USAGE
}

run_id="$(date -u +%Y%m%d-%H%M%S)"
against=""
open_page=false
matches=200
seed=1
value_alpha=1.0
value_min_samples=5
clone_epochs=20
clone_alpha=0.0001
validation=0.2
while [[ $# -gt 0 ]]; do
  case "$1" in
    --run) run_id="$2"; shift 2 ;;
    --against) against="$2"; shift 2 ;;
    --open) open_page=true; shift ;;
    --matches) matches="$2"; shift 2 ;;
    --seed) seed="$2"; shift 2 ;;
    --value-alpha) value_alpha="$2"; shift 2 ;;
    --value-min-samples) value_min_samples="$2"; shift 2 ;;
    --clone-epochs) clone_epochs="$2"; shift 2 ;;
    --clone-alpha) clone_alpha="$2"; shift 2 ;;
    --validation) validation="$2"; shift 2 ;;
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
  local name="$1" agent_a="$2" agent_b="$3"
  "${cli[@]}" evaluate --p1 "$agent_a" --p2 "$agent_b" --seeds "$seeds" --out "$run/evaluations/$name.json"
}

evaluate_policy() {
  # evaluate_policy <model dir> <opponent> <output> [extra args]: a trained policy against a baseline.
  local model="$1" opponent="$2" output="$3"
  shift 3
  "${learning[@]}" evaluate-policy "$model" --opponent "$opponent" --seeds "$seeds" --output "$output" "$@"
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

step "5. Train the value and clone policies (alpha $value_alpha, min samples $value_min_samples; epochs $clone_epochs, alpha $clone_alpha)"
"${learning[@]}" train-value "$run/dataset" -o "$run/value" --alpha "$value_alpha" --min-samples "$value_min_samples" --validation "$validation"
"${learning[@]}" train-clone "$run/dataset" -o "$run/clone" --epochs "$clone_epochs" --alpha "$clone_alpha" --validation "$validation"

step "6. Evaluate the policies against the baselines"
for model in value clone; do
  evaluate_policy "$run/$model" greedy "$run/evaluations/$model-vs-greedy.json"
  evaluate_policy "$run/$model" random "$run/evaluations/$model-vs-random.json" --no-log
done

if [[ -n "$against" ]]; then
  step "7. Replay the previous value policy of '$against' on this content"
  if [[ -f "runs/$against/value/policy.json" ]]; then
    if ! evaluate_policy "runs/$against/value" greedy "$run/evaluations/previous-value-vs-greedy.json" --no-log; then
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

page="$root/$run/report.html"
echo
echo "Done. Open '$page' in a browser: the viewer with this run already loaded (report, evaluations, training curves)."
echo "Add a journal entry (docs/learning/journal.md) if a number moved."
if [[ "$open_page" == true ]]; then
  if command -v xdg-open >/dev/null 2>&1; then xdg-open "$page" >/dev/null 2>&1 || true
  elif command -v open >/dev/null 2>&1; then open "$page" >/dev/null 2>&1 || true
  else echo "No opener found (xdg-open or open); open the page by hand."
  fi
fi
