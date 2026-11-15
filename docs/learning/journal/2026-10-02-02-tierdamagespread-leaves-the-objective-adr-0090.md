# 2026-10-02. `tierDamageSpread` leaves the objective (ADR 0090)

- **What it read under ADR 0089**, per package, on the benchmark, confirmation and hold-out seeds:
  - Assassin: 3.74, 3.24 and 3.48. Momentum (free, +2 energy) does about 1.9 a target, Shadowstep (3
    energy) about 6.5.
  - Soulreaver: 2.34, 2.53 and 2.49. Soul Devourer (drain and heal) does about 4.9, Hateful Sacrifice
    (self-damage) about 12.1.
  - Prowler: 2.26, 2.33 and 2.34. Throwing Star does about 2.9 a target on two targets, about 5.9 a cast.
    Poison Slash does about 6.8 on one.

  Every one of those splits is designed. The ratio sees neither cost, nor reach, nor riders.
- **A hand-tuned candidate inside the bounds** made five changes: Momentum 3, Shadowstep critical 0.3, Soul
  Devourer 7, Hateful Sacrifice critical 0.4, Poison Slash 2.
  - It still read 2.70, 2.77 and 2.66.
  - `tierUsageShare` went to 0.929 on the benchmark seeds.

  It was not kept.
- **What changed.** The target is gone from `data/balance/knobs.json`. The metric is still reported, and
  `tierUsageShare` and `tierWinSpread` read whether a package's spells earn their pick.

  | Objective, same runs, the term removed | Benchmark | Confirmation | Hold-out |
  | --- | --- | --- | --- |
  | Before (ADR 0089) | 18.81 | 10.03 | 13.19 |
  | After (ADR 0090) | **6.63** | **3.84** | **4.47** |

  These are on a different set of targets from every score before. What is left is `tierUsageShare` 0.87
  to 0.89, which is the one a tuning pass now has to move.
