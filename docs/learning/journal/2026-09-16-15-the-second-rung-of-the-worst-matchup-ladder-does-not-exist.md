# 2026-09-16. The second rung of the worst-matchup ladder does not exist: with its start in the panel the search wanders, and with it out the search flattens

- **Two searches from `mixture-mean.json`**, the set that beats `search-4` by 25 points on unseen seeds, each
  10 rounds of 16, seed 0, worst-matchup fitness, content `7e199df4`. The first against `greedy`,
  `mixture-mean`, `search-4` and `random`: the start is in the panel, so its own fitness is 0.5 by
  construction (it draws itself) and a candidate has to beat the set it came from. The second against
  `greedy`, `mixture-worst`, `search-4` and `random`: the start out of the panel, so the baseline reads its
  real worst matchup, 0.4925 against `mixture-worst`, and there is room above it.

  | on the searched seeds | fitness, start | fitness, best (round) | best against Greedy | `search-4` | the fourth |
  | --- | --- | --- | --- | --- | --- |
  | start in the panel | 0.5000 | 0.5225 (3) | 0.8263 | 0.6713 | 0.5225 against `mixture-mean` |
  | start out of the panel | 0.4925 | 0.5525 (9) | 0.6012 | 0.5525 | 0.5525 against `mixture-worst` |
  | `mixture-mean` itself | | | 0.8838 | 0.7050 | |

- **On 200 seeds no candidate saw** (995317 to 995516, mirrored), the best of each:

  | agent A | against Greedy | against `mixture-mean` | against `mixture-worst` | against `search-4` |
  | --- | --- | --- | --- | --- |
  | best, start in the panel | 0.8187 | 0.4750 (0.436 to 0.514) | | 0.6850 |
  | best, start out of the panel | 0.6138 (0.555 to 0.673) | 0.5250 (0.461 to 0.589) | 0.5625 (0.499 to 0.626) | 0.5525 (0.489 to 0.616) |
  | `mixture-mean` | 0.8638 | 0.5 (itself) | 0.4725 | 0.7462 |

- **With the start in the panel, the search wanders.** After round 3 every round's best sat between 0.47 and
  0.49, below the start; the best it kept loses to `mixture-mean` on unseen seeds (0.475, the interval
  through one half) and is worse than it against Greedy and `search-4`. A fitness that reads 0.5 for the
  start and asks the elite to beat it is a fitness whose signal is the noise of 400 matches against oneself.

- **With the start out of the panel, the search flattens.** The worst matchup rose, 0.49 to 0.55, and it rose
  by bringing every matchup down to it: against Greedy 0.864 to 0.614, against `search-4` 0.746 to 0.553,
  and on unseen seeds every one of its four intervals against a real opponent contains one half. It is an
  equalizer, not a stronger player: `mixture-mean` is 25 points better against Greedy and 19 against
  `search-4`, and the one thing this set does better, its matchup against `mixture-worst`, is 0.5625 with
  0.499 at the bottom of the interval. The worst matchup, optimised, finds the set that loses to nobody by
  much and beats nobody by much.

- **So the worst-matchup ladder has one rung, and the mean found it.** The entry below reads the first
  search: the mean fitness climbed every round and found `mixture-mean`, the worst matchup peaked at round
  2 and found `mixture-worst`, and the change kept the worst matchup because a mean can be won by one
  opponent. Both are true and the second is the one that matters for a ladder: a mean can climb, and it
  did not learn one opponent when it was given three. What a second rung needs is a fitness that climbs
  and cannot be won by one opponent, which is the mean under the constraint that no matchup falls below
  the start's. That is a change to `Score.mixture`, and it is the next one. No weights file from either
  search: neither is a better teacher than `mixture-mean`.

- **One number kept.** The searches cost 45 and 85 minutes for 161 evaluations of three and four; the
  hold-out five. The next rung is not a matter of budget.
