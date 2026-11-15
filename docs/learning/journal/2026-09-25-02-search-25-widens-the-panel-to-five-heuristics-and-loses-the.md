# 2026-09-25. Search 25 widens the panel to five heuristics and loses the lookahead: the ladder needs the lookahead in it

- **What ran.** #209: `search-weights --kind heuristic` from `search-23`, against Greedy, `search-21`,
  `stun-first`, `search-19` and `pressure-floor` under the start's floor. The check was the lookahead with
  `lookahead-20`, played only in the replay. It ran 6 rounds of 10, seed 0, on content `0f036b75` (tuning pass
  11), and played 61 candidates in 2h02, 305 evaluations. It scored 0.8843 to 0.9058 on the seeds it was
  searched on.
- **What it found.** `stun` -2.961 to -6.731, `kill` 29.371 to 26.322, `damage` 0.051 to -0.187, `initiative`
  0.491 to 0.383, `energy` 0.344 to 0.281, `pressure` 0.639 to 0.583, `defense` 1.212 to 1.167, `bleed` 0.670
  to 0.648, `heal` 0.533 to 0.531. It is not added to `learning/weights`; these numbers are the whole file.
- **On seeds it never saw.** 200 seeds from 995317, content `0f036b75`. Agent A's score, average rounds, and
  share at the round cap:

  | against | the found set | `search-23` |
  | --- | --- | --- |
  | Greedy | 0.976, 15.4, 0.01 | 0.990, 15.1, 0.00 |
  | `search-21` | 0.998, 21.5, 0.19 | 1.000, 24.9, 0.10 |
  | `stun-first` | **0.983**, 16.0, 0.01 | 0.905, 18.2, 0.03 |
  | `search-19` | **0.914**, 29.6, 0.90 | 0.856, 29.1, 0.74 |
  | `pressure-floor` | 0.675, 17.3, 0.08 | 0.730, 18.1, 0.09 |
  | `search-23` | **0.943**, 29.2, 0.68 | |
  | the lookahead with `lookahead-20` (the check) | **0.043**, 21.2, 0.18 | 0.599, 27.8, 0.50 |
  | itself | 0.500, 30.0, **1.00** | 0.500, 28.7, 0.37 |

- **What it says.** It is not a rung. Against the heuristics it moved where the panel asked: it gains 8 points
  on `stun-first` and 6 on `search-19`, and it beats `search-23` itself 0.943, a set left out of the panel. But
  it falls 5.5 points against `pressure-floor`, inside the panel, where its floor was held only on the seeds it
  searched. And it collapses against the check, from 0.599 to 0.043: the lookahead takes 96 % of its games. Its
  own mirror reaches the cap in every match again.

  A wider panel of heuristics did what the last three searches did, one level up: it learned the heuristics
  and gave back the one agent that plays the round out.
- **Next.** The lookahead has to be in the panel, not only in the check. Run 22 ran out of the three hours
  with it in the panel. Two ways to afford it, for the owner:
  - run the search locally, where no time limit applies;
  - give the lookahead a cheaper seat in the panel, on fewer seeds than the heuristics.

  The stun weight is the other thing to read. It doubled toward "never stun", and a set that never stuns is
  the set whose mirror stalls.
