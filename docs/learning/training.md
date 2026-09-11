# Training

The Python side of the learning stack (phases L6 and L7 of the [learning roadmap](../learning-roadmap.md),
ADR 0013): the `learning/` project, managed with `uv`, that reads what the engine records and writes back
what the engine and the viewer read. The engine never learns; it records datasets, evaluates agents, and
hosts policies. New to this? Read [explained.md](explained.md) first.

## The project

```
learning/
  pyproject.toml, uv.lock       the project, its dependencies (numpy, pandas, scikit-learn), ruff and pytest
  src/downfall_learning/
    stamps.py                   the run stamp and what differs between two of them
    artifacts.py                manifests, steps, episodes, evaluations; the dataset they form
    features.py                 the feature schema versions this side can read
    policy.py                   the policy.json exchange format and its scorer
    report.py                   training.jsonl, the contract the viewer reads
    export.py                   weights files, the wide CSV projection of a dataset
    training.py                 the split by match and the feature scaling the learners share
    train_clone.py              behaviour cloning
    train_value.py              value regression
    search_weights.py           the weight search and the engine evaluator it runs
    evaluate_policy.py          playing a trained policy with the engine, the win rate into its log
    iteration.py                the report of one iteration and what moved since another
    cli.py                      the commands
  tests/                        pytest, with synthetic runs and the viewer samples as fixtures
  weights/                      scoring weights files for heuristic:<file> (greedy.json is the built-in set)
```

```bash
uv sync --project learning                              # installs the project and its dev tools
uv run --project learning ruff check learning           # what CI runs, with ruff format --check
cd learning && uv run pytest                            # the tests, with coverage in CI
```

CI runs ruff and pytest in their own job and imports the Python coverage into the SonarCloud analysis, so
the quality gate covers the Python code like the C# code. Dependabot keeps `uv.lock` current.

## Reading artifacts

`artifacts.load_run` reads a run directory (`docs/learning/artifacts.md`) and checks that every step was
observed under the manifest's schema id; `load_runs` reads several and refuses to mix run stamps unless
`allow_mixed` says so, since a dataset built on two engines or two contents measures neither. Two schema ids
never mix, even when allowed: they are two layouts. `build_dataset` joins every step with the return of its
episode; `kinds` keeps only some decisions (`Intent`, `Targets`, ...).

`features.check_schema` refuses a schema version this side does not know (`features:v1` today), the same
refusal the engine's policy agent will make (L7). `compare-stamps` prints what moved between two artifacts
(manifests, evaluations, policies) on the axes of ADR 0013: content, engine, rules, schema, agents, seed.

## Tuning the content

`search-weights` tunes the agent; `tune-content` tunes the game. The space it searches is declared in
`data/balance/knobs.json`: per spell, the numbers a pass may move and their bounds, what the spell is for in
words, and the objective as bands over the metrics `report.json` already publishes (ADR 0021).

| Command | Needs | Writes | How |
| --- | --- | --- | --- |
| `check-knobs` | the knobs file and `data/` | nothing | Fails when a spell has no entry, a pointer addresses nothing, or the authored value sits outside its own bounds. Lists the dominated and indistinguishable spells the catalogue already carries. |
| `tune-content` | the same, plus a built engine | `tune.json` and the changed spell files under `content/` | Hill climbs: play the content, then play neighbours of the best, one knob at a time. A candidate that breaks a constraint is redrawn before the engine sees it. `--apply` writes the winning numbers into `data/`. |

**Every one of these runs explains its own result rather than printing it.** `tune-content` names what it
changed in the content's own words, which measurement the gain came from, what that gain cost elsewhere, and
what is still outside its range with the number it reads beside the number it should be. `search-weights`
names which weights moved, prints what it measured, and then says plainly that the run cannot settle whether
that is better: it keeps the best of what it drew, so the best score beats the initial one by construction,
and every candidate played the same fixed seeds, so comparing two marginal intervals is not a test of the
difference between them in either direction. What settles it is replaying the weights on seeds the search
never saw. `evaluate-policy` says whether its win rate's interval
contains one half, because a win rate of 0.54 whose interval runs 0.50 to 0.58 is not a 54% policy, it is a
policy that many matches could not measure.

The score is `sum(weight * (excess / scale) ** 2)` over the objective's targets, zero being on target. Seven
of them are `report.json` metrics under their own names, so a tuning run and a normal run are read the same
way. Six the tuner derives from `spellOutcomes`, because they need the catalogue as well as the evaluation:
`spellUsageShare`, `spellsNeverCast` and `spellsBarelyCast` over the whole catalogue, and three over a
**tier** — the spells
offered at one depth of the talent tree, which is the set a player chooses between. `tierUsageShare` asks
whether one of them owns the tier, `tierDamageSpread` whether they hit comparably hard per landed cast, and
`tierWinSpread` whether they win comparably often. Each reports its worst tier, and each skips what it
cannot read: a tier nobody cast, a spell with no `Damage` effect, a spell too few sides declared for its
own number to mean anything. Damaging is read from the content, so an attack whose hits are absorbed widens
the spread rather than leaving it.

