# Learning journal

One entry per change that moves a number: content, engine, agents, or the benchmark seeds. Each entry names
the run stamps involved so that any two results can be compared on one axis at a time (ADR 0013). Newest
first.

## 2026-09-09. `ci-3`: three quarters of the actions have no model at all

- **What changed**: nothing but the diagnostic. `ci-3` repeats `ci-2` exactly — same explored dataset, same
  alpha 10 and min samples 50 — and returns the same numbers to the digit: loss 0.9607, r² 0.04694, accuracy
  0.3044, 0 of 400 against `Greedy`, 94.75% against `Random`. The loop is deterministic and `fittedActions`
  costs nothing.
- **The number**: **93 fitted actions out of 382**. Two hundred and eighty-nine keys kept the mean of their
  few examples instead of a regression, and a row that is a constant scores the same in every state. So for
  three quarters of the legal moves the policy cannot tell one position from another; it simply prefers
  whichever constant is largest. That is what the spell entropy of 2.70 is made of.
- **What it means, read with `ci-1`**: this is a squeeze, not a mystery. At a threshold of 5 nearly every row
  gets a regression, on far too few examples, and the fit comes out worse than the mean. At 50 the fit turns
  positive and three quarters of the rows lose their model. 62,358 steps over 382 keys is about 163 per key
  on average, skewed enough that only 93 clear fifty in the training split. Exploration multiplied the action
  keys by five and the dataset did not follow.
- **Decision**: raise `--matches` to 1000 before concluding anything about the shape of the model. The
  question "is one independent regression per action the wrong shape" cannot be answered on a dataset where
  most of those regressions were never fitted.

## 2026-09-09. The tuned exploring run (`ci-2`): the fit moved, the win rate did not

- **What changed**: the same explored dataset as `ci-1` (`explore:0.2`, 200 matches from seed 1, content
  `34c616d3…80d7`), trained with `--value-alpha 10 --value-min-samples 50` instead of the defaults. It was
  asked for by committing `learning/experiments/next.json`, and the loop ran on the pull request that
  carried it. Engine `454dc1a937e6`, clean: the stamp defect of `ci-1` is gone.
- **The fit moved**: r² -0.146 to **0.047**, loss 1.155 to 0.961, accuracy 0.293 to 0.304. Against `Random`
  the policy went from 82.2% to **94.8%** and its spell entropy fell from 3.40 to 2.70. The regularization
  did produce a measurably better policy.
- **The number that matters did not**: 0.0% against `Greedy`, 0 of 400, for the fifth time.
- **What that settles**: the knobs were the confound in `ci-1`, and they are not the obstacle. Two runs now
  bracket them — worse than the mean at alpha 1 and min samples 5, positive at alpha 10 and min samples 50 —
  and the win rate against `Greedy` is exactly zero in both. Exploration at this rate, on a dataset this
  size, does not make value regression competitive with the bot that produced the data. ADR 0014 was
  necessary, since the counterfactuals now exist, and it is not sufficient.
- **The one thing still unmeasured**: with 382 action keys and a threshold of 50, an unknown share of the
  rows kept a mean instead of a model, and a row without a model cannot tell two states apart. `train-value`
  now reports `fittedActions` beside `actions`, so the next run says it outright.
- **Decision**: read that number first. If most rows are starved, the answer is more matches. If most are
  fitted, the remaining suspect is the shape itself — one independent regression per action key, ranked
  against each other at a single state — and the next thing to try is a single model over the state and the
  action together, which needs its own ADR.

## 2026-09-09. First exploring run (`ci-1`), still 0 of 400, and two things moved at once

- **What changed**: the value policy trained on a dataset recorded with `explore:0.2` instead of pure
  `Greedy` self-play (ADR 0014), on the same content `34c616d3…80d7`. This is also the first run of the
  `Learning loop` workflow, on the merge commit of pull request #24, so the whole turn played on the CI
  runners in three minutes with nobody at a keyboard. Its stamp reads `2988cd742f9c-dirty` because the
  workflow wrote its log inside the checkout before the build stamped the version; the tree was otherwise
  that commit exactly. Fixed in the same change as this entry, so the next stamp is clean.
- **The two datasets**, 200 matches each from seed 1: pure `Greedy`, 63,706 steps over 75 distinct action
  keys; explored, 62,358 steps over **382** action keys.
