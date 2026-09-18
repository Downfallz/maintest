# Learning artifacts

What a recorded run leaves on disk (phase L2 of the [learning roadmap](../learning-roadmap.md), ADR 0013),
so that the viewer (L3) and the Python side (L6) read one format. Every file is JSON or JSON lines and carries
the run stamp, directly or through its manifest.

## Producing artifacts

```bash
dotnet run --project src/DownfallArena.Cli -- simulate --matches 200 --seed 1 --record runs/random-vs-random
dotnet run --project src/DownfallArena.Cli -- play --seed 1 --trace match.trace.json
dotnet run --project src/DownfallArena.Cli -- table --rules <file> --who mk   # a playtest session, into runs/playtest/<id>/
```

`simulate --record <dir>` writes the dataset and, by default, one trace per match under `<dir>`;
`--traces <n>` keeps only the first `n` of them and `--traces 0` keeps none, which is what a dataset large
enough to train on wants: a trace is about twenty times the disk of the steps from the same match and no
learner reads one. `play --trace <file>` writes the trace of that one match. `table` records the session two people played into
`runs/playtest/<session-id>/`, or under whatever `--record` names instead; `--who <initials>` puts them in the
stamp as `human:<initials>` so two sessions played by different people differ on the agents axis rather than
looking like the same player. `--no-record` writes nothing at all — no directory, no notes, no trace — for
trying a rule out at a table that should leave the disk as it found it; the page then offers no note buttons,
because a button that cannot keep what it was told is worse than no button. Recording is on by default the
rest of the time: a playtest nobody recorded teaches nothing, and the flag is the one thing anybody would
forget. `runs/` is git-ignored: artifacts are outputs, not sources.

## Run directory

```
<dir>/
  manifest.json          run stamp, feature schema, counts
  steps.jsonl            one step per line
  episodes.jsonl         one episode per line (two per match)
  traces/<match-id>.json one trace per match, up to what --traces allows
```

A playtest session (ADR 0054) is that directory exactly, plus two files a bot run does not need:

```
runs/playtest/<session-id>/
  ... the four above, for the one match that was played ...
  notes.jsonl            one note per line: how long a decision took, a refusal, a lookup, a misplay, a comment
  catalogue.json         the card faces the match was played with
```

Both exist because a human session cannot be regenerated. A bot run is a seed and a content hash, so it is
replayed rather than kept; a session two people played once is not, and a trace read a year later carries
Spell **ids** against a catalogue that has been tuned twenty times since — `spell:pummel:v1` would be a string
whose cost and effects are gone, or silently different. `catalogue.json` is written once when the session
opens, so the hash says *which* content and the file says *what it was*. The viewer ignores both today and
opens the directory as a Batch and its trace as a Match, with no change to it.

A session's trace is rewritten after every accepted decision, so a table abandoned at Round 9 leaves a
readable partial trace. Its manifest is only rewritten with the final counts when the match reaches an
outcome: a session nobody finished keeps the zero-count manifest it was opened with, which is how an
abandoned session says so rather than reading like a finished one.

