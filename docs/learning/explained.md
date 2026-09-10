# The learning loop, explained simply

A plain-words tour of learning phases L6 and L7 for someone who has never trained a model. The precise
version is in [training.md](training.md); this page says what the pieces are for and how to read them.

## The idea in one paragraph

The engine can play thousands of matches between two bots and write down everything it saw. The Python side
reads those notes and tries to produce a better bot. The engine then plays the new bot against the old ones
and reports whether it is better, and by how much. Every result carries a stamp saying which engine, which
content, and which agents produced it, so two results are only ever compared when exactly one thing differs
between them. That is the whole loop: play, learn, play again, compare, write it down.

## The words

Start here: what a "match" needs to be reproducible, and the four things you can ask the engine to do with
one.

- **Seed**: one whole number that fully decides a match's randomness (who crits, which random agent picks
  what). Same seed, same rules, same content, same agents → the exact same match, replayed identically. This
  is what makes any of the rest possible: without seeds, "the same match" would not mean anything.
- **Exploring**: `explore:<rate>` plays the greedy move except for that share of decisions, taken at random.
  It exists for one reason: a bot that always plays its best move never shows what the other moves would have
  given, and a learner that has to compare moves cannot learn that from games where the comparison never
  happened. Recording with `--explore 0.2` mixes in enough other moves for the comparison to exist. It is a
  recording tool, never a baseline, since it is only reproducible for a given seed.
- **Simulate**: play a batch of matches (`simulate --matches 200 --seed 1`, match *i* uses seed `1 + i`) and
  print a one-line summary (win rates, average rounds). Nothing is written to disk unless you ask.
- **Record**: `simulate --record <dir>` does the same, and *also* writes every decision and every match's
  outcome to files under `<dir>` (a **dataset**: `manifest.json`, `steps.jsonl`, `episodes.jsonl`). This is
  the "notes" the Python side trains on. You only need to record when you plan to train something; a plain
  balance check does not need a dataset.
- **Evaluate**: play two named agents against each other and report win rates with confidence intervals
  (`evaluate --p1 greedy --p2 random`). Every seed is played *twice*, once with each agent as player 1
  ("mirrored"), which cancels out any first-move advantage from the comparison. Give it `--seeds <file>` to
  use a fixed list of seeds instead of `--matches`/`--seed`.
- **Benchmark seeds**: one fixed list of 200 seed numbers, committed in the repo
  (`benchmarks/benchmark-seeds.json`), so that "the same 200 matches" means the same thing on every machine,
  every day, forever. They are just an input file to `evaluate` — see below for who plays them.
- **Digest**: the recorded *outcome* of the 200 benchmark seeds (who won each one, how many rounds, and so
  on) played by the two baseline bots (`greedy` against `greedy`), for one specific content hash. One digest
  file per content hash, committed in the repo (`benchmarks/<content-hash>.json`). This is the engine's
  fingerprint on that content: two bots with no randomness in their decisions should replay it identically
  forever, unless the engine's rules or that content changed.
- **Benchmark (the command)**: `benchmark` re-plays the 200 seeds with the two baseline bots on the content
  you have right now, and compares the result to the committed digest for that content hash. Same result →
  print "verified" and exit 0. No digest exists yet for this content hash → print the digest it just computed
  and exit 1 ("this is new, go commit it on purpose"). A different result than the committed digest → exit 1
  and list what changed (someone changed engine behaviour, or content, without meaning to). This is the
  **engine-change detector**: it is not there to test your changes, it is there to catch a change you did
  *not* intend.

### The chicken and the egg: where does the first digest come from?

There is no digest until someone commits one — nothing computes it for you automatically, and `benchmark`
without a digest to compare to is *expected* to fail the first time. That is not a bug, it is the mechanism:

1. You run `benchmark` (or it runs in CI). No file exists yet for this content hash, so it prints the digest
   it just computed to your screen and exits with an error.
2. You look at the printed numbers once, to make sure they are sane (not all games ending in round 1, no
   crash), then run `benchmark --write`, which saves exactly that digest to
   `benchmarks/<content-hash>.json`.
3. You commit that file, with a line in `docs/learning/journal.md` saying why (new content, first run,
   whatever it is).
