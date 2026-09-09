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

- **Agent**: a bot that decides for one player. `random` picks anything legal. `greedy` looks one move ahead
  with a fixed scoring (a kill is worth 5 damage, a stun 3, and so on). `heuristic:<file>` is greedy with the
  scoring read from a file, so it can be tuned. `policy:<file>` follows a trained model.
- **Observation**: the board turned into a list of numbers (who is alive, how much health, what spells are
  known, ...), always in the same order. A model only ever sees these numbers. The order is the **feature
  schema**, and it has a version so a model is never fed numbers laid out differently from what it learned on.
- **Action key**: a short text for one possible decision, such as `intent:1:spell:rend:v1` (creature in slot 1
  declares Rend) or `speed:0:Quick`. A model scores keys; the best-scoring legal one is played.
- **Dataset**: the notes of a batch of matches: for every decision, the observation, the legal keys, the key
  taken, and later the **return** of that player in that match (+1 for a win, -1 for a loss, a little more or
  less depending on the health margin).
- **Policy**: the trained model. Here a policy is a table: one row of numbers per action key. The score of a
  key is its row multiplied with the observation, plus a bias. Nothing fancier, on purpose: the engine reads
  it back with a loop of multiplications and no machine-learning library.
- **Benchmark seeds**: 200 fixed random seeds. Every evaluation plays these same 200 matches twice (each agent
  gets to be player 1 once), so results are comparable from one day to the next.
- **Run stamp**: the label on every result: engine version, content hash, rule set, feature schema, both
  agents, and the seed set. If two stamps differ on more than one axis, the comparison is not meaningful.

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

`scripts/iterate.sh` runs one full turn of the loop and leaves everything under `runs/<run-id>/`:

1. Build the engine and the content, and check the **benchmark digest**: the engine replays the 200 seeds
   with greedy against greedy and compares every outcome with the committed record. Any difference means the
   engine or the content changed behaviour, on purpose or not, and the loop says so before measuring anything.
2. Play the **baselines**: random against random (is the game balanced between player 1 and player 2?),
   greedy against greedy (same question for a strong deterministic bot), greedy against random (how big is
   the gap a learned bot must beat).
3. **Record** a dataset of greedy playing itself.
4. **Train** the value and clone policies on it.
5. **Evaluate** each policy against greedy and against random, on the benchmark seeds. The win rate goes into
   the model's `training.jsonl`, so the viewer shows it next to the learning curve.
6. If a previous run is named (`--against`), **replay its policy** on today's engine and content, so the
   comparison has a row where only the engine or the content moved, never the training. When the content
   change altered the observation layout, the old policy cannot run and the report says so.
7. Write **`report.json`** and print the table: per evaluation, the win rate with its uncertainty, the
   player 1 share, draws, rounds, the share of matches ending by the round cap, the spell entropy (how many
   different spells were cast: low means one dominant spell) and the fizzle rate. With `--against`, a second
   table of deltas and the stamp axis that moved.

Then one entry in [journal.md](journal.md): what changed, the numbers, the decision (keep, tune, revert).

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
   and the version bump to `features:v2` with its section in `docs/learning/features.md`, since rule 2 there
   says a new kind is a new version. `FeatureSchemaTests` fails until the kind is listed, which is the
   reminder.
4. Agents: what the lookahead thinks the condition is worth, in `ActionScorer` and a `ScoringWeights` weight
   if none fits (then `learning/weights/greedy.json`, `JsonScoringWeightsSource`, `docs/learning/agents.md`).
5. Python: `SUPPORTED_VERSIONS` in `features.py` gains `features:v2`; nothing else reads feature names by
   position. The viewer shows conditions as chips by kind and needs nothing.
6. `benchmark --write`, then `scripts/iterate.sh` without `--against`: nothing before this change compares,
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
