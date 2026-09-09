# Learning artifacts

What a recorded run leaves on disk (phase L2 of the [learning roadmap](../learning-roadmap.md), ADR 0013),
so that the viewer (L3) and the Python side (L6) read one format. Every file is JSON or JSON lines and carries
the run stamp, directly or through its manifest.

## Producing artifacts

```bash
dotnet run --project src/DownfallArena.Cli -- simulate --matches 200 --seed 1 --record runs/random-vs-random
dotnet run --project src/DownfallArena.Cli -- play --seed 1 --trace match.trace.json
```

`simulate --record <dir>` writes the dataset and one trace per match under `<dir>`; `play --trace <file>`
writes the trace of that one match. `runs/` is git-ignored: artifacts are outputs, not sources.

## Run directory

```
<dir>/
  manifest.json          run stamp, feature schema, counts
  steps.jsonl            one step per line
  episodes.jsonl         one episode per line (two per match)
  traces/<match-id>.json one trace per match
```

The manifest is written twice: when the run starts, with zero counts, so an interrupted run still says what
it was; and when it finishes, with the final counts. Starting a run empties `steps.jsonl` and `episodes.jsonl`,
and `simulate --record` refuses a directory that is not empty, so two runs never mix.

## JSON conventions

Defined once in `ArtifactJson` (Infrastructure) and shared by every file:

- camelCase property names, enums by name (`"Player1"`, `"Evolution"`, `"Quick"`).
- Identifiers as plain values: content ids as strings (`"spell:strike:v1"`), creature ids as numbers, match
  and player ids as UUID strings, round ids as numbers. A dictionary keyed by creature id uses the number as
  the key.
- Stats (health, energy, defense, initiative) as numbers, critical chance as a fraction.
- Closed hierarchies as objects with a leading `kind` holding the type name: effects (`Damage`, `Bleed`,
  `Stun`, `DefenseBuff`, `InitiativeDebuff`, ...), effect outcomes (`DamageOutcome`, `ConditionOutcome`, ...),
  and domain events (`RoundStarted`, `CombatActionResolved`, ...).
- The engine writes artifacts and never reads them back; the converters refuse to deserialize.

## `manifest.json`

| Field | Meaning |
| --- | --- |
| `stamp` | The run stamp: `engineVersion`, `contentHash`, `ruleSet` (team size, energy, picks, round cap, critical multiplier), `featureSchema` (the schema id), `player1Agent`, `player2Agent`, `baseSeed`. |
| `createdAt` | When the run started (UTC). |
| `schemaId`, `schemaVersion` | The feature schema of every step (`docs/learning/features.md`). |
| `featureNames` | The name of every index of an observation, so a reader needs no engine to label columns. |
| `matches`, `steps`, `episodes` | Counts, final once the run finished. |
| `traces` | Whether `traces/` was written. |

## `steps.jsonl`

One line per decision of one player (`RecordingAgent`):

| Field | Meaning |
| --- | --- |
| `matchId`, `slot` | Which match, which player. |
| `round`, `subPhase` | Where in the match the decision was taken. |
| `kind` | `Pass`, `Evolve`, `Speed`, `Intent`, or `Targets`. |
| `observation` | `schemaId` and `features`, the observation of the board at that decision. |
| `candidates` | The keys of every action the options offered, in candidate order. |
| `action` | The key of the chosen action, always one of `candidates`. |
| `code` | The numeric form of the chosen action: `kind`, `actingSlot`, `spellIndex`, `speed`, `targetMask`. |

The board is the one the driver handed to the agent: during the speed and intent sub-phases it is the same
board for every creature asked, so consecutive steps can share an observation.

## `episodes.jsonl`

One line per player per match, written once the match ended:

| Field | Meaning |
| --- | --- |
| `matchId`, `seed`, `slot` | Which match, its seed, which player. |
| `steps` | How many steps that player recorded in the match (zero when the agent was not wrapped). |
| `rounds` | The round the match ended in. |
| `outcome` | `winner` (a slot, or `null` for a draw) and `reason`. |
| `remainingHealth`, `enemyRemainingHealth` | The sums at the end of the match. |
| `return` | Win `+1`, loss `-1`, draw `0`, plus `0.1 x (remainingHealth - enemyRemainingHealth) / total maximum health` of both teams. The two returns of a match sum to zero. |

## `evaluation.json`

Written by `evaluate` (and computed by `benchmark`): two agents on a seed list, each seed played twice with
the agents swapped.

