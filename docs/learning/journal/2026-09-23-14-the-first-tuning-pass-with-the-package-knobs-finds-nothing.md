# 2026-09-23. The first tuning pass with the package knobs finds nothing the noise does not: 8.35 to 2.39 on its own seeds, and the same content reads 4 to 11.5 on others

- **What ran.** `tune-content --seed 11 --iterations 6 --neighbours 6 --no-pairs` on content `4ab506fa` (30
  health, ADR 0068), the benchmark seeds, the knobs of `data/balance/knobs.json` including the 21 package
  initiative bonuses: 260 versions, 2080 evaluations, 3 h 37. The objective went from **8.35 to 2.39** with
  seven moves: `basic_attack` damage 2 to 1, `pummel` cost 1 to 2, `enraged_charge` cost 3 to 4,
  `chain_slash` critical 0.50 to 0.45, `mortal_wound` first damage 4 to 5, and the `ironbound` and
  `plague_doctor` initiative bonuses 1 to 0.
- **On seeds it never saw**, `score-content` on 200 seeds from 995317: **10.76 to 7.71**. Better, by half.
- **But one of the seven breaks its spell.** `mortal_wound` at 5 up front and 4 of bleed over its one round
  deals more now than later, against the first line of its own `keep` (Codex's review of #189). The `keep`
  lines are prose the search does not read, and the bounds allow it. Without that move the other six read
  **2.42** on the benchmark seeds, where the seven read 2.39, and **12.50** on the unseen ones, where the seven
  read 7.71 and the content as it stands 10.76: worse than not tuning. `tierUsageShare` alone goes from 0.885
  to 0.918 there, on a move that changed it by nothing on the seeds the search used.
- **Why: the objective is mostly noise at 200 seeds.** The content as it stands, `score-content` on disjoint
  blocks of consecutive seeds from 2,000,000:

  | seeds a block | blocks | objective, block by block | mean | spread (sd) |
  | --- | --- | --- | --- | --- |
  | 200 | 8 | 11.54, 7.11, 5.20, 7.02, 4.06, 4.65, 10.18, 7.56 | 7.17 | **2.62** |
  | 400 | 4 | 11.32, 14.05, 9.96, 12.47 | 11.95 | 1.74 |
  | 800 | 2 | 12.83, 10.74 | 11.79 | 1.48 |

  The benchmark seeds read 8.35 and the unseen ones 10.76, both inside that spread. Almost all of it is
  `tierWinSpread` (a penalty whose sd is 2.34 at 200 seeds) and `tierUsageShare` (0.65). A search that
  compares 260 candidates with a sd of 2.6 each keeps the luckiest: the best of 260 draws of pure noise sits
  about 2.8 sd, some seven points, below their mean, more than the 5.96 this pass found. A move
  does not change the catalogue so little that two versions share their matches either: a single step sends
  the matches down different paths, so the difference between two candidates is about as noisy as the
  difference between two seed blocks. The level moves with the block size too, 7 at 200 seeds against 12 at
  400 and 800, because both terms read a lower confidence bound that tightens as the matches add up.
- **Verdict.** Not applied. No move of this pass is separable from the noise it was selected on, and the
  one check that could tell (seeds it never saw) disagrees with itself by 4.8 when one move is dropped. What
  has to change is how the tuner reads a candidate, not the content, and that is the next change to it.
