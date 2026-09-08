# Learning roadmap

Status: Accepted (2026-09-08, decisions A to J settled in ADR 0013). Companion to [`roadmap.md`](roadmap.md), which is complete: the engine plays
whole matches, the CLI simulates batches, and every result carries its seed and content hash. This document
is the plan for agents smarter than `RandomAgent`, for the loop that lets us change the content (spells,
classes, talents) **or the engine** (a bug fix, a new condition kind) and measure what it did, and for the
viewer that lets a human see a match, a batch, or a training run at a glance.

## Goal

Three things, in this order:

1. **A yardstick.** Agents that play well enough that "does this change anything" has an answer. A random
   agent cannot tell a good spell from a bad one; a greedy or learned agent can.
2. **A loop.** Edit content or engine, rebuild, simulate, train, evaluate, read one report that says what
   changed against the previous run. Every step reproducible from a seed, a content hash, and an engine
   version, so a result from last week can be replayed and compared today.
3. **A window.** One viewer, no server, that opens any artifact the loop produces: a match trace round by
   round, a batch, an evaluation, a training run, and a side-by-side of two runs.

## Principles

- **The engine stays the source of truth.** Agents only pick among `PlayerOptions`; a decision taken from
  the options is legal by construction. Learning code never re-implements a rule; when it needs one
  (expected damage, legal targets), it calls the domain services on snapshots.
- **Two axes of change, one stamp.** Every artifact (dataset, trace, evaluation, model, report) carries a
  `RunStamp`: engine version (git commit and dirty flag, injected at build), content hash, rule set, feature
  schema version, agent kinds and versions, base seed. A comparison is always between two stamped runs, and
  the report names which axis moved: content, engine, agent, or seeds.
- **Engine changes are detected, not assumed.** A benchmark digest (the outcomes of the benchmark seeds
  played by the deterministic baseline agents) is committed per content hash. CI recomputes it: an engine
  change that alters any outcome fails the build until the digest is regenerated on purpose, with a journal
  entry saying why. Bug fixes and new rules become visible events with a before and after.
- **.NET produces, Python learns.** The engine records datasets and hosts policies; training lives in a
  Python project with the usual tools. Models cross the boundary as small files the engine reads without an
  ML runtime (JSON weights first, ONNX later if needed).
- **Baselines before models.** A scripted greedy agent is cheap, explainable, and the first thing a model
  must beat. It also produces far better training data than random play.
- **Artifacts are JSON, the viewer is static.** Every artifact is a JSON file with its stamp; the viewer is
  one HTML file with plain JavaScript that opens them from disk. No server, no build step, no framework.
- **Small steps, one PR per phase**, tests alongside, docs updated, same working agreement as the engine.

## Vocabulary

The terms below are the authoritative entries of the "Learning" section of
[`domain/glossary.md`](domain/glossary.md); this table repeats them for reading convenience.

| Term | Definition |
| --- | --- |
| Run stamp | The identity of a run: engine version, content hash, rule set, feature schema version, agent kinds and versions, base seed. Present on every artifact. |
| Engine version | The git commit the engine was built from, plus a dirty flag, injected into the assemblies at build. |
| Observation | The numeric view of a match for one player at one decision: the feature vector built from their `PlayerBoardState`. |
| Action | One decision as the engine sees it: an evolution choice or pass, a speed, an intent, a target set. |
| Step | One (observation, options, action) taken by one player in one match. |
| Episode | One match from a player's point of view: its steps and its final return. |
| Return | The reward of an episode: win, loss, or draw, shaped by the remaining-health margin. |
| Policy | A function from observation and options to an action. `RandomAgent`, `GreedyAgent`, and learned agents are policies. |
| Dataset | Recorded steps and episodes, in JSON lines, with a manifest carrying the run stamp. |
| Match trace | The full record of one match: every domain event and the board after each, enough to replay it in the viewer without the engine. |
| Evaluation | A batch of mirrored matches between two agents on the benchmark seeds, reported as win rates with confidence intervals and a run stamp. |
| Benchmark seeds | The fixed seed set every evaluation uses, so two content versions, two engine versions, or two agents compare on the same matches. |
| Benchmark digest | The committed outcomes of the benchmark seeds played by the deterministic baselines, per content hash; CI verifies it. |
| Viewer | The static HTML page that renders traces, batches, evaluations, training runs, and comparisons. |

