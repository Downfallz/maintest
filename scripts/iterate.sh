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
  --baseline <agent>     a third opponent every policy of the turn is also played against, on top of Greedy
                         and Random: the current champion, usually `heuristic:learning/weights/search-4.json`.
                         Greedy is a solved opponent and beating it stopped saying much, but the reason to
                         read more than one is stronger than that: the matchups here are **not transitive**.
                         Measured on `7e199df4` (journal, 2026-09-15), `ci-69` scores 0.5325 against
                         `search-4` and 0.725 against Greedy, where `search-4` itself scores 0.930 against
                         Greedy -- a clone at parity with its teacher and 20 points behind it against a third
                         agent. One number never describes an agent here; the panel does.

How much data
  --matches <n>          matches of the recorded greedy self-play dataset (default 200). More matches means
                         rarer moves get enough examples; the first thing to raise when a policy learns
                         something odd from too few of them.
  --seed <n>             base seed of that dataset (default 1); change it to record different matches
  --teacher <agent>      the agent whose play is recorded (default greedy). A clone can only be as good as
                         what it imitates, and Greedy is beaten 92.75% by a searched set on this catalogue
                         (journal, 2026-09-15), so a stronger teacher raises the ceiling and visits positions
                         Greedy never reaches. Takes any agent spec: `heuristic:learning/weights/search-4.json`,
                         or `policy:runs/<previous>/value/policy.json` to record the next dataset with what the
                         previous turn trained, which is what makes the turns policy iteration rather than one
                         isolated fit each. The exploring dataset deviates from the same agent rather than from
                         Greedy, so the two policies of one turn learn from one player.
  --traces <n>           match traces kept per recorded dataset (default 4). A trace is the viewer's
                         artifact, not a learner's: nothing here trains on one, and at about twenty times
                         the disk of the steps from the same match, one per match is what stops a dataset
                         from being large enough to fit rare actions. 1000 matches keep 70 MB of steps and
                         would write 4.7 GB of traces. Use 0 for none. The viewer's fizzle and crit tiles
                         count only the traces it was given and print their denominator; the rates the
                         report quotes come from the evaluations, which read every match either way.
  --explore <rate>       also record a second dataset where that share of decisions is taken at random
                         instead of greedily (try 0.2), and train the value policy on it (ADR 0014). Greedy
                         always plays the same move in the same position, so its own games never show what a
                         different move would have given; the value policy needs that to compare moves. The
                         clone keeps learning from the pure dataset, which doubles the recording time.

The value policy (train-value: predicts the return of an action, plays the best predicted)
  --value-alpha <x>      how strongly the fit is pulled toward zero (default 1.0). Higher means more
                         cautious: with 383 features per action, raise it (10, 100) when the policy trusts
                         a handful of examples too much.
  --value-min-samples <n> examples an action needs before it gets its own fit (default 5); below that, it
                         keeps the average return of the whole dataset. Raise it (50) so a rare move cannot
                         be scored on almost nothing.
  --value-baseline-alpha <x>
                         how strongly the state baseline alone is pulled toward zero (ADR 0048).
                         Empty means it shares --value-alpha, which is what every run before this
                         did. They want very different numbers: on the 1000-match exploring dataset
                         the baseline's held-out r2 rises from 0.0705 at 10 to 0.1021 at 10000,
                         while the action rows are fitted on sixty examples each and want the small
                         one. Sharing one number served neither.
  --value-lambda <x>     how far an advantage looks ahead along its own trajectory (ADR 0046). 1.0, the
                         default, labels every decision of a match with the match's own outcome, which is
                         what every run before this did and carries no credit assignment at all. 0.0 keeps
                         only how much the state value moved in one step: far less variance, and only as
                         good as the baseline. Try 0.95, then 0.5.
  --value-discount <x>   how much a later step is worth (default 1.0, no discounting)
  --value-share <what>   what an action row is fitted on (ADR 0045). `action` is one regression per action
                         key over the whole observation: 431 weights from the steps of that one key, which on
                         a 1000-match dataset is a median of 60. `kind` fits one regression per decision kind
                         over every step of that kind and leaves each action a scalar, so the board response
                         is determined and only the scalar is thin. Default `action` until the two are
                         measured on the same dataset.

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
baseline=""
open_page=false
matches=200
traces=4
teacher=greedy
explore=
seed=1
value_alpha=1.0
value_min_samples=5
value_share=action
value_lambda=1.0
value_discount=1.0
value_baseline_alpha=
clone_epochs=20
clone_alpha=0.0001
validation=0.2
while [[ $# -gt 0 ]]; do
  case "$1" in
    --run) run_id="$2"; shift 2 ;;
    --against) against="$2"; shift 2 ;;
    --baseline) baseline="$2"; shift 2 ;;
    --open) open_page=true; shift ;;
    --matches) matches="$2"; shift 2 ;;
    --traces) traces="$2"; shift 2 ;;
    --explore) explore="$2"; shift 2 ;;
    --seed) seed="$2"; shift 2 ;;
    --teacher) teacher="$2"; shift 2 ;;
    --value-alpha) value_alpha="$2"; shift 2 ;;
    --value-min-samples) value_min_samples="$2"; shift 2 ;;
    --value-share) value_share="$2"; shift 2 ;;
    --value-lambda) value_lambda="$2"; shift 2 ;;
    --value-baseline-alpha) value_baseline_alpha="$2"; shift 2 ;;
    --value-discount) value_discount="$2"; shift 2 ;;
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
# Called as a module rather than through the console scripts, so an environment that holds only the locked
# dependencies of the project can run the loop (what CI does: nothing is built from source there).
learning=(uv run --project learning python -m downfall_learning.cli)

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
if [[ -n "$baseline" ]]; then
  evaluate baseline-vs-greedy "$baseline" greedy
