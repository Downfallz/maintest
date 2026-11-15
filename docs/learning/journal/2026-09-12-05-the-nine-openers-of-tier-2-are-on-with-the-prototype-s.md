# 2026-09-12. The nine openers of tier 2 are on, with the prototype's numbers and no balance claim

- **What changed**: the one opener of each of the nine specialisations is enabled — `protective_slam`,
  `full_plate`, `enraged_charge`, `parasite_jab`, `momentum`, `noxious_cure`, `meteor`, `summon_minions`,
  `healing_screech` — together with `talent-tree:base_creature:v1`, which carries them, and the one creature
  now points at that tree. The eighteen deeper spells of those specialisations stay off and are pruned from
  their nodes. The catalogue goes from 9 spells to **18**. Content `37ec4b49` to **`ef078082`**.
- **`talent-tree:core_classes:v1` is disabled in the same change.** `base_creature` has the same root, the
  same three branch nodes and the same six tier-1 spells, so it is a strict superset: leaving both on would
  ship a tree no creature can reach. This is what the one failing test caught —
  `The_repository_content_builds_and_loads` asserts the repository ships a single tree, and that assertion is
  right.
- **No number was tuned.** Every enabled spell carries the value the legacy port gave it. This entry is the
  measurement of that state, not a balance pass, and the state is deliberately unbalanced.
- **Numbers**, greedy mirror on the benchmark seeds: matches fall from 8.5 rounds to **6.2**, 0.5 % reach the
  round cap, 12 of the 18 spells are cast. Of the nine openers, four are cast — `meteor` **946**,
  `noxious_cure` 151, `enraged_charge` 122, `healing_screech` 55 — and **five are never cast at all**:
  `protective_slam`, `full_plate`, `parasite_jab`, `momentum`, `summon_minions`.
- **`meteor` is the new monopoly**: 946 landed casts for **8928 damage**, the largest single source in the
  game. Four damage on up to three enemies for three energy, on teams of three.
- **Why the five are dead, and it is not close.** Greedy takes the highest raw score it can afford, and
  `weights.Energy` is 0.2, so a cheaper spell gains almost nothing in the score: cost bites through the two
  energy a round pays, not through the price. `lightning_bolt` carries 6.47 at two energy and any creature can
  unlock it beside its own specialisation, so that is the bar. `protective_slam` carries 4.00 at the same
  price, `parasite_jab` 3.00, `full_plate` 1.95, `summon_minions` 0.60, `momentum` 0.20 — and `momentum` and
  `summon_minions` both hand over less energy per activation than the free `wait` every creature starts with.
  `check-knobs` reaches the same finding from the content alone: `lightning_bolt` strictly dominates
  `protective_slam` and `parasite_jab`, and neither becomes a choice anywhere inside its declared bounds.
- **A gap in the tooling this exposed**: `cast_value` — what `check-knobs` compares spells with — ignores
  `maxTargets`, while `ActionScorer` sums a cast over every target it hits. `meteor` reads 6.00 to the knobs
  and plays at roughly 18. That is why an AoE could be the strongest spell in the game and no check said so.
  Recorded here; fixing it is its own change.
- **What follows**: the nine openers are designed one at a time, identity first, and this digest is the
  before. Nothing in this entry is a claim that the tier is balanced.