4. From now on, `benchmark` compares against *that* file. If the content hash never changes again, the digest
   never needs to change again either — it is a "nothing moved" tripwire, checked on every pull request.

So the egg (the digest) always comes from a chicken (a `benchmark --write` a human ran on purpose, once, and
committed). Nothing lays it by itself; that is the whole point — an *automatic* digest would defeat the
purpose of catching accidental changes.

- **Observation**: the board turned into a list of numbers (who is alive, how much health, what spells are
  known, ...), always in the same order. A model only ever sees these numbers. The order is the **feature
  schema**, and it has a version so a model is never fed numbers laid out differently from what it learned on.
- **Action key**: a short text for one possible decision, such as `intent:1:spell:rend:v1` (creature in slot 1
  declares Rend) or `speed:0:Quick`. A model scores keys; the best-scoring legal one is played.
- **Dataset**: what `simulate --record` writes: for every decision of a recorded match, the observation, the
  legal keys, the key taken, and later the **return** of that player in that match (+1 for a win, -1 for a
  loss, a little more or less depending on the health margin). This is the thing a model actually trains on.
- **Baseline**: how good the position is, before you consider what to play. The learner fits it on every
  recorded decision at once, then asks each action only "how much better or worse than that?" — because a
  match's result is credited to all hundred and fifty of its decisions, so on its own it mostly tells you
  about the position, not about the move (ADR 0016).
- **Policy**: the trained model. Here a policy is a table: one row of numbers per action key. The score of a
  key is its row multiplied with the observation, plus a bias. Nothing fancier, on purpose: the engine reads
  it back with a loop of multiplications and no machine-learning library.
- **Run stamp**: the label on every result: engine version, content hash, rule set, feature schema, both
  agents, and the seed set. If two stamps differ on more than one axis, the comparison is not meaningful.

### How the four commands relate

|  | Plays matches? | Writes a dataset (steps/episodes)? | Compares two named agents? | Checks against a committed answer? |
| --- | --- | --- | --- | --- |
| `simulate` | yes | only with `--record` | no (one agent spec per side, no ranking) | no |
| `evaluate` | yes | no | yes, with confidence intervals | no |
| `benchmark` | yes (the 200 fixed seeds) | no | no (always greedy vs greedy) | yes, against the committed digest |
| `evaluate-policy` (Python) | asks the engine to `evaluate` for it | no | yes (a trained policy vs a baseline) | no, but it saves the win rate |

`benchmark` is really just `evaluate --p1 greedy --p2 greedy --seeds benchmarks/benchmark-seeds.json`, plus
the save/compare step against a committed file. Everything else in the loop (`search-weights`,
`evaluate-policy`, `scripts/iterate.sh`) is built out of `simulate`, `evaluate`, and `benchmark`, called with
different agents.

## Phase L6: learning from the notes

Three ways to make a better bot, from the cheapest to the most ambitious.

1. **Weight search** (`search-weights`). The greedy bot's scoring has eight knobs (damage, kill, heal, stun,
   bleed, buff, energy, risk). The search turns the knobs at random around the current setting, has the engine
   play each setting against a fixed opponent on the benchmark seeds, keeps the settings that won the most,
   and centres the next round of random turns on those. Repeat ten times. No notes are needed, only the
   engine; it is slow (one engine run per setting) but simple and robust. The result is a weights file the
   `heuristic:<file>` agent plays directly.
2. **Behaviour cloning** (`train-clone`). Take the notes of a bot playing itself and teach a model to guess
   the key that bot took from the observation alone, like learning chess by predicting a master's moves. The
   clone can never be better than what it copies, but it plays much faster than the lookahead and it is the
   first model to cross the Python-to-engine boundary, which is what this phase proves.
3. **Value regression** (`train-value`). Instead of copying, predict the outcome: from an observation and a
   key, guess the return the player ended up with. The agent then takes the key with the best guessed return.
   This can, in principle, find moves the greedy bot never took.

What comes out, in every case, is a small folder: `policy.json` (or a weights file), `training.jsonl` (one
line per training step, so the viewer can draw the learning curve), and metrics. The learners hold back one
match in five to check themselves on matches they never saw; the accuracy they report is on those.