fi

step "4. Record a $teacher self-play dataset ($matches matches from seed $seed, $traces trace(s))"
"${cli[@]}" simulate --p1 "$teacher" --p2 "$teacher" --matches "$matches" --seed "$seed" --traces "$traces" --record "$run/dataset" --out "$run/dataset.csv"

# The value policy trains on the explored dataset when there is one, the clone always on the pure one: a clone
# of a bot that is wrong on purpose part of the time is not the baseline the report compares run to run.
value_dataset="$run/dataset"
if [[ -n "$explore" ]]; then
  # The exploring agent deviates from the teacher, not from Greedy: whatever follows the rate is read as a
  # whole agent spec, so any teacher can be explored -- including a policy, which is what lets a turn record
  # its next dataset with what the previous one trained. `explore:<rate>` alone already means Greedy and is
  # left exactly as it was, so a greedy turn stamps the way every earlier one did. The kind is matched
  # case-insensitively because the engine parses it that way.
  explorer="explore:$explore"
  if [[ "${teacher,,}" != greedy ]]; then
    explorer="explore:$explore:$teacher"
  fi
  step "4b. Record an exploring self-play dataset ($explorer)"
  "${cli[@]}" simulate --p1 "$explorer" --p2 "$explorer" --matches "$matches" --seed "$seed" \
    --traces "$traces" --record "$run/dataset-explore" --out "$run/dataset-explore.csv"
  value_dataset="$run/dataset-explore"
fi

step "5. Train the value policy on '$value_dataset' and the clone on '$run/dataset' (alpha $value_alpha, min samples $value_min_samples, share $value_share, lambda $value_lambda, discount $value_discount, baseline alpha ${value_baseline_alpha:-shared}; epochs $clone_epochs, alpha $clone_alpha)"
# An array rather than an unquoted ${x:+...}, which is how the rest of this file passes optional
# arguments: the expansion has to stay unquoted to vanish when empty, and unquoted is exactly what
# word-splits a value with a space in it.
baseline_alpha_arguments=()
if [[ -n "$value_baseline_alpha" ]]; then
  baseline_alpha_arguments=(--baseline-alpha "$value_baseline_alpha")
fi
"${learning[@]}" train-value "$value_dataset" -o "$run/value" --alpha "$value_alpha" --min-samples "$value_min_samples" --share "$value_share" --gae-lambda "$value_lambda" --discount "$value_discount" --validation "$validation" "${baseline_alpha_arguments[@]}"
"${learning[@]}" train-clone "$run/dataset" -o "$run/clone" --epochs "$clone_epochs" --alpha "$clone_alpha" --validation "$validation"

step "6. Evaluate the policies against the baselines"
for model in value clone; do
  evaluate_policy "$run/$model" greedy "$run/evaluations/$model-vs-greedy.json"
  evaluate_policy "$run/$model" random "$run/evaluations/$model-vs-random.json" --no-log
  if [[ -n "$baseline" ]]; then
    evaluate_policy "$run/$model" "$baseline" "$run/evaluations/$model-vs-baseline.json" --no-log
  fi
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
