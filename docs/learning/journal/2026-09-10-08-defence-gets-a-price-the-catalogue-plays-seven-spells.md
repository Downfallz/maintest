# 2026-09-10. Defence gets a price: the catalogue plays seven spells instead of four, and stalls

- **What changed**: ADR 0022 prices a defensive effect by the damage it prevents. No content moved in this
  entry; the digest for content `c0ec6984` is regenerated because the engine changed under it. Engine at the
  commit this entry lands with.
- **What it does**, greedy against greedy on the benchmark seeds:

  | | Before | After |
  | --- | --- | --- |
  | Spells cast | 4 of 9 | **7 of 9** |
  | `rejuvenate` | 0 | 2895 casts, 6509 health restored |
  | `guard` | 0 | 1424 casts, 2656 buffs applied |
  | `poison_slash` | 0 | 963 |
  | Spell entropy | 0.74 | **2.02** |
  | Average rounds | 8.1 | **18.1** |
  | Round-cap share | 0.000 | **0.385** |

  The defensive half of the catalogue came alive on the first try, and it broke the game: two bots that both
  value survival heal faster than they hurt, and nearly two matches in five ran out of rounds. That is the
  honest result of pricing defence correctly in content authored for agents that ignored it. The measurements
  above are the first cut of the scorer; two review findings then changed its arithmetic, so treat them as the
  shape of the effect rather than as numbers to compare against later runs.
- **Two bugs caught in review, both real, both in the direction of overvaluing defence**:
  - Prevention was priced at a point per point of buff. Defense comes off a hit before the floor at zero, so
    a point past a hit's plain damage only reaches its critical branch: against a 3-damage spell at 3
    defense, a fourth point is worth 0.05, not 1. It is now read as the difference between the unbuffed and
    the buffed threat.
  - The denied kill was priced per outcome, and `guard` carries two defense buffs. When one point alone
    crossed the survival threshold the cast was paid `kill` twice; when survival needed both points it was
    paid none. The whole defensive reading now happens once per target, with a target's healing and all of
    its buffs read together, and the buffs priced on top of each other rather than each from the bare board.

  Both ship with a test that fails without the fix. Both were found by review, not by the suite, which is
  worth saying plainly: the six tests written with the feature all passed on the buggy code, because the test
  board has zero defense and one buff per cast — the two cases where the bugs are invisible.
- **What is next**: re-tune the catalogue against this scorer. The proposal that first accompanied this change
  was searched against the arithmetic the two findings corrected and has been discarded rather than kept.
