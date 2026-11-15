# 2026-09-16. Under the floor of what it started from, the search finds no neighbour of `mixture-mean` at all: every gain against one opponent is paid to `search-4`

- **The search**: `mixture-mean` as the start, the mean under the start's floor as fitness (the constrained
  mean that replaced the worst matchup after the rung-2 entry of this day), against `greedy`,
  `mixture-worst`, `search-4` and `random` at once, ten rounds of sixteen on the benchmark seeds, 644
  evaluations in 55 minutes, on content `7e199df4` and engine `9e272ab25531`, built in the branch's worktree
  before its commit (hence its dirty mark; no C# changed between that commit and `main`, so the engine is
  `main`'s), into `runs/search-mixture-3/` locally. The start on those seeds: 0.884 against Greedy, 0.4925
  against `mixture-worst`, 0.705 against `search-4`, 0.9925 against Random, mean 0.7684; its floors, the low
  end of each interval: 0.853, 0.471, 0.664, 0.984.

- **Not one of the 159 candidates held above all four floors.** The start is the only conforming set of the
  run, so the best is the start and nothing was written. The shortfalls: the least 0.024, the median 0.129,
  twelve within 0.05 of holding; the spread of the draw went from 0.67 of each weight in round 1 to 0.13 by
  round 10, so the search narrowed around the least-falling sets, as it does, and found none that held
  there either.

- **Which floor, replayed.** The search keeps one score per candidate and not its parts, so the three
  least-falling sets (rounds 6, 7 and 9; stun 5.1 to 5.3 against 4.06, bleed negative against 0.50, heal
  0.22 to 0.26 against 0.53, the rest within a tenth) were replayed on the same seeds, on engine
  `5fd061112bbb` and the same content; the three scores against Random reproduce the start's to the digit:

  | on the searched seeds | `mixture-mean` (floor) | round 6 | round 7 | round 9 |
  | --- | --- | --- | --- | --- |
  | against Greedy | 0.884 (0.853) | 0.870 | 0.871 | 0.894 |
  | against `mixture-worst` | 0.4925 (0.471) | **0.565** | **0.5475** | **0.5475** |
  | against `search-4` | 0.705 (0.664) | *0.640* | *0.640* | *0.640* |
  | against Random | 0.9925 (0.984) | 0.9925 | 0.9925 | 0.9925 |

  All three fell against `search-4`, by the same 0.024 below its floor and 6.5 points below the start, and
  all three gained 5.5 to 7 points against `mixture-worst`. Their mean is the start's to within a point: the
  neighbourhood trades one strong opponent for the other, and the floor refused the trade, which is exactly
  what it was built to refuse. The plain mean would have taken it, and the entry after would have read a set
  that beats `mixture-worst` and lost a rung against `search-4`.

- **What this says about the ladder, and about the floor.** From `search-4`, the plain mean found a set 25
  points better against `search-4` at 5 points against Greedy (0.864 against 0.914, the three-opponent
  entry): the first rung was a trade too, and one this floor would have refused, since 0.864 is under the
  low end of `search-4`'s interval against Greedy. So the floor keeps a rung from being lost and also keeps
  one from being climbed when climbing costs an opponent, and what this run measures is the neighbourhood
  under that rule: from `mixture-mean`, within two thirds of each weight and then within an eighth, no set
  improves any opponent without paying another beyond the noise of 400 matches. Another start, a wider
  draw or a looser floor are not excluded by 159 candidates around one point; what is excluded is a third
  rung near `mixture-mean` on this panel at this cost, which is where the rung-2 entry ended too, from the
  worst-matchup side: two fitnesses, two searches, the same neighbourhood empty.

- **What this licenses.** Another search of the same neighbourhood is the one thing this run says not to
  buy. The cheaper next question for the heuristic is what the weights weigh: a feature the score does not
  see (the weakest enemy's distance to a kill, the opponent's energy, initiative relative to the target),
  measured one at a time on fixed content with `sweep-weight.py` (ADR 0037) before any search sees it. A
  search from `search-4` under the floor, or from `mixture-mean` with a floor set a few points under the
  start, would say whether the rule or the region is what stopped this one. And the search itself should
  keep every candidate's parts in `search.json`, so that the next entry can name the floor a search fell
  against without replaying its best fallers.