`docs/learning/explained.md` says what a band, a scale and a weight are in plain words, and how to point the
objective at a match length or at your own agent.

A candidate costs one content build plus one evaluation per objective entry, about twenty seconds on the
benchmark seeds. The opening sweep is up to two candidates per playable knob before the climb starts, plus
up to two more per pair of knobs on a spell no single step could move at all — on the nine-spell core
content that is up to 41 swept and up to 47 paired. `--no-sweep` and `--no-pairs` turn each off.

## Three learners

| Command | Needs | Writes | How |
| --- | --- | --- | --- |
| `search-weights -o <dir>` | the built engine and `data/dst`, no dataset | `weights.json`, `search.json`, `evaluation.json`, `training.jsonl` | Cross-entropy method over the eight scoring weights. Each candidate is a weights file evaluated by the engine's `evaluate` as `heuristic:<file>` against `--opponent` (default `greedy`) on `--seeds` (default the benchmark seeds), mirrored; the fitness is the mean score. The mean of the elite becomes the next mean, its spread the next spread; the current mean is always in the population, so the best is never lost. |
| `train-clone <runs> -o <dir>` | a recorded run (`simulate --record`) | `policy.json`, `training.jsonl` | Behaviour cloning: a linear classifier from observation to action key (`SGDClassifier`, log loss), one epoch per iteration, keeping the epoch whose choice among the candidates matches the data best on held-out matches. |
| `train-value <runs> -o <dir>` | a recorded run, ideally an explored one (below) | `policy.json`, `training.jsonl` | Value regression in two parts (ADR 0016): one baseline over the observation alone, fitted on every step, then one ridge regression per action key over what the baseline leaves. A score is the baseline plus the action's row, so it still predicts the return, and the agent takes the candidate that scores best. A key seen fewer than `--min-samples` times keeps its mean, and an unseen key the mean of the data — both on top of the baseline. |

Matches are held out whole (`--validation`, default one in five), so a validation step never comes from a
match the model saw. Features are standardized for the optimizer and the scaling is folded back into the
weights, so a policy file stays a plain dot product. `export-csv` writes the wide CSV projection of a dataset
(one row per step, one column per feature) for anything that prefers a table.

The weight search is the slow one: one engine run per candidate, a few seconds each on 400 matches, so ten
iterations of sixteen take around ten minutes. A smaller seed file (`--seeds`) makes it faster and noisier.

## How big a dataset fits

A run is loaded into one array of one row per step, and every step keeps a view on its row rather than its
own numbers. At the width of `features:v1` for a team of three (383 features) that array is about 3 MB per
thousand steps, and the loader's own overhead adds roughly half a kilobyte per step; measured peak resident
memory, which includes what the JSON parsing leaves behind, is closer to 9 MB per thousand steps. A recorded
match is a few hundred steps, so:

| Dataset | Steps | Peak while loading |
| --- | --- | --- |
| 200 matches | ~50,000 | ~0.5 GB |
| 1000 matches | ~320,000 | ~3 GB |

Training on several runs at once (`train-value a b --allow-mixed`) loads each of them and then copies the
selection into one array, so budget the sum plus one more copy. A machine that runs out is killed by the
kernel with no Python error and no output file: if a training command ends silently and leaves nothing
behind, check its exit code for `137` and record fewer matches.

## Why the value learner fits a baseline first

A step's return is the outcome of a whole match of about a hundred and fifty decisions, credited to every
decision in it. It therefore measures the position the player was in far more than the move they chose. When
each action key is fitted on the raw return, every row spends its capacity re-learning the position, on its
own slice of positions, and their intercepts end up calibrated on different worlds — which is exactly what
makes two of them incomparable at the moment of choosing.

So `train-value` fits one state-value model `V(s)` on every training step, with no split by action, and
regresses each action key on `return - V(s)`. The baseline sees the whole dataset, so it is the
best-determined part of the model; what is left for a row is the part its own action is responsible for,
which is the quantity the policy needs. Both go in `policy.json`: the score of a candidate is the baseline's
value plus the action's row, so it still reads as a predicted return, and since the baseline is the same
number for every candidate at a state it never changes which one wins.

`training.jsonl` and the policy's metrics carry `baselineR2` beside `r2`: what the position alone explains,
against what the position and the action explain together. A `baselineR2` close to `r2` means the action rows
are adding nothing. `fittedActions` beside `actions` says how many keys got a regression of their own rather
than a mean.