## Phases

### Phase L0. Decisions and vocabulary (done, docs only)

- ADR 0013: the learning stack (Python for training, JSON weights as the first exchange format, where the
  Python project lives, what CI runs for it, the static viewer).
- `docs/learning/features.md` created with the schema versioning rule: a feature list is immutable once
  published; a change (a new condition kind, a new stat) is a new version, and a model records the version
  it was trained on.
- The decisions listed at the end of this document are settled (all recommendations accepted).

### Phase L1. Run stamp, observations, action encoding (done, Application `Learning/`)

- `EngineVersion`: the git commit and dirty flag, read from the assembly's informational version. The
  `StampEngineVersion` target in `Directory.Build.props` sets it from `git rev-parse` and `git status` at
  build; without git the version is `unknown`.
- `RunStamp`: engine version, content hash from `IGameResources.Version`, rule set values (`RuleSetStamp`),
  feature schema version, both agent names, base seed. `DifferencesFrom` names the axes on which two stamps
  differ, so a comparison can refuse to mix more than one.
- `FeatureSchema` and `Observation`: the published layout `features:v1` (`docs/learning/features.md`), whose
  id adds a fingerprint of the concrete layout (content and rule set), and the fixed-length `float` vector
  `ObservationBuilder` fills from a `PlayerBoardState`. Board slots put own
  creatures first and enemies after, so the two players' vectors mirror each other; missing slots stay zero.
  The condition kinds are compared with the domain's `LastingEffect` subclasses by a test, so a new kind fails
  the build until a new schema version is published.
- `ActionEncoder`: the stable key and `ActionCode` of every decision, always naming the acting creature's board
  slot (`pass`, `evolve:<slot>:<spell>`, `speed:<slot>:<Quick|Standard>`, `intent:<slot>:<spell>`,
  `targets:<slot>:<spell>:<target slots>`), and `Candidates` listing every action a `PlayerOptions` offers.
- Tests pin the vector length and every index for the test content, the mirroring, the condition sums, the
  keys and codes, and the candidate enumeration including target combinations.

### Phase L2. Recording: datasets and match traces (done, Application `Learning/Recording`, `Learning/Tracing`)

- `RecordingAgent` wraps any `IPlayerAgent`: for every decision it records a `StepRecord` (observation, the
  candidate action keys, the chosen key and code) with match id, slot, round, sub-phase, and refuses a choice
  the options do not offer.
- `RunRecorder` writes a run: `manifest.json` (run stamp, schema id and feature names, counts),
  `steps.jsonl`, `episodes.jsonl`, and `traces/<match>.json` when a `MatchTraceRecorder` is attached. A match
  is closed by the runner once its final board is known (the return needs it), through `IMatchRecorder`,
  which `BatchRunner.RunAsync(scenario, recorder)` calls; `EpisodeRecord` and `Returns` compute
  `+1 / -1 / 0 + 0.1 x` health margin (decision C).
- `MatchTraceRecorder`, a domain event listener on `IMatchEvent` (every match event now names its match):
  every event of a match with both players' boards after the command that raised it.
- `IArtifactWriter` port (JSON documents and JSON lines); `FileArtifactWriter` and `ArtifactJson` in
  Infrastructure define the one JSON dialect every artifact uses (`docs/learning/artifacts.md`).
- CLI: `simulate --record <dir>` and `play --trace <file>`; every command prints the engine version, the
  content hash, the schema id, and the seed.
