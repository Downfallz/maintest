# 2026-09-24. Search 20 fits the lookahead's weights on the 30-health content, and they beat the built-in reading on seeds it never saw

- **What ran.** The rung #187 asked for: `search-weights --kind lookahead` from the built-in weights, against
  Greedy, `stun-first` and `pressure-floor`, 4 rounds of 10, seed 0, on the benchmark seeds. The content was
  `4ab506fa`, before the stun immunity (ADR 0072) and the bleed price (ADR 0073). It played 41 candidates in
  2h23 and scored 0.3104 to 0.9867 on the seeds it was searched on, the best of 123 evaluations.
- **What it changed.** `kill` 5.00 to 7.94, `stun` 3.00 to 5.51, `heal` 0.80 to 1.58, `bleed` 0.80 to 1.33,
  `defense` 0.65 to 0.85, `pressure` 0 to 0.17, `damage` 1.00 to 0.73, `energy` 0.30 to -0.29, and
  `initiative` stays at 2.10. It is the first set with a negative weight on energy: it spends rather than
  banks.
- **On seeds it never saw.** 200 seeds from 995317, on `main` at `be89c0e`, so under the two rules it was not
  searched under. Both are played as the lookahead. The first two columns give agent A's score, and the last
  gives the difference paired seed by seed, with its interval:

  | against | `lookahead-20` | built-in lookahead | difference |
  | --- | --- | --- | --- |
  | Greedy | 0.975 | 0.541 | +0.434 (+0.384 to +0.483) |
  | `stun-first` | 0.700 | 0.560 | +0.140 (+0.063 to +0.217) |
  | `pressure-floor` | 1.000 | 0.458 | +0.543 (+0.490 to +0.595) |
  | `search-4` | 0.760 | 0.661 | +0.099 (+0.024 to +0.174) |
  | `search-19` | 0.370 | 0.338 | +0.033 (-0.053 to +0.118) |

  None of these matches reach the round cap in more than 0.12 of them, and the found set's matches last 14 to
  19 rounds.
- **What it says.** A rung for the lookahead. It holds on rules it was not searched under, and it is measurably
  better than the built-in reading against every opponent but `search-19`, where the interval covers zero.
  `search-19` still beats both, so the lookahead's rung is not yet the ladder's top. Added as
  `learning/weights/lookahead-20.json`.
- **Not done.** The workflow's own hold-out table is in the run's step summary, which this session cannot
  read. The replay above takes its place and was made on the rules as they are now.