- **Value policy**, at the defaults (alpha 1.0, min samples 5): loss 1.155, **r² -0.146**, accuracy 0.293 on
  12,320 held-out steps. 0.0% against `Greedy`, 0 of 400 for the fourth time, and 82.2% against `Random`.
  Its spell entropy against `Greedy` is 3.40, next to `Random`'s 4.25 and nowhere near `Greedy`'s 0.20: the
  policy scatters instead of choosing.
- **Clone policy**, still trained on the pure dataset and therefore still the control: 98.6% accuracy, 38.0%
  against `Greedy` (score 0.475), 100% against `Random`. Unchanged, as it should be.
- **What this settles and what it does not**: exploration did deliver what ADR 0014 asked of it. Seventy-five
  action keys became 382, so the actions `Greedy` never plays now carry samples of their own. But the same
  62,000 steps are spread over five times as many independent regressions, and this run used the defaults
  rather than the `--value-alpha 10 --value-min-samples 50` that had taken r² from 0.216 to 0.372 on the
  greedy dataset. The dataset and the knobs moved together, so a fit that is now worse than predicting the
  mean does not by itself refute the ADR.
- **Decision**: repeat the run with those two knobs on the explored dataset, which is one dispatch of the
  workflow. If the win rate is still zero once the fit is no longer worse than the mean, the remaining
  suspect is the shape of the model itself, one independent row per action, and the next thing to try is a
  single model over the state and the action together.

## 2026-09-09. Two tuning attempts and a mixed dataset, all still 0 of 400

- **What changed**: nothing in the engine or the content; three trainings of the value policy on the same
  content `34c616d3…80d7`, evaluated against `Greedy` on the 200 benchmark seeds, mirrored.
- **The three attempts**: 200 matches of `Greedy` self-play at the defaults (run "premier"); the same
  fivefold larger with `--alpha 10 --min-samples 50` (run "second"); and the union of a 200-match `Greedy`
  self-play with a 200-match `Random` self-play, `--allow-mixed`, at those same settings. Win rate against
  `Greedy`: 0.0000 each time, no draw, 400 matches each time. The held-out fit did improve between the first
  two (r² 0.216 to 0.372, loss 0.729 to 0.508), so the models are genuinely different and the metric that
  matters ignored it.
- **The control that matters**: behaviour cloning on the same data, through the same `PolicyAgent`, reaches
  98.5% accuracy and 38.0% against `Greedy`, statistically `Greedy`'s own 39.25%. The encoding, the policy
  file, the agent and the evaluation are therefore sound, and the fault is in what value regression is asked
  to learn, not in the plumbing.
- **Why**: each action key gets its own regression, fitted only on the steps where that action was taken.
  Under a deterministic policy those subsets are disjoint state distributions, so `heavy_strike` is fitted on
  the states where `Greedy` wanted it and `basic_attack` on the leftovers. The return then measures how good
  those situations were, not how good the action is, and ranking two such rows at one state compares models
  calibrated on different worlds. `Random` self-play does not repair it: it covers actions in states no
  strong policy visits, with returns a single action barely moves. This supersedes the "overfitting on a rare
  action" reading of the entry below, which explained the extreme weights but not why more data and more
  regularization changed nothing.
- **Decision**: stop tuning this learner. ADR 0014 proposes recording with an exploring agent, which is the
  one change that puts the same state distribution behind every row. Both cheap attempts it names as
  prerequisites are now done and both failed.

## 2026-09-09. First full turn of the loop (`scripts/iterate.sh`), run "premier"

- **What changed**: nothing in the engine or the content on purpose; this is the first end-to-end run of the
  L7 loop, on content `34c616d3…80d7` (the digest already committed), engine `d3f1fe3284bc-dirty` (the L7
  pull request's head at the time, `#23`, with local uncommitted state, so read this as a smoke test of the
  loop rather than a citable baseline; a clean re-run on the merged engine is the number to keep). 200
  matches of `Greedy` self-play recorded (`simulate --record`, base seed 1), a `train-value` and a
  `train-clone` policy trained on them, both evaluated against `Greedy` and `Random` on the 200 benchmark
  seeds, mirrored (seed set `733404048`).