- Tests: a recorded match yields as many steps as decisions, every step's action is among its candidates,
  one episode per player whose returns sum to zero, a trace whose last entry carries the final board and the
  outcome, and the stamp on the manifest and every trace.

### Phase L3. Viewer (static HTML, `viewer/`) (done)

- `viewer/index.html` (plain JavaScript, Chart.js from a CDN) and `viewer/viewer.css`, opened from disk. It
  takes files by drag and drop, a file picker, or a directory picker, tells them apart by content, and
  renders by kind:
  - **Trace**: the match round by round, every event with both teams after it: health bars, energy, defense,
    initiative, conditions as chips, known spells, intents, the timeline with revealed and resolved actions,
    outcomes, fizzles and crits marked, the outcome. Step forward and backward (buttons or arrow keys), jump
    to a round.
  - **Batch**: from a run directory (`episodes.jsonl`, `steps.jsonl`, `manifest.json`, traces) or a
    `simulation.csv`: win rates with Wilson 95% intervals, draw rate, rounds histogram, remaining-health
    distributions, intents per spell, fizzle and crit rates when traces are included, the run stamp.
    Evaluations (L4) will be read the same way once their file exists.
  - **Training run** (`training.jsonl`, contract in `docs/learning/artifacts.md`): loss and evaluation win
    rate per iteration, best iteration marked.
  - **Comparison**: two artifacts of the same kind side by side with deltas, and the stamp diff at the top
    (what moved: content, engine, rules, schema, agents, seeds).
- Samples under `viewer/samples/` (a small run with one full trace, a CSV, a training file), generated to the
  documented shape. `ViewerSamplesTests` records a real run and compares key paths with the samples, so a
  change in what the engine writes fails the build until the samples follow. Real data comes from
  `simulate --record` and `play --trace`.

### Phase L4. Evaluation harness and benchmark digest (done)

- Agent registry: `AgentSpec` (a kind and an optional path, `random` or `kind:path`) parsed by the CLI's
  `--p1` and `--p2`, seated by `AgentFactory`. `AgentKind` holds `Random` until L5 and L7 add the others;
  `SimulationScenario` takes specs and an explicit seed list.
- Mirrored evaluation: `EvaluationRunner` plays each seed twice with the agents swapped, which cancels the
  Player1 first-mover bias the phase 7 tests showed.
- `EvaluationResult`: per agent, wins and win rate with a 95% interval, the mean score (win 1, draw one half,
  loss 0), average remaining health, intents per spell with their entropy (so a dominant spell shows), fizzle
  and crit rates (from `CombatStatsRecorder`, an event listener); shared: draw rate, average rounds, share of
  matches ending by the round cap; and every seed pair. The two mirrored matches of a seed are paired, not
  independent, so the interval is a normal interval over the per-pair means, never over the individual
  matches. Written as `evaluation.json` (stamped, read by the viewer) and printed as one table by
  `evaluate --p1 <agent> --p2 <agent> [--seeds file | --matches N --seed S]`.
- `benchmarks/benchmark-seeds.json` fixed in the repo (decision G, 200 seeds). `benchmark` plays them with
  the baseline agents and compares the outcomes with the committed `benchmarks/<content-hash>.json` (per
  seed and order: winner, reason, rounds, final health); CI fails on any difference or a missing digest, and
  `benchmark --write` regenerates it as a deliberate commit with a journal entry (`docs/learning/journal.md`,
  whose first entry is that first digest). This is the engine-change detector. The baseline is `Random` versus `Random` until `Greedy` lands in L5, which regenerates the
  digest once.
- Tests: paired intervals (perfectly correlated pairs widen the interval), entropy, seed pair scoring, the
  runner on the test content (two matches per seed, reports, combat rates, and the same digest twice), the
  digest comparison naming every changed, missing, or extra entry, the store's read and write, and the
  committed seed list.