The runs that led here are in `docs/learning/journal.md`: seven straight losses of 400 matches to `Greedy`,
across exploration, five times the data, and thresholds from 5 to 50, with the held-out fit moving every time
and the win rate never.

## Why the value learner needs an exploring dataset

`Greedy` plays the same move in the same position, so a dataset of its own games never shows what a different
move would have given there. Each action key is then fitted on its own slice of states: `heavy_strike` on the
states where `Greedy` wanted it, `basic_attack` on the leftovers where it was not available. The return
measured on such a slice says how good those situations were, not how good the action is, and ranking two of
those rows at one position compares models calibrated on different worlds. Three trainings confirmed it, each
losing all 400 mirrored matches against `Greedy` while behaviour cloning on the same data reached `Greedy`'s
own strength (`docs/learning/journal.md`, ADR 0014).

The `explore:<rate>` agent is the fix: it plays `Greedy`'s move except for the given share of decisions, which
it takes uniformly at random. The states stay the ones a strong policy reaches, and every action key now
appears in some of them, so the rows become comparable.

```bash
dotnet run --project src/DownfallArena.Cli -- simulate --p1 explore:0.2 --p2 explore:0.2 --matches 200 --seed 1 --record runs/<id>/dataset-explore
scripts/iterate.sh --explore 0.2      # the same, inside the loop, keeping a pure dataset for the clone
```

It draws from the seeded random source, so a recorded run replays exactly, but it never plays a baseline: the
benchmark digest is defined by agents that draw nothing. Behaviour cloning keeps the pure `Greedy` dataset,
since imitating a bot that is wrong on purpose part of the time is not what the clone is for.

## Playing a policy

`policy:<file>` seats a `PolicyAgent` in the engine: every decision builds the observation of the board,
lists the candidate actions the options offer in the order a dataset records them, scores each key with the
policy's row (or its fallback), and takes the best, the first on a tie. `AgentFactory` refuses a policy whose
schema id is not the one the current content and rule set give, with a message naming both ids; the spec is
stamped `Policy:<path>@<fingerprint>`, eight hex digits of the file's bytes.

```bash
dotnet run --project src/DownfallArena.Cli -- evaluate --p1 policy:models/value/v1/policy.json --p2 greedy --seeds benchmarks/benchmark-seeds.json
uv run --project learning evaluate-policy models/value/v1 --opponent greedy     # the same, and the win rate into training.jsonl
```

`evaluate-policy` writes `evaluation-vs-<opponent>.json` next to the model (or `--output`) and fills
`winRate`, its interval, and `matches` on the kept iteration of the model's `training.jsonl`, unless
`--no-log`. The engine's refusal of a foreign schema surfaces as an error with its message.

## The `policy.json` exchange format

A policy is a linear scorer over action keys, read without an ML runtime:

| Field | Meaning |
| --- | --- |
| `kind` | `clone` (rows are classifier logits) or `value` (rows are predicted returns). Both are read the same way. |
| `stamp` | The run stamp of the training data. |
| `schemaId`, `schemaVersion` | The feature schema of the observations (`docs/learning/features.md`); a reader refuses a version it does not know. |
| `featureNames` | The feature names, in observation order; the width of every row. |
| `actionKeys` | One entry per row, the action keys the policy knows (`docs/learning/features.md`, action encoding). |
| `weights`, `bias` | One row and one bias per action key. |
| `fallback` | The score of a candidate action the policy never saw. |
| `trainedAt`, `metrics` | When, and what the training measured (loss, accuracy, r2, step counts). |

To choose: for each candidate action the options offer, its row's dot product with the observation plus
its bias, or `fallback` when the policy has no row for that key; the best-scoring candidate wins, the first
on a tie. `Policy.choose` does it in Python; the engine's `PolicyAgent` (L7) will do the same, so a policy
plays identically on both sides.

## Model files

A trained policy is committed under `models/<name>/<version>/` (`policy.json`, `training.jsonl`, and the
`evaluation.json` of its evaluation against the baselines once L7 runs it). Datasets stay out of git
(`runs/`); they are regenerated from seeds. `learning/weights/` holds the weights files the heuristic agent
reads, the searched ones next to the built-in `greedy.json`.

## The loop (L7)

`scripts/iterate.sh [--run <id>] [--against <id>] [--matches N] [--seed S]` runs one iteration and leaves
everything under `runs/<id>/`:

