# 2026-09-28. A denied kill reads the enemies still to act: the benchmark digest is regenerated again

- **What changed.** ADR 0085, on top of ADR 0084. The scorer pays the kill price for a heal or a buff that saves
  a creature from a lethal round (ADR 0022). "Lethal" summed the best hit of every living enemy; it now sums
  only the enemies whose slot comes after the cast on the timeline, when the agent has one. This is the
  "survival mode" the owner asked for, made to fire when the danger is real.
- **How it was measured.** The same uncommitted two-seat switch as ADR 0084, whose control read exactly 50 %.
  Win rate of the new reading against the old, same weights on both seats:

  | weights | denied kill only, benchmark | denied kill only, confirmation | + prevented damage, benchmark |
  | --- | --- | --- | --- |
  | greedy | 56.8 % (52.1 to 61.4) | 59.6 % (56.5 to 62.7) | 56.5 % (prevented alone 49.0 %) |
  | search-23 | 52.2 % (50.7 to 53.8) | 55.6 % (54.0 to 57.2) | 60.0 % (prevented alone 56.8 %) |
  | search-21 | 51.2 % | 50.5 % | 51.7 % (prevented alone 44.8 %) |
  | stun-first | 50.0 % | 50.4 % | 46.0 % (prevented alone 47.7 %) |

  The denied kill alone never lost; the prevented damage read the same way was mixed and stays as it was.
- **The digest.** Content `813bb91b`, Greedy against Greedy, 200 seeds, against the ADR 0084 digest: 177 of the
  200 AB outcomes moved and 83 changed winner. Player1 wins 114 → 105, the round cap 5 → 0, average rounds
  10.8 → 9.8, rounds 10 to 15 95 → 82, health the winner kept 33.8 → 30.1.
- **What it says.** A heal on a creature the rest of the round cannot kill is no longer paid as a rescue, so the
  agent heals less for nothing and hits more: matches get shorter. The weights were searched under the old
  reading, and the next search is fitted under this one.
