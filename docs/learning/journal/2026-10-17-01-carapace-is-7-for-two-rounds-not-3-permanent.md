# 2026-10-17. Carapace is +7 for two rounds, not +3 permanent

The content goes from `3ae51e9f` to `53bef178`, one spell changed: Carapace (`full_plate`, Ironhide, level 2).
Its digest is committed beside the old one.

- **Why Carapace.** The slot log (`--slot-log`, read by `scripts/slot-choices.py`) records every castable
  spell's value at each slot the lookahead decides. Read on `explore:0.2:lookahead` against `lookahead`, 400
  benchmark matches, 13,817 slots, a few spells were seldom the best cast and far behind when they were not,
  most of them defensive. Replayed reading four rounds ahead (`lookahead:4x4`, 179 matches on the first 100
  seeds, the run cut short by a container restart), six of them stayed there: Overdrive, Wraithguard,
  Fortress, Carapace, Vital Surge and Emberstorm. Carapace was the best cast of its slots 9.6 % of the time,
  5.5 points behind in the median.
- **Why not just a bigger number.** Three of the six are permanent defense that stacks when cast again, and
  stacked permanent defense is what stalls a match: `roundCapShare`'s own note says so, and the owner's
  rejection of pass 28's Bonewall move was the same stall. So the change is the shape, not the amount: heavy
  and short, deciding the rounds it covers, gone after. A duration counts full rounds after the cast's own
  (`Condition.RemainingRounds`), so +7 for 2 rounds holds through the rest of the round it is cast in and the
  two after. Cast every round, that is at most three copies, 21, which the cap on defense buffs holds to 10
  (ADR 0076); permanent, it had no ceiling but that cap.
- **Four versions on the same 200 benchmark seeds, both seats.** Greedy against itself (mirror), and
  `explore:0.2:lookahead` against itself (variety, with the slot log):

  | Carapace | rounds, mirror / variety | over 16 rounds, variety | round cap, mirror | cast by Greedy | cast again next round | best cast | median gap |
  | --- | --- | --- | --- | --- | --- | --- | --- |
  | +3 permanent | 10.1 / 10.25 | 1.5 % | 1 % | 132 | 3 % | 9.6 % | 5.5 |
  | +5 for 2 rounds | 10.1 / 10.1 | 0.5 % | 0 % | 162 | 6.5 % | 12.5 % | 5.6 |
  | **+7 for 2 rounds** | 10.4 / 10.4 | 2 % | 1 % | 265 | 1.3 % | 16 % | 5.1 |
  | +8 for 1 round | 10.1 / 10.2 | 1.5 % | 0 % | 76 | 0 % | 8.7 % | 6.6 |

  An option taken at random would be the best cast about 12 % of the time (some eight castable spells a
  slot). At +7 Carapace is above that, Ironhide is bought more (Carapace offered at 954 slots, 730 before),
  and nobody spams it. +8 for 1 round covers one round fewer for one more point, and reads worse than +3
  permanent.
- **What it costs.** Matches run 0.3 rounds longer and the share over 16 rounds goes from 1.5 % to 2 %; the
  round cap stays at 1 %. All of it inside the band of 8 to 14 rounds, and within what 400 matches can tell
  apart. The owner took +7 without a replay on the confirmation seeds, so this is one seed set: the next
  tuning pass and the next slot log read it again.
- **The knobs.** Carapace's entry gets a new intent and keep, its amount searched from 5 to 8 and its duration
  from 1 to 2 rounds, its cost unchanged. `data/balance/README.md` drops it from the rule on permanent buffs.