1. Build the engine and the content; check the benchmark digest (`benchmark`).
2. Baselines on the benchmark seeds, mirrored: `random-vs-random`, `greedy-vs-greedy`, `greedy-vs-random`.
3. Record a greedy self-play dataset (`simulate --record`).
4. Train the value and clone policies on it (`train-value`, `train-clone`).
5. Evaluate each policy against greedy and against random (`evaluate-policy`).
6. With `--against`, replay the previous run's value policy on this content into
   `previous-value-vs-greedy.json`; when its feature schema no longer applies, say so and go on.
7. `report`: read every `evaluations/*.json`, refuse to mix engines or contents, write `report.json`, print
   the table, and with `--against` the deltas and the stamp axis that moved. It also writes `report.html`
   (unless `--no-html`): the viewer page with the stylesheet inlined and the run's `report.json`,
   evaluations, and `training.jsonl` files embedded as one JSON block the page reads at start, so the file
   opens on the report with nothing to drag in. `--viewer <dir>` points at another copy of the viewer.

```
runs/<id>/
  evaluations/*.json      one evaluation per pair of agents
  dataset/, dataset.csv   the recorded run and its summary
  value/, clone/          policy.json, training.jsonl, evaluation-vs-*.json
  report.json             the summary below
  report.html             the viewer, carrying this run: open it and it starts on the report
```

The script takes every tuning knob as a flag (`--matches`, `--value-alpha`, `--value-min-samples`,
`--clone-epochs`, `--clone-alpha`, `--validation`), passed through to the learners, so a run is tuned from
the command line and its `--help` explains each in plain words; `docs/learning/explained.md` has the table.

The same turn runs unattended in `.github/workflows/iterate.yml` (Actions -> "Learning loop"): on a push to
`main` that touches `src/`, `data/`, `learning/`, `benchmarks/` or the script, on a pull request that changes
`learning/experiments/`, weekly, and on demand. The knobs come from `learning/experiments/next.json`, so an
experiment is committed rather than typed; a workflow input, when a dispatch fills one in, wins over the file
for that run, and `explore` set to `off` skips the exploring dataset. The report table goes in the run
summary, under the file's `why`; `report.html`, `report.json`, the evaluations and the two policies are the
run's artifact. The datasets are not uploaded: the seed reproduces them. A CI runner keeps nothing between
runs, so `--against` stays a local comparison.

Two more turns of the same crank run on the runners and nowhere else in particular, so asking for one needs
no local SDK and no machine left on:

| Workflow | Dispatch inputs | What comes back |
| --- | --- | --- |
| **Tune the catalogue** (`tune.yml`) | search seed, rounds, neighbours, knobs per proposal, and whether to apply | The proposal in the run summary, and, when it moved something, a **branch** carrying the changed spell files and a regenerated benchmark digest, with a link that opens it as a pull request. |
| **Search the agent weights** (`search.yml`) | opponent, seed file, rounds, population, search seed | The weights in the run summary, as ratios to `damage`, beside the baseline's. Nothing is committed. |

The asymmetry is deliberate. A tuning pass proposes content, and content is reviewed as a diff, so it arrives
on a branch that costs nothing to delete. A weight search proposes an *agent*, and `Greedy`'s weights are the
baseline every learned agent is measured against, so adopting them makes every comparison in the journal
incomparable — that is a commit with an entry that says why, not a workflow's side effect. The 2026-09-10
entry is the worked example: the search found a better agent and the entry decided against taking it.

**The tuning workflow pushes the branch and stops there, on purpose.** A pull request opened by a workflow
does not start the `pull_request` workflows, so it would arrive with no CI and no SonarCloud quality gate —
which this file's own rule requires on every pull request. The run summary carries the link that opens the
branch as a pull request instead: one tap, the repository's real checks run, and because the branch holds a
single commit GitHub fills the description in from its message, which the workflow wrote for that purpose.

Before it pushes anything the job runs the gate a contributor runs — the .NET build, tests and format check,
and the learning project's ruff, format check and pytest — so a proposal that breaks any of them never becomes
a branch. Both jobs call the learning project as a module (`python -m downfall_learning.cli`), the way
`scripts/iterate.sh` does: the runners install the locked dependencies without installing the project itself,
so its console scripts are not on the path there.

`report.json` holds the run's stamp and, per evaluation, the agents, the matches, and the metrics: `winRateA`
with its interval, `scoreA`, `player1WinShare` (the share of matches player 1 won, near one half when the
agents are identical), `drawRate`, `averageRounds`, `roundCapShare`, `spellEntropyA`/`B`, `fizzleRateA`/`B`.
These are the balance signals the roadmap asks the loop to show every time. `report --against <run>` adds the
deltas for every evaluation both runs hold and lists the stamp axes that differ (content, engine, rules,
schema); agents and seeds are expected to differ between evaluations and are not axes of a report.

Then one entry in `journal.md`: what changed, the numbers, the decision.
