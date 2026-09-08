# Learning roadmap

Status: Proposed (2026-09-08). Companion to [`roadmap.md`](roadmap.md), which is complete: the engine plays
whole matches, the CLI simulates batches, and every result carries its seed and content hash. This document
is the plan for agents smarter than `RandomAgent`, and for the loop that lets us change the content (spells,
classes, talents) and measure what it does.

## Goal

Two things, in this order:

1. **A yardstick.** Agents that play well enough that "does this spell change anything" has an answer. A
   random agent cannot tell a good spell from a bad one; a greedy or learned agent can.
2. **A loop.** Edit content, rebuild, simulate, train, evaluate, read one report. Every step reproducible
   from a seed and a content hash, so a result from last week can be replayed today.

## Principles

- **The engine stays the source of truth.** Agents only pick among `PlayerOptions`; a decision taken from
  the options is legal by construction. Learning code never re-implements a rule; when it needs a rule
  (expected damage, legal targets), it calls the domain services on snapshots.
- **Everything is stamped.** Every dataset row and every evaluation carries the content hash, the seed, the
  feature schema version, and the agent version. Rows from different content are never mixed by accident.
- **.NET produces, Python learns.** The engine records datasets and hosts policies; training lives in a
  Python project with the usual tools (pandas, scikit-learn, later PyTorch). Models cross the boundary as
  small files the engine can read without an ML runtime (JSON weights first, ONNX later if needed).
- **Baselines before models.** A scripted greedy agent is cheap, explainable, and the first thing a model
  must beat. It also produces far better training data than random play.
- **Small steps, one PR per phase**, tests alongside, docs updated, same working agreement as the engine.

## Vocabulary (to add to the glossary in phase L0)

| Term | Definition |
| --- | --- |
| Observation | The numeric view of a match for one player at one decision: the feature vector built from their `PlayerBoardState`. |
| Action | One decision as the engine sees it: an evolution choice or pass, a speed, an intent, a target set. |
| Step | One (observation, options, action) taken by one player in one match. |
| Episode | One match from a player's point of view: its steps and its final return. |
| Return | The reward of an episode: win, loss, or draw, optionally shaped by the remaining-health margin. |
| Policy | A function from observation and options to an action. `RandomAgent`, `GreedyAgent`, and learned agents are policies. |
| Dataset | Recorded steps and episodes, in JSON lines, stamped with content hash, seeds, feature schema, and agent versions. |
| Evaluation | A batch of mirrored matches between two agents on a fixed seed set, reported as win rates with confidence intervals. |
| Benchmark seeds | The fixed seed set every evaluation uses, so two content versions or two agents compare on the same matches. |

## Phases

### Phase L0. Decisions and vocabulary (docs only)

- ADR 0013: the learning stack (Python for training, JSON weights as the first exchange format, where the
  Python project lives, what CI runs for it).
- Glossary entries above. `docs/learning/features.md` created empty with the schema versioning rule: a
  feature list is immutable once published; a change is a new version.
- Settle the decisions listed at the end of this document.

### Phase L1. Observations and action encoding (Application, `Learning/`)

- `Observation`: a fixed-length `float[]` plus a schema version, built from `PlayerBoardState` by a pure
  `ObservationBuilder`. Per own creature slot and per enemy slot: alive, health fraction, energy, stunned,
  defense bonus, initiative, bleed per round, one bit per known spell of the content, one bit per talent
  node unlocked. Global: round number over round cap, phase, sub-phase, timeline position, whether the
  opponent has revealed actions this round. Missing slots are zero-filled so team size can vary.
- `ActionEncoding`: a stable key per decision (`evolve:<spell>`, `pass`, `speed:<Quick|Standard>`,
  `intent:<spell>`, `targets:<slot indices>`) and a per-kind numeric encoding (target slots as a bitmask).
- Tests: same board gives the same vector; mirrored board (slots swapped) gives the mirrored vector; the
  vector length matches the published schema; every spell of the content has a stable index.

### Phase L2. Dataset recording

