# 2026-10-05. Search 34: the lookahead's own weights hold on unseen seeds against search-23, and tie Greedy

- **The run.** `search.yml` with `kind: lookahead`, from the built-in weights, against Greedy and `search-23` on
  the first 30 benchmark seeds (`learning/experiments/lookahead-seeds.json`), three rounds of five, no check
  opponent (#281). It took 3h33 of the six hours #280 allowed. The best of 32 evaluations scored 0.9167 where
  the built-in weights scored 0.9042, which is one or two matches out of 120, chosen as the best of 32.
- **What it moved.** `initiative` 2.1 to 2.5, `energy` 0.3 to 0.538, `heal` 0.8 to 1.006, `stun` 3.0 to 3.166,
  `defense` 0.65 to 0.734, `bleed` 0.8 to 0.561, `kill` 5.0 to 4.857, `pressure` 0 to 0.001. Damage unchanged.
- **Replayed on 60 seeds nothing had played** (the unseen block from 995377), both seats, against the bare
  `lookahead` on the same seeds, read with `paired`:

  | against | built-in | search 34 | paired difference |
  |---|---|---|---|
  | Greedy | 0.883 | 0.892 | +0.008 (−0.074 to +0.091) |
  | `search-23` | 0.896 | 0.967 | +0.071 (+0.007 to +0.135) |

  Even with Greedy, measurably above against `search-23`, which it beats 116 matches of 120 where the
  built-in weights take 107. The lower bound is close to zero: this is one replay of one search, not a margin.
- **Kept as `learning/weights/lookahead/lookahead-34.json`**, the values rounded to three decimals as replayed.
  A folder of its own because the table offers every file directly under `learning/weights/` as a heuristic
  seat, which is how `lookahead-20` came to be played by Greedy's reading; `search.yml` now writes a set for
  another reading into a folder named after it. The table seats the lookahead on these weights first, and the
  lookahead on its built-in weights second (`learning/seatable.json`).
- Content `e6f72578`, unchanged; no digest change.