**An abandoned session keeps its notes and its trace and loses its dataset.** Steps are held in memory until
the match is closed, so `steps.jsonl` and `episodes.jsonl` of an interrupted session are empty. Nine rounds of
trace beside an empty `steps.jsonl` is that, and not a bug — a human session that was not played to the end
teaches the learning pipeline nothing, and what it still has to say is in `notes.jsonl`.

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
| `candidateTermNames` | The name of every index of a step's `candidateTerms`, the scoring weight names in the engine's order ([ADR 0051](../adr/0051-the-policy-sees-what-the-heuristic-sees.md)). Empty in a run recorded before them, and every step of such a run carries no `candidateTerms`. |
| `matches`, `steps`, `episodes` | Counts, final once the run finished. |
| `traces` | Whether `traces/` was written at all. It does not say how many: a capped run holds a sample, and a reader that counts anything over them (the viewer's fizzle and crit tiles) prints the denominator it actually had. The rates an evaluation reports are counted over every match and never over traces. |

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
| `candidateTerms` | One number list per candidate, in candidate order, indexed like the manifest's `candidateTermNames`: the scorer's terms of that action, what the heuristic weighs before it decides (ADR 0051). An intent carries the terms of the target set the built-in weights would bind for it; a target set its own; an unlock its combat estimate, the initiative it buys and the cost it cannot cover; a speed choice and a pass all zeros. Absent from a run whose manifest names no terms. |
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
| `selfPlay` | True when both agents are the same spec. An agent is seeded from the match seed and the player slot, never from which agent it is, so the mirrored pass then replays the same matches: each agent report is the total of both players and the even split is arithmetic, not a measurement. `spellOutcomes` counts that replay once, so its `sides` stay independent. |
| `spellOutcomes` | Per spell, what it correlates with and what it did. Chosen: `spell`, `intents` (declarations), `sides` (one match seen from one player), `wins`, `losses`, `draws`, `score` (the share of those sides that won, a draw counting one half) and `intentsPerSide`. Done: `resolved` and `fizzled` (declarations that reached resolution and landed, or did not), `resolveRate`, `criticals`, `damage`, `healing`, `energy`, `energyDrained` (kept apart from `energy` rather than netted against it: one field for both would make a spell that gives two and one that takes two read alike), `damagePerCast`, `hits` (the targets its casts actually landed damage on) and `damagePerTarget`, the applications `stuns`, `bleeds`, `regens`, `energyRegenerations`, `defenseBuffs`, `defenseDebuffs`, `initiativeBuffs`, `initiativeDebuffs`, and what those conditions went on to do at upkeep: `conditionDamage`, `conditionHealing` and `conditionEnergy`, counted against the spell because a condition remembers the cast it came from ([ADR 0027](../adr/0027-a-condition-remembers-the-spell-that-applied-it.md)). `damagePerCast` reads both halves, so a spell whose point is its bleed is not read as the garnish alone. `resolvedWhenWon` and `castShareWhenWon` weigh the correlation by casts rather than by side; that share is read against the run's own baseline, above one half, not against one half. Best score first. One half is no signal: a spell both sides declare is on the winning and the losing side of every match. The tuner reads `resolved` for `spellUsageShare`, `spellsNeverCast` and `spellsBarelyCast` (ADR 0021), and `damagePerTarget` for `tierDamageSpread` ([ADR 0043](../adr/0043-a-control-spell-is-not-an-attack-and-reach-is-not-force.md)): `hits` is the reach a cast found and never the `maxTargets` it is allowed, because a sweep finds fewer creatures as they die and an exploring agent may pick a smaller legal set. |
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
without it reads as a baseline of zero, so policies written before it still load. A policy trained on a run
that records `candidateTerms` also carries `candidateTermNames` and `candidateWeights`, one weight per term
shared by every key, added to a candidate's score on its own terms (ADR 0051); a file without them scores
the keys alone, and a file naming terms in another order than the engine's is refused. The fields and how a
reader scores candidates are in `docs/learning/training.md`; committed under `models/<name>/<version>/`.

## `report.json` (written by the Python side, L7)

The summary of one iteration under `runs/<run-id>/`: the run's stamp and, per evaluation of
`runs/<run-id>/evaluations/`, the agents, the matches, and the balance metrics (`winRateA` with its interval,
`scoreA`, `player1WinShare`, `drawRate`, `averageRounds`, `roundCapShare`, spell entropies, fizzle rates).
`docs/learning/training.md` says how it is built and compared.

## `notes.jsonl` (a playtest session only)

One line per note, written through `IArtifactWriter.AppendJsonLinesAsync` and timed with `TimeProvider`.

| Field | Meaning |
| --- | --- |
| `sessionId`, `matchId`, `slot` | Which session, which match, which seat. |
| `round`, `subPhase` | Where in the match, as in `steps.jsonl`. |
| `at` | When, in UTC. |
| `kind` | `Decision`, `Refused`, `Lookup`, `Misplay` or `Comment`. |
| `elapsedMs` | For a `Decision`: from the moment that seat's options were **served** to the moment the decision was accepted. Served, not asked: the engine may ask a seat while nobody is looking at the screen, and the difference is the walk back to the table. |
| `code`, `message` | For a `Refused`: the `DomainError` the aggregate or the host's pre-check returned. |
| `text` | For a `Lookup`, a `Misplay` or a `Comment`: what the player typed. A tap carries none. |

`Decision` and `Refused` are the host's own account of what happened and it refuses to accept either from a
client; the other three come from the page, for the seat whose token posted them. No identifier is added to a
step to tie the two files together: alignment is by order, because both are appended in the order that seat
decided.

That alignment holds **for a seat a person played throughout, and only for such a seat.** A `Decision` note is
written when a person's tap is accepted, while a step is recorded for whoever was seated — so `table --p2
greedy` gives player 2 steps and no notes at all, and `table --handover 10` gives player 1 nine rounds of steps
before its first note. Join the two files only for a seat the run stamp says a person held from the start.

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