| Field | Meaning |
| --- | --- |
| `stamp` | The run stamp; `player1Agent` and `player2Agent` are agent A and agent B. When the seeds came from a file, `baseSeed` is the identity of that seed list (`SeedSets.IdentityOf`), the same on every run. |
| `agentA`, `agentB` | Per agent: `agent` (its spec), `wins`, `winRate` and `score` (`mean`, `low`, `high`: a 95% interval over the seed pairs; the score counts a win 1, a draw one half, a loss 0), `averageRemainingHealth`, `spellUsage` (intents per spell id), `spellEntropy` (bits), `actions`, `fizzles`, `criticals`, `fizzleRate`, `criticalRate`. |
| `matches`, `draws`, `drawRate`, `averageRounds`, `roundCapShare` | The shared numbers over every match. |
| `selfPlay` | True when both agents are the same spec. An agent is seeded from the match seed and the player slot, never from which agent it is, so the mirrored pass then replays the same matches: each agent report is the total of both players and the even split is arithmetic, not a measurement. The spell outcomes are unaffected. |
| `spellOutcomes` | Per spell, over the sides that declared it: `spell`, `intents` (casts), `sides` (one match seen from one player), `wins`, `losses`, `draws`, `score` (the share of those sides that won, a draw counting one half) and `intentsPerSide`. Best score first. One half is no signal: a spell both sides declare is on the winning and the losing side of every match. |
| `pairs[]` | Per seed: `seed`, `aFirst` and `bFirst` (the match result of each order: `outcome`, `rounds`, remaining health per slot), `scoreOfA`, `winsOfA`, `winsOfB`, `draws`, `remainingHealthOfA`, `remainingHealthOfB`. |

## `benchmarks/`

`benchmark-seeds.json` holds the fixed seed list (`{"seeds": [...]}`). `<content-hash>.json` is the benchmark
digest of that content: `contentHash`, `engineVersion` (informative), `agentA`, `agentB`, and `entries[]`
with, per seed and order (`AB`: agent A as player 1, `BA`: agent B as player 1), `winner` (`Player1`,
`Player2`, or `Draw`), `reason`, `rounds`, `player1Health`, `player2Health`. Plain values only, so CI reads it
back without the engine's converters. See `benchmarks/README.md` for the check and the regeneration.

## `training.jsonl` (written by the Python side, L6)

One line per training iteration, the contract the viewer reads (`docs/learning/training.md` says which
learner writes what):

| Field | Meaning |
| --- | --- |
| `iteration` | The iteration number, increasing. |
| `loss` | The training loss of that iteration. |
| `winRate` | The evaluation win rate of the model of that iteration against the baseline. |
| `winRateLow`, `winRateHigh` | The confidence interval of `winRate` (optional). |
| `matches` | How many evaluation matches produced `winRate` (optional). |
| `best` | `true` on the iteration the run keeps; without it the viewer marks the highest `winRate` (optional). |
| `stamp` | The run stamp (at least on the first line). |

A learner may add numeric fields of its own (`accuracy`, `r2`, `bestScore`, `sigma`); the viewer lists them.
A learner that was not evaluated against the engine writes no `winRate`, and the viewer then marks the best
iteration by the lowest loss.

## `policy.json` (written by the Python side, L6)

A trained policy: the run stamp of its data, the feature schema, the action keys, one weight row and one
bias per key, and a fallback score. A `value` policy also carries an optional `baseline` object, one
`weights` row over the features and one `bias`, whose value is added to every score (ADR 0016); a file
without it reads as a baseline of zero, so policies written before it still load. The fields and how a
reader scores candidates are in `docs/learning/training.md`; committed under `models/<name>/<version>/`.

## `report.json` (written by the Python side, L7)

The summary of one iteration under `runs/<run-id>/`: the run's stamp and, per evaluation of
`runs/<run-id>/evaluations/`, the agents, the matches, and the balance metrics (`winRateA` with its interval,
`scoreA`, `player1WinShare`, `drawRate`, `averageRounds`, `roundCapShare`, spell entropies, fizzle rates).
`docs/learning/training.md` says how it is built and compared.

## `traces/<match-id>.json`

The full record of one match (`MatchTraceRecorder`), what the viewer replays and what a bug report attaches:

| Field | Meaning |
| --- | --- |
| `matchId`, `seed`, `stamp` | The match, its seed, the run stamp. |
| `entries[]` | One per domain event, in order: `sequence`, `round`, `subPhase`, `event` (with its `kind`), `player1` and `player2` (each player's board state). |
| `outcome` | The outcome, from the last entry. |

Events are dispatched once the command that raised them is saved, so the boards of an entry are the boards
after that command: the entries of one command share them. Each board is the player's own view, hidden
intents included, since a trace is a debugging record rather than something a player sees.

## Reading artifacts

`viewer/index.html` opens any of these files from disk (see `viewer/README.md`): a run directory or a CSV as
a batch, a trace as a match to step through, `evaluation.json` as an evaluation, `training.jsonl` as a
training run, and two artifacts of the same kind as a comparison with the stamp diff.
