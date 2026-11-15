# 2026-10-04. A defense debuff is priced by the damage it lets through (ADR 0098)

- **Why.** The owner saw Greedy and the lookahead cast Infectious Blast on a team with no defense and energy to
  spare. Its -3 defense for good was priced 0.65 x 3 x 3 = 5.85 a target whatever the target held, the largest
  reading in the catalogue for one energy.
- **What.** A defense debuff is now the threat on the target with the defense it takes off gone, less the threat
  on it now, over its rounds, shared across the target's living team, the mirror of the buff. It takes off at
  most what the target holds.
- **Measured** on the 200 benchmark seeds, content `e6f72578`, old scorer against new:

  | Match | Score, old to new | Infectious Blast | Noxious Cure |
  | --- | --- | --- | --- |
  | Greedy against Greedy | 0.500 to 0.500 | 32 to 2 | 70 to 137 |
  | Greedy against `search-19` | 1.000 to 1.000 | 12 to 28 | 2 to 2 |
  | Greedy against `search-31` | 0.993 to 0.993 | 26 to 30 | 48 to 76 |
  | Greedy against Random | 1.000 to 1.000 | 11 to 16 | 53 to 67 |
  | Lookahead on `search-19` against Greedy | **0.440 to 0.532** | **861 to 216** | 661 to 937 |

  The lookahead's gain is +0.092 ± 0.049 read seed by seed. Declarations count both sides. Against
  `search-19` and `search-31` the count rises, since those weights were fitted to the old price and a debuff is
  now cast where it takes defense off. Matches with the lookahead run 12.0 rounds instead of 11.6.
- **`check-knobs` and the studio** read a spell with no board and no defense, so a defense debuff now reads
  zero there too. Infectious Blast went from 23.55 a round to **6.00**, its 2 damage on three enemies, and
  Noxious Cure from 18.44 to 22.34, the largest reading at tier 2 now that its debuff on its allies costs nothing.
- **The weights fitted for the lookahead** (`lookahead-5`, `lookahead-20`, `lookahead-30`) are removed from
  `learning/weights/`. The table offered them as heuristic players under their file names, so a seat named
  `lookahead-20` played Greedy's reading with weights fitted for another one, and every one of them had collapsed
  on the content it was last measured on. The lookahead itself is still offered, on `search-19`'s weights.
- Benchmark digest regenerated on `e6f72578`.
