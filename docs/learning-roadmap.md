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

### Phase L1. Run stamp, observations, action encoding (Application, `Learning/`)

- `RunStamp`: engine version read from the assembly's informational version (set from `git rev-parse` in
  `Directory.Build.props` and in CI), content hash from `IGameResources.Version`, rule set values, feature
  schema version, agent kinds and versions, base seed. Serializable, printed by every CLI command.
- `Observation`: a fixed-length `float[]` plus a schema version, built from `PlayerBoardState` by a pure
  `ObservationBuilder`. Per own creature slot and per enemy slot: alive, health fraction, energy, stunned,
  defense bonus, initiative, one value per condition kind of the closed taxonomy (ADR 0012: amount and
  remaining rounds), one bit per known spell of the content, one bit per talent node unlocked. Global: round
  number over round cap, phase, sub-phase, timeline position, revealed enemy actions this round. Missing
  slots are zero-filled so team size can vary. Adding a condition kind to the domain changes the observation
  length: that is a new schema version by construction, and a test fails until it is published.
- `ActionEncoding`: a stable key per decision that always names the acting creature by its team slot,
  since speed, intent, and targets are asked once per creature on the same board and two creatures can
  unlock the same spell: `evolve:<slot>:<spell>`, `pass`, `speed:<slot>:<Quick|Standard>`,
  `intent:<slot>:<spell>`, `targets:<slot>:<target slots>`; and a per-kind numeric encoding (the acting slot
  as an index, target slots as a bitmask).
- Tests: same board gives the same vector; mirrored board (slots swapped) gives the mirrored vector; the
  vector length matches the published schema; every spell and every condition kind of the content has a
  stable index.

### Phase L2. Recording: datasets and match traces

- `RecordingAgent` wraps any `IPlayerAgent`: for every decision it records the step (observation, the
  options' encoded action keys, the chosen action) with match id, slot, round, sub-phase. At `MatchEnded`
  the recorder closes the two episodes with the outcome and the return.
- `MatchTraceRecorder`, a domain event listener: every event of a match with the two board states after it,
  serialized as `trace.json`. This is what the viewer replays, and what a bug report attaches.
- `IArtifactWriter` port (JSON and JSON lines), Infrastructure adapter writing under
  `runs/<run-id>/` (`manifest.json` with the run stamp, `steps.jsonl`, `episodes.jsonl`, `traces/<match>.json`).
- CLI: `simulate --record <dir>` and `play --trace <file>`. Return = `+1` win, `-1` loss, `0` draw, plus
  `0.1 x` health margin fraction (decision C).
- Tests: a recorded match yields one episode per player with as many steps as decisions, returns sum to
  zero between the two players, the trace replays to the same final board, stamps present everywhere.

### Phase L3. Viewer (static HTML, `viewer/`)

- One `viewer/index.html` with plain JavaScript and a charting library from a CDN, opened from disk, that
  accepts a file (drag and drop or file picker) and renders it by kind:
  - **Trace**: the timeline of a match round by round: health bars per creature after each action, the
    revealed actions with their outcomes, fizzles and crits marked, conditions shown as chips, the outcome.
    Step forward and backward, jump to a round.
  - **Batch or evaluation**: win rates with their intervals, draw rate, rounds histogram, remaining-health
    distributions, spell usage, fizzle rate, the run stamp in the header.
  - **Training run** (`training.jsonl` written by the Python side): loss and evaluation win rate per
    iteration, best iteration marked.
  - **Comparison**: two artifacts of the same kind side by side with deltas, and the stamp diff at the top
    (what moved: content, engine, agent, seeds).
- No build step: the page is a single file; a second file holds the CSS. Tested by hand with the sample
  artifacts committed under `viewer/samples/`, which the tests of L2 regenerate.

### Phase L4. Evaluation harness and benchmark digest

- Agent registry: `AgentKind` grows (`Random`, `Greedy`, `Heuristic`, `Policy`), the CLI gets `--p1
  <kind[:path]>` and `--p2 <kind[:path]>`.
- Mirrored evaluation: each seed is played twice with the agents swapped, which cancels the Player1
  first-mover bias the phase 7 tests showed.
- `Evaluation` result: win rate of each agent with a confidence interval, draw rate, average rounds,
  average remaining health, spell usage counts (entropy, so a dominant spell shows), fizzle rate, share of
  matches ending by round cap. The two mirrored matches of a seed are paired, not independent, so the
  interval is computed over the seed pairs (each pair contributes the agent's mean score over its two
  matches; a bootstrap over pairs gives the interval), never over the individual matches. Written as
  `evaluation.json` (stamped) and printed as one table.
- `benchmark-seeds.json` fixed in the repo (decision G). `benchmark` command: plays the benchmark seeds with
  `Greedy` versus `Greedy` and writes `benchmarks/<content-hash>.json` (per seed: outcome, rounds, final
  health). CI runs it against the committed digest and fails on any difference; regenerating the digest is a
  deliberate commit with a journal entry. This is the engine-change detector.
- Tests on the statistics (paired intervals, mirroring, entropy) with hand-built results, including one
  where perfectly correlated pairs widen the interval; a test that the digest of the test content is stable
  across two runs.

### Phase L5. Baseline agents without a model

- `GreedyAgent`: one-step lookahead using the domain rules on snapshots. Intent: for each castable spell and
  each legal target set, resolve twice with `ResolutionRules.Resolve` on the snapshots (a forced crit and a
  forced non-crit random source) and weight the two outcomes by the actual critical chance of the actor and
  spell, so the expected damage includes the critical contribution; score the expectation: damage dealt,
  kills, healing, conditions applied, energy kept. Targets: the set with the best score for the declared
  spell. Evolution: the unlockable spell with the highest estimated value. Speed: Quick when a kill is on
  the table, Standard otherwise.
- `HeuristicAgent`: the same scoring with explicit weights (`damage`, `kill`, `heal`, `stun`, `energy`,
  `risk`) read from a JSON file, so the weights can be tuned by search in L6 without a model runtime.
- Both deterministic given the seed; tests pin their choices on small boards. Greedy versus random on the
  benchmark seeds is the first number in the journal, and the first benchmark digest is committed here.

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
