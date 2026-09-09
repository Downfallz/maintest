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

## What is deliberately not here yet

No neural network, no reinforcement learning, no GPU. The linear table is enough to prove the loop and to
measure the content; anything heavier gets its own decision record when the table stops improving.
