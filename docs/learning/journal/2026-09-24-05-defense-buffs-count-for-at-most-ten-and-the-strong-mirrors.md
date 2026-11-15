# 2026-09-24. Defense buffs count for at most ten, and the strong mirrors still stall: 10 still stops every plain hit

- **What changed.** ADR 0076: a creature's defense buffs, permanent and timed together, add at most 10 to its
  total defense. The scorer prices a buff only for the points still under the ceiling. Greedy's mirror and
  the benchmark digest do not move.
- **The measurement.** 200 seeds from 995317, content `4ab506fa`. The first column is `main` without the
  ceiling. "10, blind" is the ceiling in the engine with a scorer that does not know it; "10" and "5" have
  the scorer aware. Each cell gives average rounds, the share at the round cap and, where it matters, the
  share of draws:

  | mirror | no ceiling | 10, blind | 10 | 5 |
  | --- | --- | --- | --- | --- |
  | Greedy | 9.96, 0.000 | 9.96, 0.000 | 9.96, 0.000 | 9.96, 0.000 |
  | `stun-first` | 30.00, 1.000 | 29.67, 0.750 | 29.57, 0.580 | 21.76, 0.035 |
  | `search-19` | 29.98, 0.970 | 27.61, 0.135 | 29.19, 0.555 | 23.61, 0.035 |
  | `search-21` | 27.6, 0.670 | 21.52, 0.065, draws 0.295 | 30.00, **1.000**, draws 0.150 | 19.16, 0.105 |
  | `pressure-floor` | 22.29, 0.010, draws 0.820 | 21.96, 0.005, draws 0.820 | 22.02, 0.005, draws 0.710 | 24.11, 0.115, draws 0.020 |
  | the lookahead with `lookahead-20` | 24.29, 0.320 | 24.28, 0.315 | 24.27, 0.310 | 24.26, 0.275 |

  Head to head at 10: `search-21` still beats Greedy (1.000), `stun-first` (1.000) and `search-19` (0.810),
  and plays `lookahead-20` even (0.500). At 5, `search-19` holds it (0.481) and `lookahead-20` takes every
  match from it.
- **What the traces say at 10.** Twenty traced matches of each mirror:
  - `search-21` reaches the ceiling by round 10 and holds it. No hit lands after that. It bleeds, 110 points
    a match in rounds 11 to 20 and 224 in rounds 21 to 30. But `restorative_gush` is half its actions, and
    health stays at 30 until round 25.
  - `stun-first` and `search-19` also sit at 9 to 10 defense. After round 10 `crushing_stomp` is about half
    their actions, and a fifth to a third of all actions fizzle on a stunned caster. `stun-first` casts its
    first bleed after round 20. Hits land more than without the ceiling (38 to 51 damage in ten rounds, against
    14 to 38), so these matches are slow rather than frozen: about a third of the creatures are dead by round
    30.
- **What it says.** Every printed hit is 10 or less, most of them 2 to 7, and a critical doubles it. So 10
  defense stops every hit that is not a critical. At 5 they land and the stalls end. "10, blind" ended more of them than "10" only by accident: the bots kept wasting
  turns on buffs that added nothing. The owner kept 10 (2026-09-24). What remains is heal against bleed for
  `search-21`, and the stun trade for the older sets.
- **Next.** A weight search under the ceiling, and the owner's call on healing against bleeding.