- **Baselines** (match the committed digest and the L5/L6 journal entries, as expected since content and
  agents did not change): `Greedy` vs `Greedy` 39.25% each, 54.5% player 1 share, 21.5% draws, 22.0 rounds;
  `Greedy` vs `Random` and `Random` vs `Random` unchanged from before.
- **Clone policy**: 38.0% against `Greedy` (interval 34.7% to 41.3%, score 0.475) — statistically the same as
  `Greedy` playing itself (39.25%, interval 36.4% to 42.1%). This is the ceiling behaviour cloning is supposed
  to reach (`docs/learning/explained.md`: "the clone can never be better than what it copies") and it reached
  it: on 200 matches of `Greedy` self-play, the clone reproduced `Greedy`'s own strength almost exactly. 100%
  against `Random`.
- **Value policy — root cause found, with `policy.json` and `training.jsonl` in hand**: 0.0% against
  `Greedy` (0 wins, 0 draws, 400 matches), 99.5% against `Random`. `training.jsonl` shows why the fit itself
  is weak before the match numbers even come in: r² 0.216 and accuracy 25.7% on the 12,683 held-out steps
  (validated on one match in five, never trained on) against 51,023 training steps — a linear model over 383
  features explains barely a fifth of the return's variance. The evaluation's `spellUsage` shows what that
  training failure does at the table: against `Greedy` the policy casts `heavy_strike` 16 times against 9499
  `basic_attack`s (11996 actions total) — almost the mirror image of `Greedy`'s own play, which casts
  `heavy_strike` roughly 96% of the time (L5 journal entry). Against `Random` it is far more reasonable
  (10819 `heavy_strike` against 6193 `basic_attack`), so the policy is not broken in general, only around this
  one choice.
  The weights explain the "why": `intent:X:spell:basic_attack:v1` and `speed:X:Quick` carry the exact same
  bias for creature slots 0 and 1 (`-0.467` and `-0.228` respectively, to the last digit) and an extreme
  outlier for slot 2 (`-6.958` for `Quick`, `-6.355` for `basic_attack`, against a fallback of `0.012` and
  every other bias in roughly `[-0.6, 1.7]`). The identical biases are not a coincidence: `docs/learning/artifacts.md`
  already says the speed and intent sub-phases hand the agent the same board observation for a creature, and
  `Greedy` picks `basic_attack` (and, separately, `Standard`) far less often than `heavy_strike`/`Quick` — the
  same benchmark showed a 578-versus-15039 split. A linear ridge regression over 383 features, fit per action
  key at the default `alpha=1.0`, overfits that thin, low-variance slice of `basic_attack` rows into a large
  negative coefficient; slot 2 happening to draw the worst luck of the three explains the outlier. The value
  agent then reads that coefficient at the table and avoids `heavy_strike` almost entirely.
  Concretely worth trying next: raise `--min-samples` well past the default 5 so a rare key like
  `basic_attack` falls back to the dataset-wide mean instead of fitting its own noisy row; raise the ridge
  `--alpha` (383 features per key is a lot to regularize at the default strength); and record more than 200
  matches so the rare choice gets enough support to fit honestly. None of these are engine bugs — the loop,
  the encoding, and the training code did exactly what they were asked to; the data given to them was small
  enough for the regression to memorize noise on one specific, rare action.
- **Why it matters**: the loop runs end to end, produces a report, and the two learners diverge exactly as
  the theory predicts — cloning is a safe, bounded reproduction of `Greedy` (and reached that bound), value
  regression is more ambitious and, on 200 matches, currently overfits one rare-but-important choice into a
  losing bot. This value policy is not committed under `models/`: on this evidence it is a "tune more" case,
  not a "keep" case, and a citable number needs a clean (non-dirty) engine commit besides. Next: retrain with
  a higher `--min-samples` and `--alpha` on a bigger recorded dataset, re-evaluate against `Greedy`, and only
  then decide whether to commit it with its evaluation.

## 2026-09-08. Greedy against Random, the first measured gap