Reading the numbers: **accuracy** is how often the model's best key is the one the data took (only meaningful
for cloning). **r2** says how much of the return the value model explains (1 is perfect, 0 is no better than
guessing the average). **winRate** is the real test, added in L7: what the model wins against a baseline.

## Phase L7: the loop

`scripts/iterate.sh` is nothing new by itself — it just runs the four commands from the section above, in
order, with specific agents, and saves everything so the numbers can be compared to the next run. Think of
it as one script that does "a day's worth of manual typing" for you and leaves a paper trail.

`scripts/iterate.sh` runs one full turn of the loop and leaves everything under `runs/<run-id>/`:

1. **Build** the engine and the content (`dotnet build`, the data builder). This is the "make sure what I'm
   about to measure actually reflects my latest edits" step.
2. **Check the digest** (`benchmark`): did I accidentally change engine or content behaviour? If this fails
   because there is genuinely no digest yet for new content, see "the chicken and the egg" above — run
   `benchmark --write` once and re-run the loop.
3. Play the **baselines** (three `evaluate` calls, all on the fixed benchmark seeds so they are comparable
   run after run):
   - `random` vs `random` — is the game balanced between player 1 and player 2 with no skill involved at all?
   - `greedy` vs `greedy` — same question, but with a strong deterministic bot, which is a harder test of the
     rules than two random bots.
   - `greedy` vs `random` — how big is the skill gap a trained bot needs to beat to be worth anything?
4. **Record** a dataset (`simulate --record`) of `greedy` playing itself. This produces the notes ("what did a
   decent bot do, and how did each match turn out") that step 5 trains on.
5. **Train** (Python `train-value` and `train-clone`) two policies from that dataset.
6. **Evaluate** each trained policy (Python `evaluate-policy`, which itself calls the engine's `evaluate`)
   against `greedy` and against `random`, on the benchmark seeds. The win rate is saved into the policy's own
   `training.jsonl`, so the viewer can plot it next to the training curve.
7. If a previous run is named (`--against <run-id>`), **replay that older run's policy** on today's engine
   and content (again, just `evaluate-policy` on an old model directory). This gives one row in the report
   where *only* the engine or the content changed — never the training — because it is the exact same trained
   numbers, just asked to play again. When the content change added or removed a spell (a new feature schema,
   see the worked cases below), the old policy literally cannot run on the new layout, and the report says so
   instead of a bogus number.
8. Write **`report.json`** (Python `report`) and print the table: per evaluation, the win rate with its
   uncertainty, the player 1 share, draws, rounds, the share of matches ending by the round cap, the spell
   entropy (how many different spells were cast: low means one dominant spell) and the fizzle rate. With
   `--against`, a second table of deltas and the stamp axis that moved.

Then one entry in [journal.md](journal.md): what changed, the numbers, the decision (keep, tune, revert).

### Seeing the results without uploading anything

The last step also writes `runs/<id>/report.html`: the viewer page with this run's report, evaluations, and
training curves already inside it. Open that one file in a browser and it starts on the report; nothing to
drag, nothing to pick. Drop another run's `report.json` on it to get the deltas side by side. (`--open` asks
the script to launch the browser itself; that works on a desktop, not inside a container.)

### Getting a report without running anything locally

The whole turn also runs on GitHub Actions, in **Actions -> Learning loop**. It starts by itself when `src/`,
`data/`, `learning/`, `benchmarks/` or the script change on `main`, and once a week on Monday morning; "Run
workflow" starts one on demand with the same knobs as the script (matches, explore, seed, and the two value
flags), so you never need a .NET SDK or `uv` on your machine to get numbers.

The third way to ask for one is to commit the question. `learning/experiments/next.json` holds the knobs for
the next run, plus a `why` line saying what it is meant to answer; a pull request that changes that file runs
the loop with those settings and prints the `why` above the report. GitHub matches that filter against the
pull request's whole diff, so once the file is in the diff every later push starts a run too; the newest one
cancels the one before it, and the answer you want is the one on the last commit. So an experiment is a commit, its
parameters are in git next to the numbers they produced, and anyone who can push can ask for a run without
touching the Actions form. A field typed into that form still wins for that one run; a field left empty falls
back to the file. `learning/experiments/README.md` lists the keys.

