# 2026-09-10. Energy gets a price and a lasting kind: nothing moves, and that is the finding

- **What changed**: (a) `ActionScorer` scores `EnergyOutcome`, which it never did — the switch matched
  `HealOutcome` and `ConditionOutcome` and let energy fall through to zero, while the `weights.Energy` term
  beside it priced only the energy the actor *keeps* after paying. (b) `EnergyRegeneration` joins the effect
  taxonomy, `Regeneration` with energy in place of health, given before the healing and the bleeds and never
  wasted because energy has no cap (ADR 0020). (c) `SpellOutcome.Buffs` splits into `DefenseBuffs` and
  `InitiativeDebuffs`: one number for two stats could not say which one a spell moved. (d) An outcome on a
  creature the same action kills is no longer scored on any of the three paths, since `Heal` and `GainEnergy`
  both return zero on a corpse.
- **Digest**: unchanged. `benchmarks/50a291d5…7a87.json` verifies with **400 of 400 matches identical**,
  engine `094515bf7822`, schema `features:v3+18b1bd690dff`. This entry exists anyway, because the absence of
  movement is the result: the journal's rule is one entry per change that moves a number, and the number that
  refused to move is the whole point.
- **What started it**: 200 recorded `Greedy`-vs-`Greedy` matches, read off the traces. Of **5096
  declarations, none was one of the five `EnergyGain` spells** — not Wait, which every creature knows from
  round one, not Summon Minions, which gives 3 energy for a cost of 2. The agent was structurally unable to
  value any of them, so the count is not a preference, it is a blind spot.
- **Why fixing the blind spot changed nothing**: a point of energy is worth 0.2, and the wall is arithmetic.
  Best-case one-step score of what `Greedy` actually picks against what it never picks:

  | Spell | Score | | Spell | Score |
  | --- | --- | --- | --- | --- |
  | `meteor` | 18.0 | | `restorative_gush` | 4.8 |
  | `engulfing_flames` | 12.0 | | `healing_screech` | 3.2 |
  | `ice_spear` | 7.0 | | `restorative_burst` | 2.8 |
  | `lightning_bolt` | 5.0 | | `rejuvenate` | 2.4 |
  | `heavy_strike` | 3.0 | | `summon_minions` | 0.6 |
  | `basic_attack` | 1.0 | | `wait` | 0.2 |

  The best heal in the game, cast at the one moment it caps out, loses to `lightning_bolt`. The best energy
  spell loses to `basic_attack`. No decision flips, so the 400 mirrored seeds replay identically.
- **The same measurement, for healing**: **0 of 5096** declarations were a heal, and the reason is not the
  declaration step. In **1798** declarations the actor was missing health — often 8 to 14 of 20 — and in
  exactly **2** of those did it know a heal at all. The break is at the unlock: **2 heal unlocks out of
  4660**. And **47% of evolution choices are made at full health**, where a heal is worth exactly zero by
  construction, round 1 being 800 choices at 100% full — the one round `rejuvenate` is offered beside
  `lightning_bolt`. The spell is on the table precisely when it cannot score.
- **What the fix does buy, then**: the knob exists. Before, no value of `weights.energy` could make an energy
  spell visible, because the gain was multiplied by nothing; `search-weights` could not have found a setting
  that worked, whatever it tried. Now it can, and whether one exists is an empirical question about the
  content rather than a property of the code.
- **Two things I predicted and measured to be false**, both corrected in ADR 0020. I expected the digest to
  move — it does not. And I wrote that energy touching no health made its place in the tick order irrelevant
  — it is not: every upkeep loop skips the dead, so a creature its own bleed kills that round keeps the
  energy it was just given and still reports a tick. No health number and no match result depends on the
  position, which is the narrower claim that survives.
- **`features:v3`**: publishing a condition kind changes the observation layout, so nothing trained under v1
  or v2 is comparable. `EnergyRegeneration` sits beside `Regeneration`, so the three over-time effects stay
  together and every index from +10 on shifts. The Python side reads all three versions; the engine plays
  only the one it reads.
- **No spell uses the new kind yet**, deliberately. Re-pricing Momentum and Summon Minions in the change that
  adds the kind would leave nothing able to say which half moved the numbers. That is also why this change is
  measurably behaviour-preserving, which is what let the two review fixes ride along without muddying it.
- **Still not a balance pass**: nothing here was tuned. Both findings now point at the same place — the
  content's numbers, not the agent — and neither the heal amounts nor the energy amounts have ever been
  chosen by measurement.
