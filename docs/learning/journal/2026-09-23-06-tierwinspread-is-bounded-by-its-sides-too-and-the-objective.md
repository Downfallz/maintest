# 2026-09-23. `tierWinSpread` is bounded by its sides too, and the objective goes from 23.36 to 11.11: what is left is two real findings

- **What changed.** `tierWinSpread` reads the lower bound of the 95 % Newcombe interval of the gap between two
  win shares of a package, zero where the sides cannot tell it from chance, instead of the raw gap
  (ADR 0065). The band, the content and the engine do not move.
- **Why.** Eight packages are read, most on a few dozen sides. Drawn 2000 times at those side counts with each
  pair truly winning alike, the worst raw gap reads 0.254 at the median and 0.399 at the 90th percentile,
  against a band of 0.15: 1 to 6 points of chance in every candidate's score. The bound reads 0.000 and 0.061.
- **The packages**, exploring run of content `4d7a841c`, benchmark seeds:

  | package | sides | win shares | raw gap | bound |
  | --- | --- | --- | --- | --- |
  | `blightweaver` | 22 / 27 | 0.659 / 0.148 | 0.511 | **0.238** |
  | `warmonger` | 8 / 24 | 0.375 / 0.667 | 0.292 | 0 |
  | `deathstalker` | 37 / 9 | 0.514 / 0.278 | 0.236 | 0 |
  | `soulreaver` | 19 / 136 | 0.895 / 0.695 | 0.200 | 0 |
  | `prowler` | 206 / 364 | 0.663 / 0.518 | 0.145 | 0.061 |
  | `occultist` | 232 / 135 | 0.522 / 0.556 | 0.034 | 0 |

- **The objective**, one `score-content` run: **23.36 to 11.11**. `tierWinSpread` goes from 13.03 to 0.78. What is
  left is two findings the samples prove: `tier:soulreaver:v1` sells a spell that lands 19 of its 286 casts (7.77) and
  `tier:blightweaver:v1` sells `infectious_blast`, which wins 0.148 of the sides that declare it against
  `tranquilizer_dart`'s 0.659 (0.78). Both are content questions, which is what a tuning pass should now be
  reading.