- **What changed**: nothing; this is the first measurement across two agents on the same engine and content,
  run by the `Evaluate` workflow (engine `d25c67db04d1`, the merge commit of the L5 pull request, content
  `34c616d3…80d7`, schema `features:v1+31987e1de3a9`, the 200 benchmark seeds, stamped as seed set
  `733404048`), `Greedy` against `Random`, mirrored. The `Seed` the command prints at start is the session
  seed of the interactive commands, drawn at random when `--seed` is absent; an evaluation seeds every match
  from the seed file and never uses it.
- **Numbers**: 400 matches, Greedy wins 400 (100.0%, interval 100.0% to 100.0%), score 1.000, 24.5 health
  left on average, no draw, every match by elimination in 15.5 rounds on average, none by the round cap.
  Intent entropy 0.20 bits for Greedy against 4.07 for Random; fizzles 4.0% against 1.6%; crits 4.9% against
  5.2%. Greedy casts `spell:heavy_strike:v1` 17914 times and `spell:basic_attack:v1` 582 times; Random
  spreads its 12000 or so casts over all 36 spells, `spell:heavy_strike:v1`, `spell:wait:v1` and
  `spell:basic_attack:v1` leading at about 1850 each.
- **Why it matters**: the one-step lookahead beats a uniform policy without losing a match, so the baseline
  is a real bar for the learned agents (L6, L7) rather than a coin flip. The gap is so wide that it says
  little about the content: a stronger opponent than Random is needed to grade the heuristic itself, which
  is what the seed pairs against a tuned heuristic will provide. Greedy's higher fizzle rate most likely
  comes from its focus: several creatures aim at the same enemy, the first kill leaves the later actions
  without a target, and they fizzle. Random spreads its targets and rarely loses one.

## 2026-09-08. Greedy replaces Random as the benchmark baseline

- **What changed**: nothing in the engine or the content; the greedy and heuristic agents arrived (learning
  phase L5) and the benchmark now plays `Greedy` against `Greedy`, so the digest is regenerated once here.
  The previous Random digest for the same content hash is superseded, not comparable.
- **Digest**: `benchmarks/34c616d340ed0d4ac67b0874ce3ac1895c126509b94ebe2a9963dfff45af80d7.json`, the same
  content hash as before, played by `Greedy` against `Greedy` on the 200 benchmark seeds, mirrored, under the
  default rule set. Generated by CI on the L5 pull request (engine `24e35df135f7`).
- **Numbers**: 400 matches, player 1 wins 218, player 2 wins 96, 86 draws by mutual elimination, every match
  by elimination between rounds 19 and 23 (22.0 on average, none by the round cap). Per agent: 157 wins,
  39.3% win rate (36.4% to 42.1%), score 0.500, intent entropy 0.23 bits, 5.1% fizzles, 4.9% crits.
  `spell:heavy_strike:v1` is cast 15039 times against 578 `spell:basic_attack:v1`; no other spell is used.
- **Why it matters**: two identical greedy agents give player 1 a 54.5% win share against 24.0%, far outside
  the noise of 400 matches, where Random against Random showed no edge. The first mover wins the damage race
  when both sides play the same deterministic plan; a rule change that tempers it (initiative, pick order)
  now has a number to move. The low entropy and the two-spell repertoire say the current content offers the
  greedy scorer little to choose from. Greedy against Random is measured by the `Evaluate` workflow, not by
  this digest.

## 2026-09-08. First benchmark digest

- **What changed**: nothing in the engine or the content; the evaluation harness and the benchmark digest
  arrived (learning phase L4).
- **Digest**: `benchmarks/34c616d340ed0d4ac67b0874ce3ac1895c126509b94ebe2a9963dfff45af80d7.json`, the
  content of `data/` at that hash, played by `Random` against `Random` on the 200 benchmark seeds, mirrored,
  under the default rule set (team of three, two energy and two picks per round, thirty rounds, double damage
  on a crit). Generated by CI on the merge commit of the L4 pull request (engine `0554a2aa8fe3`).
- **Numbers**: 400 matches, player 1 wins 206, player 2 wins 194, no draw, every match by elimination.
  Read as a first-mover check: 51.5% for player 1 with two identical random agents, inside the noise of 400
  matches, and the mirrored pairs cancel it in every evaluation anyway.
- **Why it matters**: from this commit, any engine or content change that alters an outcome on these seeds
  fails CI until the digest is regenerated here on purpose. The greedy agent (L5) will replace the random
  baseline and regenerate it once.
