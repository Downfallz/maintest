# 2026-10-06. Why the clone lost: it never saw a board Greedy makes. And the weights are a cycle

All on content `ad3e4d00`, played locally.

- **Where the clone parts from its teacher.** A clone of search-23 was fitted on 2000 matches of its self-play,
  as in ci-247 (97.0 % on held-out steps). Its argmax was then compared with the decision search-23 actually
  made. Steps with one candidate are left out.

  | decisions of search-23 | in its self-play | against Greedy (1200 matches, both seats) |
  |---|---|---|
  | Evolve | 0.970 | 0.251 |
  | Intent | 0.971 | 0.639 |
  | Speed | 0.955 | 0.584 |
  | Targets | 0.989 | 0.631 |

  Against Greedy, search-23 buys other packages than it buys against itself (Dreadnought, Occultist, Lich),
  and the clone buys the self-play answer (Ironbound, Marauder, Shaman). The features are not what it lacks.
  It has never been shown a board Greedy makes, which is the ordinary failure of cloning from one opponent.
- **The remedy, measured on three dataset seeds.** A second clone was fitted on two kinds of data:
  700 matches of self-play, and search-23's own steps from 1200 matches against Greedy, 600 in each seat.
  This was done three times on separate seeds:
  - self-play seeds 1, 10001 and 20001;
  - Greedy seeds 900001/910001, 930001/940001 and 950001/960001.

  Each clone was played on the 200 benchmark seeds, which none was fitted on. The self-play-only clone is
  ci-247's, three seeds of 2000 matches on the same content, played on the same seeds (min / median / max):

  | clone | against Greedy | against search-23 |
  |---|---|---|
  | self-play only (ci-247, 2000 a seed) | 0.005 / 0.018 / 0.229 | 0.394 / 0.433 / 0.440 |
  | self-play (700) and against Greedy (1200) | **0.958 / 0.965 / 0.970** | 0.328 / 0.449 / 0.484 |

  Against Greedy the worst seed of the mixed clone is above the best seed of the self-play one. Against
  search-23 the two overlap, and the mixed clone's worst seed (0.328) is below the self-play clone's. The
  smaller self-play share costs something there, so the dataset has to mix opponents, not trade one for another. A turn that records the teacher against a panel, and fits only on
  the teacher's steps, would be the change; it is not made, because no policy is needed for play (below).
- **Memory.** Fitting on 923,815 steps (2000 self-play matches and the 1200 against Greedy) was killed by the
  kernel at 14 GB. That is the likeliest cause of the runner shutdown in ci-245, at 5000 matches.
- **The weights are a cycle.** Every weights file against search-23, then the sets that rival it against
  Greedy, all on the benchmark seeds:
  - pressure-floor takes **1.000** from search-23 and 0.124 from Greedy.
  - search-31 takes 0.497 from search-23 and 0.010 from Greedy.
  - search-2 takes 0.430 and 0.458.
  - stun-first takes 0.410 and 0.060.
  - Every other file takes 0.217 or less from search-23.

  search-23 takes 0.973 from Greedy. No weights file beats all three.
- **The lookahead on lookahead-34 does**, on 30 unseen seeds (60 matches each):

  | opponent | score | interval |
  |---|---|---|
  | Greedy | 0.983 | 0.95 to 1.00 |
  | search-23 | 0.900 | 0.83 to 0.97 |
  | pressure-floor | 0.917 | 0.85 to 0.98 |

  It is the one agent here no weights file exploits, which is why the table seats it first. The tuner keeps
  measuring with weights, both sides of the cycle included, because the lookahead is too slow for it.
- **The loop is put on a light turn.** It now records 300 matches a seed, not 2000, so a push to main costs
  minutes rather than an hour and a half.
