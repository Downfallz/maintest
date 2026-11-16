# 2026-09-12. The knobs check learns to read a sweep, and to see a free permanent buff

- **What changed**: `learning/`, no content. `cast_value` now prices a cast for every target the spell is
  allowed, `outclassed` only takes a rival that reaches as many targets or more, and a new `unbounded` check
  reports a permanent effect that costs no energy. No number in `data/` moved, so the content hash and the
  digest are untouched.
- **The sweep.** `cast_value` read one target where `ActionScorer` sums a resolution over all of them, so
  `meteor` priced at **6.00** and played at **10.74 damage a landed cast** — the largest single source in the
  catalogue, passing every check. Measured over the benchmark seeds, the single-target spells land 0.88 to
  1.01 of their expected value and `meteor` lands **1.79** of its three targets: reading one understated it
  by 1.8x, reading three overstates it by 1.7x. Three is the reading kept, because the question the number
  answers — can this spell ever be a choice — is a question about a spell at its best.
- **Which broke the module's own rule, and the fix is the second half.** With a sweep priced at 18.00,
  `outclassed` made `meteor` the bar for its whole tier and reported `protective_slam` as never a choice on
  content that casts it **354 times in 400 matches**. That module states its error may only run one way —
  under-report, never invent — so a rival now has to reach as many targets or more. A single-target spell
  cannot match a sweep without ceasing to be single-target, which is `dominates`'s axis and not this one.
  With the rule, the findings are `parasite_jab` twice, `pummel` and `throwing_star`: the four true ones.
- **The permanent buff, where the diagnosis was wrong and worth writing down.** The suspicion was that
  `PERMANENT_CONDITION_ROUNDS = 3` understated a permanent effect. It does not: it matches
  `ActionScorer.PermanentConditionRounds`, and raising it would not find the real problem anyway, because
  every reading here prices **one cast** and one `full_plate` is a point of defense at any horizon. What has
  no brake is re-casting: free, never expiring, stacking, bounded by nothing but the round cap. So the check
  reads the **price**, not the magnitude, and `full_plate` is the one spell in the catalogue that trips it.
  Its own cost knob reaches zero, so this is also what stops a tuning pass from putting it back.
- **Why it matters now**: the nine openers are being designed against these numbers. A tool that reads a
  sweep at a third of its worth and calls an unbounded spell fine is not a tool to design nine spells with.
