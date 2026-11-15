# 2026-09-09. First full turn of the loop (`scripts/iterate.sh`), run "premier"

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
