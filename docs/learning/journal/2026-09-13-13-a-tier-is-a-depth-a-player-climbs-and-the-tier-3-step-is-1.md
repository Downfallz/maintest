# 2026-09-13. A tier is a depth a player climbs, and the tier-3 step is 1.13x

- **What changed**: `_tiers` reads a spell's prerequisites as well as its node (ADR 0034). The catalogue's
  shape goes from 3 / 6 / **27** to 3 / 6 / 9 / **18**. No content moved; scores across this change are not
  comparable, the way ADR 0029 made them incomparable.
- **The entry below called this "not a blocker" and that was wrong.** It was measured the lazy way — the
  values barely move, because each target reports its worst tier — when the test that mattered was whether
  the reading still ranks two candidates the same way. It does not. On two proposals for `chain_slash`,
  `tierDamageSpread` reads 3.360 / 3.662 / 4.164 under node depth and 2.910 / **2.825** / **2.884** under the
  real one: both look worse one way and better the other. The blob holds the openers and the biggest tier-3
  casts together, so any tier-3 buff widens it; split, the same buff is measured against the tier-3 floor and
  narrows it. Eighteen spells were about to be designed against a yardstick that reverses the sign.
- **What the fix makes visible, and it is the number the tier-3 pass needs.** Cast value a round, by tier
  median: 2.00, 4.53, 7.20, **8.12**. The step between tiers is **2.26x, then 1.59x, then 1.13x** — tier 3 is
  barely a tier. And it is the widest: 0.60 to 25.20, a factor of **42**, against 7.6 at tier 2 and 2.2 at
  tier 1.
- **So the pass has two jobs, not one**: raise the median toward roughly 10.5 — what a 1.45x step on 7.20
  would give, holding the decay between the last two steps — and collapse the spread. Against a working band
  of **8 to 14**, tier 3 today is four spells too big (`infectious_blast` 25.2, `tornado` 16.0,
  `crazed_specter` 16.0, `revenant_guards` 15.6), four already inside it (`toxic_waves` 11.2, `ice_spear`
  10.2, `hateful_sacrifice` 10.0, `chain_slash` 10.0) and **ten too small**, ending at `death_squad` 0.6.
- **Which settles the first spell before it was touched.** `chain_slash` reads 10.00 and is already in the
  band; two candidates that made it bigger both measured worse. Its problem was never its size — its knob
  entry claims "the largest cast in the catalogue" and that has been false since `crazed_specter` (18 damage
  on the board) and `tornado` (12) were enabled beside it.