### Phase L5. Baseline agents without a model (done, `docs/learning/agents.md`)

- `ActionScorer`: one-step lookahead using the domain rules on snapshots. For an action, resolve twice with
  `ResolutionRules.Resolve` (a forced crit and a forced miss) and weight the two scores by the actor's
  critical chance for the spell, so the expected damage includes the critical contribution; score damage
  dealt (capped at the target's health), kills, healing (capped at what was missing), stuns, bleeds as future
  damage, buffs, energy kept, and a risk penalty for a fizzle or dropped targets.
- `HeuristicAgent`: the lookahead with explicit weights (`damage`, `kill`, `heal`, `stun`, `bleed`, `buff`,
  `energy`, `risk`). Intent: the castable spell whose best target set scores best. Targets: the best set for
  the declared spell at reveal time. Speed: Quick when a kill is on the table, Standard otherwise. Evolution:
  the unlockable spell worth the most as if known and affordable; pass only when nothing is unlockable.
- `GreedyAgent`: the heuristic agent with the built-in weights (`learning/weights/greedy.json` holds the same
  values as a file). `AgentKind` has `Random`, `Greedy`, `Heuristic`; the weights file comes through the
  `IScoringWeightsSource` port (`JsonScoringWeightsSource` in Infrastructure).
- Both deterministic; tests pin their choices on small boards (a bleed over a hit, a kill over a bleed, the
  target it can kill, both targets of a stun, Quick only with a kill available, the most valuable unlock) and
  the greedy digest replays identically.
- The benchmark baseline moves from `Random` to `Greedy`, which regenerates the digest once (journal). Greedy
  versus Random on the benchmark seeds is the first evaluation number in the journal; the `Evaluate` workflow
  (`workflow_dispatch`) runs an evaluation on the CI runners and keeps `evaluation.json` as an artifact.

### Phase L6. Learning loop (Python, `learning/`)

- A `uv` project: `learning/pyproject.toml`, `ruff`, `pytest`, a CI job that runs them. Modules:
  `artifacts.py` (load manifests, steps, episodes, evaluations; refuse to mix run stamps unless asked),
  `features.py` (checks the schema version against the recorded one), `train_value.py`, `train_clone.py`,
  `search_weights.py`, `export.py`, and `report.py` (writes `training.jsonl` for the viewer).
- Three learners, in order of cost:
  1. **Weight search** for `HeuristicAgent`: cross-entropy method or a simple evolution strategy over the
     weights, evaluated by running the .NET CLI on the benchmark seeds. No dataset needed.
  2. **Behaviour cloning**: a classifier from observation to action key on greedy or self-play datasets.
  3. **Value regression**: predict the episode return from (observation, action); the agent picks the
     option with the highest predicted return. This is the legacy `RewardTrainer` idea on real features.
  Reinforcement learning proper (PPO self-play) is a later phase, only if the value agent plateaus.
- Exchange format: `policy.json` (run stamp of the training data, feature schema version, action keys,
  weight matrix or decision table). `PolicyAgent` in Application loads it, refuses a schema version it does
  not know, and scores options with a dot product; no ML runtime. ONNX and `Microsoft.ML.OnnxRuntime` get an
  ADR when a model needs it.
- Model files: small JSON committed under `models/<name>/<version>/` with their evaluation; datasets are
  git-ignored and regenerated from seeds.

### Phase L7. Iteration workflow

- `scripts/iterate.sh` (and the same as a documented sequence): rebuild content, check the benchmark digest,
  baseline random versus random on the benchmark seeds, greedy versus random, record a dataset with greedy
  self-play, train, evaluate the policy against greedy and random, write `runs/<run-id>/report.json` and
  compare it with the previous run (`--against <run-id>`).
- `docs/learning/journal.md`: one entry per change, content or engine, with both stamps, what changed, the
  numbers, and the decision (keep, tune, revert). This is where balance and engine knowledge accumulates.
- Balance signals the report always shows: Player1 versus Player2 win rate on mirrored play (near 50%),
  average rounds (a band we choose), spell usage entropy (no dominant spell), share of matches ending by
  round cap (low), fizzle rate.

## The loop, once L7 is done

```bash
dotnet run --project tools/DownfallArena.DataBuilder -- data data/dst            # 1. content -> hash
dotnet run --project src/DownfallArena.Cli -- benchmark                           # 2. engine unchanged? digest check
dotnet run --project src/DownfallArena.Cli -- simulate --p1 random --p2 random    # 3. balance baseline
dotnet run --project src/DownfallArena.Cli -- simulate --p1 greedy --p2 greedy --record runs/   # 4. data + traces
uv run --project learning train-value runs/<run-id> -o models/value/v3            # 5. train (+ training.jsonl)
dotnet run --project src/DownfallArena.Cli -- simulate --p1 policy:models/value/v3 --p2 greedy --against runs/<previous>  # 6. evaluate + compare
```

Then one journal entry, and any artifact dropped on the viewer. Changing a spell means running the lines
again on the new content hash; fixing a bug means running them on the new engine version with the same
content hash. The comparison report says which axis moved and what it did to the numbers.

A retrained policy on new content mixes two effects, the content and the training, so the comparison never
rests on it alone. The report always includes the fixed rows that move for one reason only: the deterministic
baselines (`Random`, `Greedy`) on both content versions, and the previous policy, frozen, on both versions
when its feature schema still applies (numeric edits keep the schema; adding a spell or a condition kind
changes it, and the report then says the frozen policy could not run and only the baselines compare). The
retrained policy is a third row, read against those two.

## What the content needs from us

The current spells are placeholders (three base attacks, one class node each). The loop works on them, but
agents only get interesting when decisions trade off: a costly burst versus a cheap poke, a heal versus an
attack, a stun on the enemy's best creature versus damage on the weakest. Phase L5 should land together with
one content pass that gives each class three or four spells with real differences (cost, targets, effects,
initiative), so the yardstick measures something. Content authoring itself stays a backlog item; the loop is
the deliverable here.

## Decisions (settled in ADR 0013)

| | Question | Recommendation |
| --- | --- | --- |
| A | Training language and tools | Python with `uv`, pandas, scikit-learn; PyTorch only when a phase needs it. ML.NET stays out: weaker tooling for exploration, and the engine should not carry a training dependency. |
| B | Model exchange format | JSON weights read by `PolicyAgent`, no runtime in .NET. ONNX later, by ADR, if a model needs more than linear scoring. |
| C | Return (reward) definition | Win `+1`, loss `-1`, draw `0`, plus `0.1 x` the remaining-health margin as a fraction of total health, so early advantages are visible and the signal is not only terminal. |
| D | Where the Python project lives | `learning/` in this repository with its own CI job (ruff, pytest). One clone, one history, the run stamp links both sides. |
| E | Dataset and artifact format | JSON lines for steps and episodes, JSON for traces, evaluations and reports, one manifest per run with the stamp; the legacy wide CSV is a projection Python can produce when a model wants it. |
| F | First learned agent | Weight search over `HeuristicAgent` (no dataset, no model runtime), then behaviour cloning, then value regression. Reinforcement learning only if the value agent plateaus. |
| G | Benchmark seeds | 200 seeds fixed in the repo, mirrored (400 matches per evaluation), refreshed only by a decision recorded in the journal. |
| H | Engine version stamp | The git commit (and dirty flag) injected into the assemblies' informational version at build, read by `RunStamp`; no manual version number to forget. |
| I | Engine-change detection | A committed benchmark digest per content hash, verified in CI, regenerated on purpose with a journal entry. |
| J | Viewer technology | One static HTML file with plain JavaScript and a CDN charting library, opened from disk; no server, no build, no framework. A Blazor or web UI stays a backlog item by ADR. |
