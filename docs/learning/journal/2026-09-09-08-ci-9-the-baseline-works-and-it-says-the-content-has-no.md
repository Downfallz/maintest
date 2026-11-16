# 2026-09-09. `ci-9`: the baseline works, and it says the content has no decision in it

- **What changed**: ADR 0016 implemented. Same run as `ci-5` otherwise — the thousand-match explored dataset,
  alpha 10, min samples 10 — so the two-part fit is the only difference. Engine `1cc41a7797e3`.
- **The fit improved**: loss 0.8050 to **0.7705**, r² 0.1876 to **0.2225**. 293 fitted actions of 455, the same
  as `ci-5`, as expected. Against `Greedy`: 0 of 400, the eighth time. Against `Random`: 83.2%.
- **The number that ends the investigation**: **`baselineR2` 0.2815 against `r2` 0.2225.** The position alone
  explains more of the held-out return than the position and the action together. The action rows do not
  merely add nothing; they add variance, and the prediction is better without them.
- **Why, and it is not the learner**: a creature starts with three spells, and they are
  `basic_attack` (1 damage), `wait` (1 damage, effects identical to `basic_attack` to the character) and
  `heavy_strike` (1 damage plus Bleed 1 for one round). All three cost 0 energy, all three have initiative 1,
  all three target one enemy. `heavy_strike` therefore strictly dominates: same cost, same speed, same
  targeting, strictly more damage. `wait` and `basic_attack` are one spell under two names. There is no
  trade-off on any axis, and `baseEnergy` is 0, so the resource axis is inert too.
- **Which explains every earlier number**: `Greedy`'s spell entropy of 0.23 is not a defect, it is correct
  play; exploration's deviations are uniformly worse by an amount the position already carries; and an action
  that carries no information cannot be fitted, however the data is recorded or the model is shaped. The
  metric added to diagnose the model diagnosed the content instead.
- **Decision**: stop here on value regression. It is not broken, it is asking a question this content does not
  pose, and the temporal-difference target considered next in ADR 0016 is dropped with it: better credit
  assignment for a decision that does not exist would refine an instrument aimed at nothing. Behaviour cloning
  stays the loop's working learner. The loop's own balance signals — spell entropy, player 1 share, draw rate,
  round cap share — are the instrument for the content work that comes next, and `Greedy`'s entropy rising
  above 0.23 is the sign that the content finally offers a choice.