- `RecordingAgent` wraps any `IPlayerAgent`: for every decision it records the step (observation, the
  options' encoded action keys, the chosen action) with match id, slot, round, sub-phase. At `MatchEnded`
  the recorder closes the two episodes with the outcome and the return.
- `IDatasetWriter` port (JSON lines), Infrastructure adapter writing `steps.jsonl` and `episodes.jsonl`
  under `datasets/<content-hash>/<run-id>/` with a `manifest.json` (content hash, base seed, agent kinds and
  versions, feature schema version, rule set).
- CLI: `simulate --record <dir>`. Return = `+1` win, `-1` loss, `0` draw, plus `0.1 x` health margin
  fraction (decision C).
- Tests: a recorded match yields one episode per player with as many steps as decisions, returns sum to
  zero between the two players, stamps present.

### Phase L3. Evaluation harness

- Agent registry: `AgentKind` grows (`Random`, `Greedy`, `Policy`), the CLI gets `--p1 <kind[:path]>` and
  `--p2 <kind[:path]>`.
- Mirrored evaluation: each seed is played twice with the agents swapped, which cancels the Player1
  first-mover bias the phase 7 tests showed.
- `Evaluation` result: win rate of each agent with a Wilson confidence interval, draw rate, average rounds,
  average remaining health, spell usage counts (entropy, so a dominant spell shows). Written as
  `evaluation.json` and printed as one table. `benchmark-seeds.json` fixed in the repo.
- Tests on the statistics (intervals, mirroring, entropy) with hand-built results.

### Phase L4. Baseline agents without a model

- `GreedyAgent`: one-step lookahead using the domain rules on snapshots. Intent: for each castable spell
  and each legal target set, estimate the resolution (`ResolutionRules.Resolve` on a copy of the snapshots
  with a no-crit random) and score it: damage dealt, kills, healing, conditions applied, energy kept.
  Targets: the set with the best score for the declared spell. Evolution: the unlockable spell with the
  highest estimated value. Speed: Quick when a kill is on the table, Standard otherwise.
- `HeuristicAgent`: the same scoring with explicit weights (`damage`, `kill`, `heal`, `stun`, `energy`,
  `risk`) read from a JSON file, so the weights can be tuned by search in phase L5 without a model runtime.
- Both deterministic given the seed; tests pin their choices on small boards. Greedy versus random on the
  benchmark seeds is the first number in the journal.

### Phase L5. Learning loop (Python, `learning/`)

- A `uv` project: `learning/pyproject.toml`, `ruff`, `pytest`, a CI job that runs them. Modules:
  `dataset.py` (load manifests and JSON lines into pandas), `features.py` (checks the schema version
  against the recorded one), `train_value.py`, `train_clone.py`, `search_weights.py`, `export.py`.
- Three learners, in order of cost:
  1. **Weight search** for `HeuristicAgent`: cross-entropy method or a simple evolution strategy over the
     weights, evaluated by running the .NET CLI on the benchmark seeds. No dataset needed; first learned
     agent within a day.
  2. **Behaviour cloning**: a classifier from observation to action key on greedy or self-play datasets.
     Cheap, gives a policy that generalizes the greedy heuristics.
  3. **Value regression**: predict the episode return from (observation, action); the agent picks the
     option with the highest predicted return. This is the legacy `RewardTrainer` idea, done on real
     features.
  Reinforcement learning proper (PPO self-play) is a later phase, only if the value agent plateaus.
- Exchange format: `policy.json` (feature schema version, action keys, weight matrix or decision table).
  `PolicyAgent` in Application loads it and scores options with a dot product; no ML runtime. ONNX and
  `Microsoft.ML.OnnxRuntime` get an ADR when a model needs it.
- Model files: small JSON committed under `models/<name>/<version>/` with their evaluation; datasets are
  git-ignored and regenerated from seeds.

### Phase L6. Content iteration workflow

- `scripts/iterate.sh` (and the same as a documented sequence): rebuild content, baseline random versus
  random on the benchmark seeds, greedy versus random, record a dataset with greedy self-play, train, evaluate
  the policy against greedy and random, write `results/<content-hash>/report.md`.
- `docs/learning/journal.md`: one entry per content change with the content hash, what changed, the numbers,
  and the decision (keep, tune, revert). This is where balance knowledge accumulates.
- Balance signals the report always shows: Player1 versus Player2 win rate on mirrored play (should stay
  near 50%), average rounds (a band we choose), spell usage entropy (no dominant spell), share of matches
  ending by round cap (should be low), fizzle rate.

## The loop, once L6 is done

```bash
dotnet run --project tools/DownfallArena.DataBuilder -- data data/dst          # 1. content -> hash
dotnet run --project src/DownfallArena.Cli -- simulate --p1 random --p2 random  # 2. balance baseline
dotnet run --project src/DownfallArena.Cli -- simulate --p1 greedy --p2 greedy --record datasets/  # 3. data
uv run --project learning train-value datasets/<hash> -o models/value/v3        # 4. train
dotnet run --project src/DownfallArena.Cli -- simulate --p1 policy:models/value/v3 --p2 greedy       # 5. evaluate
```

Then one journal entry. Changing a spell means running the five lines again on the new hash and comparing
the two reports; a regression in the learned agent's win rate against the previous content's agent is the
signal that the content changed the game, not the agent.

## What the content needs from us

The current spells are placeholders (three base attacks, one class node each). The loop works on them, but
agents only get interesting when decisions trade off: a costly burst versus a cheap poke, a heal versus an
attack, a stun on the enemy's best creature versus damage on the weakest. Phase L4 should land together with
one content pass that gives each class three or four spells with real differences (cost, targets, effects,
initiative), so the yardstick measures something. Content authoring itself stays a backlog item; the loop is
the deliverable here.

## Decisions to settle before L1

| | Question | Recommendation |
| --- | --- | --- |
| A | Training language and tools | Python with `uv`, pandas, scikit-learn; PyTorch only when a phase needs it. ML.NET stays out: weaker tooling for exploration, and the engine should not carry a training dependency. |
| B | Model exchange format | JSON weights read by `PolicyAgent`, no runtime in .NET. ONNX later, by ADR, if a model needs more than linear scoring. |
| C | Return (reward) definition | Win `+1`, loss `-1`, draw `0`, plus `0.1 x` the remaining-health margin as a fraction of total health, so early advantages are visible and the signal is not only terminal. |
| D | Where the Python project lives | `learning/` in this repository with its own CI job (ruff, pytest). One clone, one history, the content hash links both sides. |
| E | Dataset format | JSON lines for steps and episodes plus a manifest; the legacy wide CSV is a projection Python can produce when a model wants it. |
| F | First learned agent | Weight search over `HeuristicAgent` (no dataset, no model runtime), then behaviour cloning, then value regression. Reinforcement learning only if the value agent plateaus. |
| G | Benchmark seeds | 200 seeds fixed in the repo, mirrored (400 matches per evaluation), refreshed only by a decision recorded in the journal. |
