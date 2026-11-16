# 2026-09-23. A creature buys one package an opportunity, the mirror stops being one match, and the objective goes from 11.11 to 5.30

- **What changed.** The two picks of an opportunity resolved in sequence (ADR 0056), so both could go to one
  creature. They cannot now: a creature buys at most one package an opportunity, and a player down to one
  living creature has one pick (ADR 0066). The schedule, the packages and their bonuses do not move, and
  neither does the content. The engine does, and the digest for content `4d7a841c` is regenerated.
- **How the picks were used.** Over 40 greedy mirrors, both picks of an opportunity went to one creature in
  120 of 240 opportunities before the rule, and in none of 340 after it. Greedy never buys above level 1, in
  either version: the stacking was two level-1 packages on one creature, a wider creature rather than a taller
  one. Random agents stack rarely and climb more; they buy 3.55 level-3 packages a match where they bought
  4.99 (300 matches, seed 1), and the first one still lands between rounds 5 and 11.
- **The mirror.** Greedy against greedy on the benchmark seeds: every match lasted 6 rounds and ended 38
  health to 0, one match played 400 times with a d20 deciding the winner. Now the 400 matches last 6 to 13
  rounds, 7.8 on average, and end at many different health totals. Player 1 wins 224 (56 %), from 190.
- **The objective**, content `4d7a841c`, its own knobs and weights, the benchmark seeds, ADR 0065's
  objective:

  | term | before | after |
  | --- | --- | --- |
  | score | 11.11 | **5.30** |
  | `variety.tierUsageShare` | 0.899 (7.77) | 0.849 (1.89) |
  | `variety.tierDamageSpread` | 2.528 (1.12) | 2.587 (1.38) |
  | `variety.spellsNeverCast` | 2 (0) | 4 (1.00) |
  | `variety.spellUsageShare` | 0.298 (0.23) | 0.336 (0.74) |
  | `mirror.fizzleRateA` | 0.179 (0.33) | 0.174 (0.24) |
  | `variety.tierWinSpread` | 0.238 (0.78) | 0.170 (0.04) |
  | `mirror.averageRounds` | 6.00 (0.89) | 7.80 (0.01) |
  | `exploit.winRateA` | 0.958 | 0.258 |

  The monopoly term that was most of the score drops to a quarter of itself. Two more spells go uncast on
  the exploring run, which is the price ADR 0066 names: the deepest packages arrive later. `exploit.winRateA`
  collapses from 0.958 to 0.258 without a penalty moving. The exploit panel's weights were searched under
  stacked picks, and whatever they found in it is gone.
- **What it means.** Every weight set, every tuning run and the benchmark digest were taken under stacked
  picks, and none of them carries over. The tuning pass that was running when this landed, and the weight
  search in #181, both measure the previous rule. Each needs to be run again on this one before any result
  from it is read.