What a finished run gives you:

- the **summary of the run** (the page GitHub opens): the policy evaluations and the whole report table, no
  download;
- the **artifact** `run-<number>`: `report.html` (open it, it is the viewer with the run inside),
  `report.json`, the evaluations, and the two trained policies.

The recorded datasets are not in the artifact on purpose: they are hundreds of megabytes and the seed
reproduces them exactly. The runner starts empty every time, so a CI run has no previous run to compare
against; the deltas are still a local `--against`, or a comparison of two `report.json` in the viewer.

### The knobs

Everything you may want to change lives on `scripts/iterate.sh`, as a flag with a default; nothing needs
editing in a file, and `scripts/iterate.sh --help` repeats this list. In plain words:

| Flag | Default | What it does | When to touch it |
| --- | --- | --- | --- |
| `--matches` | 200 | how many greedy self-play matches are recorded as the dataset | first thing to raise when a policy learns something odd from too few examples of a rare move |
| `--seed` | 1 | which matches get recorded (the base seed) | to record different matches with the same size |
| `--explore` | none | also records a second dataset where that share of decisions is taken at random, and trains the value policy on it (ADR 0014) | whenever you train a value policy; greedy's own games never show what another move would have given. Doubles the recording time |
| `--value-alpha` | 1.0 | how strongly the value model's fit is pulled toward "no effect". A model with 383 numbers per action and few examples can fit noise; a higher pull makes it more cautious | raise (10, 100) when the value policy trusts a handful of examples too much (a huge weight on one action) |
| `--value-min-samples` | 5 | how many examples an action needs before it gets its own fit; below that it keeps the dataset's average return | raise (50) so a rare move cannot be scored on almost nothing |
| `--clone-epochs` | 20 | how many passes the clone makes over the dataset; the best pass on held-out matches is kept | raise if the accuracy is still climbing at the last pass |
| `--clone-alpha` | 0.0001 | the same pull toward "no effect", for the clone's classifier | rarely; raise if the clone is worse on held-out matches than on the ones it saw |
| `--validation` | 0.2 | the share of matches held out to check the models on matches they never saw | leave it; lower only when the dataset is tiny |
| `--against` | none | the previous run to compare with; its value policy is replayed on this content | every run after the first |

"Alpha" is the usual name for the pull toward zero (regularization) in these models; the two learners have
their own because they are different models. `--min-samples` only exists for the value learner because it
fits one small model per action.

The first real run (journal, 2026-09-09) is the worked example: the value policy lost every match to greedy
because one rare action got its own fit on almost no data. The fix is those flags, not code:
`scripts/iterate.sh --run second --against premier --matches 1000 --value-min-samples 50 --value-alpha 10`.

## How to read a report

- **Player 1 share far from 50% with identical agents**: the rules favour the first mover; a rules question,
  not a training one.
- **Spell entropy near zero**: one spell dominates; the content offers no real choice, so the models have
  little to learn.
- **Round cap share climbing**: matches drag; damage or costs are off.
- **A policy below random**: the training is broken, or the data was too small; check `r2` and `accuracy`.
- **A policy above greedy**: a real gain; the first target of the loop.

## Worked cases: what to do when you change something

Every case below follows the same shape: make the change, rebuild the content, let the engine-change detector
say whether behaviour moved, run one turn of the loop against the previous run, read the deltas on the one
axis that moved, write the journal entry. What differs is which files move and whether the models survive.

Two facts drive everything:

- **The content hash** is the SHA-256 of the consolidated content. Any edit under `data/`, however small,
  gives a new hash, so a new benchmark digest file (`benchmark --write`) and a journal entry are always part
  of a content change. The old digest stays in git: it is the record of the old content.
- **The feature schema id** (`features:v1+<fingerprint>`) hashes the feature names, which include one bit
  per spell id and per talent node, and the round cap. A numeric edit (a cost, a damage amount, a duration)
  keeps the id, so trained policies still run and the comparison can include them. Adding or removing a
  spell or a node changes the id, so policies trained before cannot run on the new content and must be
  retrained; only the baselines compare across that change.

