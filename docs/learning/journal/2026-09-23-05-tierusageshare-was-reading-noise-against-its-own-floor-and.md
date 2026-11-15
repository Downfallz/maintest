# 2026-09-23. `tierUsageShare` was reading noise against its own floor, and the objective goes from 53.18 to 23.36 with the content unchanged

- **What changed.** `tierUsageShare` reads the lower bound of each package's top-share 95 % Wilson interval
  instead of the raw share, against a band of 0.8 at scale 0.05 instead of 0.5 at 0.1 (ADR 0064). The content
  and the engine do not move.
- **Why.** Every package that teaches more than one spell teaches two, so 0.5 is the floor of each package,
  and the band asked the eleven packages that were cast to split perfectly at once. It was also the worst of
  eleven raw shares, several of them read on 15 or 16 casts. Drawn 2000 times at the observed sample sizes,
  with every pair truly splitting 50/50, the worst raw share reads 0.667 at the median and 0.750 at the 90th
  percentile, a floor of 5.6 points no move could remove. The Wilson bound reads 0.497 at the median.
- **The packages**, on the exploring run of content `4d7a841c`, benchmark seeds, landed casts of the
  most-cast spell:

  | package | casts | raw | bound |
  | --- | --- | --- | --- |
  | `soulreaver` | 267 / 286 | 0.934 | **0.899** |
  | `deathstalker` | 109 / 118 | 0.924 | 0.861 |
  | `prowler` | 1760 / 2057 | 0.856 | 0.840 |
  | `warmonger` | 53 / 61 | 0.869 | 0.762 |
  | `harbinger` | 12 / 16 | 0.750 | 0.505 |
  | `occultist` | 417 / 622 | 0.670 | 0.633 |
  | `brute` | 62 / 110 | 0.564 | 0.470 |

- **The objective**, from one `score-content` run read both ways: **53.18 to 23.36**. `tierUsageShare` goes
  from 37.60 to 7.77, all of it `soulreaver`, a real monopoly on 286 casts. `tierWinSpread` is now the largest
  term at 13.03. It is also the worst of eleven small samples, and its noise floor is the next thing to
  measure before any tuning pass trusts it.
