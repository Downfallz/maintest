# 0048. The baseline is a training-time device, and was fitted as if it were not

Date: 2026-09-15
Status: Accepted

## Context

`baselineR2` read **0.07054** on five consecutive runs — `ci-72`, `ci-74`, `ci-78`, `ci-80`, `ci-81` — while
the lambda moved from 1.0 to 0.5 and back. Every lambda below 1.0 estimates an advantage as
`discount * V(next) - V(here)` (ADR 0046), so the whole credit-assignment mechanism rests on a baseline that
explains seven percent of the return. The journal named it as the binding constraint; this ADR is the
measurement of what was actually constraining it.

**The baseline never affects which action is played.** `LinearScorer.scores` adds `baseline.value(observation)`
to every candidate of a decision alike, and the code says so: *"The same number for every candidate, so it
never changes the winner; it makes a score a return."* So it is a **training-time device**: it shapes the
advantage the action rows are fitted on, and at play time it is presentational. Improving it costs no format
change, no engine change and no feature schema — which is not how it had been treated.

Measured on the 1000-match exploring dataset of those five runs (`search-4` self-play, seed 1, held out by
match — the numbers reproduce the runs exactly):

| | all held-out steps | rounds 15-30 |
| --- | --- | --- |
| as fitted, alpha 10 | +0.0705 | **-0.2179** |
| alpha 10000 | +0.1021 | +0.0200 |
| gradient boosting, same features | +0.1215 | +0.3760 |

Three things came out of it.

1. **The alpha was far too low, and could not be raised.** Held-out `r2` rises monotonically with it, but
   `--alpha` is shared with the action rows, which are fitted on a median of sixty examples each and want
   the small number. One knob served neither.
2. **The fit predicts returns that cannot happen.** `Returns.Of` pays a win, a loss or a draw plus a tenth of
   the health margin, so no return here is outside 1.05 either way; the linear fit predicts from -1.78 to
   +2.19. In rounds 15 and beyond that made the baseline **worse than predicting a constant** — `r2` -0.2179
   against -0.0014 for the training mean — and clipping alone recovers half of it.
3. **The ceiling is the features, not the linearity.** Gradient boosting on the same features reaches 0.1215
   where a well-regularised linear fit reaches 0.1021. There is little left for a better model to find.

The late game matters more than its share of steps suggests: the high-lambda policies live there. `ci-81` at
lambda 0.9 plays **19.1 rounds** against `Greedy` and reaches the round cap in **46.2%** of matches, where
`Greedy` against `Greedy` plays 7.8 and caps 0.5%. Their advantage was being taken from a baseline that is
anti-predictive exactly where they operate.

## Decision

Two changes, both Python-side and both training-time only.

- **`train-value --baseline-alpha`** (and `iterate.sh --value-baseline-alpha`) pulls the state baseline
  toward zero independently of the action rows. **Its default is to keep sharing `--alpha`**, so every run
  before this reproduces unchanged.
- **The baseline is clipped to the range a return can take**, everywhere it is used as a value, and that is
  not a knob: a prediction the target cannot take is wrong by construction. The bound is taken from the
  training returns rather than from a constant copied out of `Returns.Of`, so it cannot drift from the engine
  and it follows the return definition if that ever moves.

The written `baseline` in `policy.json` stays the bare fit. Clipping it there would change nothing, because a
reader adds it to every candidate alike, and leaving it alone keeps the file exactly what ADR 0013's exchange
format says it is.

**The fit metrics score against the clipped value, not the written one.** The action rows are fitted against
an advantage taken from the clipped value, so reconstructing a score from the unclipped baseline mixes two
different values and is wrong by exactly the overshoot — on the steps the clip exists for, and nowhere else.
On the 1000-match dataset that is `r2` 0.0237 against 0.0273 and `loss` 1.0062 against 1.0024. `accuracy` is
unaffected either way, since a baseline adds one number to every candidate of a decision and cannot move an
argmax. Found by the Codex review of `d71e565`.

## Consequences

- Good: on the same dataset, `baselineR2` goes **0.0705 → 0.0806** from the clip alone and **→ 0.1053** with
  `--baseline-alpha 10000`, and rounds 15-30 go from **-0.2179 to +0.0685**. The advantage every lambda below
  1.0 is built on stops being taken from an anti-predictive signal in the late game.
- Bad: **`baselineR2` no longer means what it meant in the five runs that reported 0.07054.** It is now
  measured on the values actually used, clip included. The step change on identical data is 0.0705 to 0.0806
  and is recorded in the journal, so the series is readable across the break, but it is a break.
- Neutral: nothing downstream moves. `policy.json`, `PolicyAgent`, the feature schema and the benchmark
  digest are untouched, and a policy trained before this plays exactly as it did.
- Open: **a nonlinear baseline is now permissible and was not before.** Since the baseline never reaches the
  engine, gradient boosting could supply the values for the advantage at 0.1215 against 0.1053, with no
  format consequence at all. That is the obvious next step and it is not taken here.

## Alternatives considered

- **Give the baseline a `round_fraction` interaction with the health features**, so it can read "health
  decides more as the cap approaches" (ADR 0011). The natural hypothesis, and **it was measured and it is
  worse**: overall `r2` 0.0705 → 0.0574, and rounds 15-30 -0.2179 → -0.4375. Recorded because it was wrong,
  not despite it.
- **Add the bound as a field on `Baseline`**, so the clip travels in the file. Rejected: it changes ADR 0013's
  exchange format and obliges the C# side to read a number that cannot change any decision it makes.
- **Raise `--alpha` for everything.** What the shared knob forces today. It would buy the baseline 0.03 of
  `r2` and starve the action rows, which are the thing the policy actually ranks with.
- **Leave it and spend the effort on the features.** The ceiling above says that is where the remaining
  signal is. It is also a feature schema change, which invalidates every trained model, so it wants its own
  decision rather than riding this one.

## Follow-up

- `learning/src/downfall_learning/train_value.py`: `ValueOptions.baseline_alpha`, `alpha_of_the_baseline`,
  `_reachable`, and the `baselineAlpha` metric.
- `learning/src/downfall_learning/cli.py` (`--baseline-alpha`), `scripts/iterate.sh`
  (`--value-baseline-alpha`), `.github/workflows/iterate.yml` (`value_baseline_alpha`).
- `learning/tests/test_train_value.py` pins the separate pull, the clip, and that the clip narrows the
  advantage. It does **not** pin the metric fix above: the test fixture's baseline predicts inside the
  return range at every alpha, so the clip never bites there and no test written on it can tell the two
  metrics apart. That one is verified on the real dataset instead, and the numbers are in the journal.
- The journal entry of 2026-09-15 carries the measurements, including the interaction that failed.
- Open: the nonlinear baseline above, and whether any of this moves a win rate — nothing here has been played.