### I want to change a spell's casting cost (or damage, duration, initiative)

The cheapest change: numbers only, same schema, every model still runs.

1. Edit the number in `data/Spells/<class>/<spell>.v1.json` (`energyCost`, an effect's `amount`, ...).
2. `scripts/iterate.sh --run cost-heavy-strike-1 --against <previous-run>`. The benchmark step fails: the
   digest of the new content hash does not exist yet. That is expected. Run
   `dotnet run --project src/DownfallArena.Cli -- benchmark --write` once, read the printed table (this is
   your first number: Greedy against Greedy on the new content), then run the iteration again.
3. In the report, read `greedy-vs-greedy`, `greedy-vs-random` and `previous-value-vs-greedy` first: same
   agents on both sides, only the content moved, so their deltas are the effect of the cost change. Did the
   spell's share of intents change (`spellEntropy`)? Did rounds get longer or shorter? Did the player 1 share
   move? The retrained policy row says whether a bot can still exploit what is left.
4. Journal entry: the two content hashes, the deltas, the decision (keep, tune more, revert). Commit the new
   digest, the content, the journal, and the model you keep.

### I want to add a spell

A new spell id adds a feature bit, so the schema id changes: the baselines compare, the old models do not.

1. Write `data/Spells/<class>/<name>.v1.json` (copy `heavy_strike.v1.json`: id `spell:<name>:v1`, type,
   class, initiative, cost, crit chance, targeting, effects among the kinds of `data/README.md`). Add the
   alias `"spell:<name>": "spell:<name>:v1"` to `data/aliases.json`. Make it reachable: as a starting spell of
   a creature (`startingSpellIds`) or as a node spell in `data/TalentTrees/talent_tree.v1.json` with its
   prerequisites, otherwise no creature ever knows it and nothing changes.
2. `dotnet run --project tools/DownfallArena.DataBuilder -- data data/dst` tells you about a typo, an unknown
   effect kind, or a broken reference before anything runs.
3. `benchmark --write` for the new content hash, then `scripts/iterate.sh --run add-<name>-1 --against
   <previous-run>`. The script tries to replay the previous policy, the engine refuses it (schema id
   changed), and the report says so: only the baselines compare, plus the freshly trained policies flagged as
   retrained.
4. Read: does the spell get cast at all (`spellUsage` in the evaluation files, entropy in the report)? A spell
   greedy never picks is either weak or scores badly under the lookahead; check `docs/learning/agents.md` for
   what the scorer values. Does `player1WinShare` move? A spell that rewards going first widens it.
5. Journal entry, commit content, alias, tree, digest, journal, and `docs/learning/features.md` stays as it
   is: the version is unchanged, the fingerprint did what it is for.

### I want to add a creature (or a creature class)

A creature definition is `data/Creatures/<name>.v1.json` (stats, class, talent tree, starting spells). A new
class also needs a value in `CreatureClass` in the domain and its spells declare it in `creatureClass`.

Today the CLI seats the **first** creature of the content for every slot of both teams (`GameSession.Roster`),
so a second creature file changes the content hash and nothing else. Until a roster option exists
(`--roster` on the CLI and the scenario), test a new creature by making it the one the CLI picks, or by
editing the existing definition. Adding a creature changes no feature bit; a new talent node does.

When the roster option lands, the loop is the same as for a spell: new hash, `benchmark --write`, iterate,
read the mirrored rows first.

### I want to add a condition to the engine, and a spell that applies it

The most expensive change, on purpose (ADR 0012: effects are a closed set, so nothing half-implemented can
ship). A new condition kind is a new observation layout: **a new feature schema version**, and every model
trained before is retired.

1. Domain: a sealed record under `Domain/Resources/Effects` with a validated factory (`Bleed.cs` is the
   model), and the rules that apply and tick it (`Rules/Combat`, `Rules/Rounds`; `BleedTick` and
   `UpkeepRules` show the pattern). Tests in `Domain.Tests` for every rule it touches.
2. Content: the `kind` string in `Infrastructure/Resources/GameSchemaMapper`, a row in `data/README.md`, and
   the spell that applies it under `data/Spells/`.
3. Learning encodings: `FeatureSchema.ConditionKinds` and `ObservationBuilder` (the two features per kind),
   and the version bump to the next `features:vN` with its section in `docs/learning/features.md`, since rule
   2 there says a new kind is a new version. `FeatureSchemaTests` fails until the kind is listed, which is the
   reminder. Then every fixture carrying the old version string: the policy files under
   `tests/`, the recorder's manifest assertion, and the samples under `viewer/samples/`.
4. Agents: what the lookahead thinks the condition is worth, in `ActionScorer` and a `ScoringWeights` weight
   if none fits (then `learning/weights/greedy.json`, `JsonScoringWeightsSource`, `docs/learning/agents.md`).
5. **Everything that counts or shows it.** This is the step that has been missed three times, and each time
   the effect worked and read as doing nothing:
   - `CombatStatsRecorder` and `SpellEffects`/`SpellOutcome`: its own counter, never folded into another
     kind's. Two kinds sharing a number cannot be read apart later.
   - `EvaluationConsole`: its own column. That table is what journal entries are written from, so a kind
     missing from it is a kind that does not exist as far as any conclusion goes.
   - `ConsoleMatchLog`: a round where only the new effect ticked must not print nothing.
   - `viewer/index.html`: the trace describer, `conditionSlot`, the two spell tables, and the run comparison.
     `ViewerSpellOutcomeContractTests` catches a rename here but not a column nobody added.
   - `studio/studio.js`: the effect form's field spec, and the content audit's table.
   - `ContentAudit`/`SpellReach` if the effect adds to a total the audit reasons about.
6. Python: `SUPPORTED_VERSIONS` in `features.py` gains the new version; nothing else reads feature names by
   position.
7. `benchmark --write`, then `scripts/iterate.sh` without `--against`: nothing before this change compares,
   the engine, the content, and the schema all moved. This run is the new baseline; the journal entry says
   so and names the schema version.

### The typical balancing workflow, end to end

1. **Start from a green run.** `scripts/iterate.sh --run base` on the current `main`: the digest verifies,
   the report exists, the journal has the numbers. Every later run is read against it.
2. **Change one thing.** One spell, one number, one rule. Two changes in one run cannot be told apart.
3. **Rebuild and regenerate the digest** (`benchmark --write`) and look at its table before anything else:
   Greedy against Greedy on the benchmark seeds is the quickest balance read (player 1 share, rounds,
   round cap share, the spells cast).
4. **Iterate against the base**: `scripts/iterate.sh --run <change> --against base`.
5. **Read the report in this order**: the stamp axis that moved (it should be exactly one); the mirrored
   rows (`random-vs-random`, `greedy-vs-greedy`) for balance; `greedy-vs-random` for the size of the skill
   gap; the replayed previous policy, when it could run, for what the change did to a fixed bot; the
   retrained policies last, knowing their delta mixes the change and the training.
6. **Decide and record**: keep, tune more, or revert, written in `docs/learning/journal.md` with both
   content hashes (and engine versions when the engine moved), the deltas, and the reason. Commit the
   content, the digest, the journal, and a kept model under `models/`.
7. **Drop the artifacts on the viewer** when a number surprises you: a trace explains a single match, the
   batch view shows the distributions behind an average.

What "balanced" means here is a choice, not a formula. The signals the report always shows are the ones the
roadmap named: player 1 share near one half on mirrored play, average rounds inside a band you pick, no
dominant spell (entropy well above zero), few matches ending by the round cap, a fizzle rate that stays low.

## What is deliberately not here yet

No neural network, no reinforcement learning, no GPU. The linear table is enough to prove the loop and to
measure the content; anything heavier gets its own decision record when the table stops improving.

The first thing the loop actually taught us is in that spirit. Value regression lost every mirrored match
against greedy three times running, on more data, on more regularization, and on a mixed dataset, while
behaviour cloning on the same data matched greedy exactly. The cause was not the model or the code but the
data: greedy never shows an alternative move, so the learner had nothing to compare. That is what
`--explore` addresses, and it is why the next number worth writing in the journal is an exploring run
(ADR 0014).
