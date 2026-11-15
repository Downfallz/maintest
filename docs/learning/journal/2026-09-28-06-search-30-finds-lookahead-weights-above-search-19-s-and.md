# 2026-09-28. Search 30 finds lookahead weights above search-19's, and they hold on 200 seeds they never saw

- **What ran.** #230: `search-weights --kind lookahead` from `search-19`, against search-19, stun-first,
  greedy and pressure-floor, all under the start's floor. 3 rounds of 8, seed 0, on content `813bb91b`.
  25 candidates, 100 evaluations, 2h49.
- **What it found.** 0.9213 over the panel (0.898 to 0.945), from 0.8253 for the start. The weights, against
  search-19's: energy 0.237 to 0.039, stun 9.048 to 9.663, defense 1.236 to 1.419, initiative 0.612 to
  0.764, heal 0.391 to 0.450, kill 11.267 to 11.018, pressure 0.831 to 0.785, bleed 0.493 to 0.516, damage
  0.273 to 0.280. The lookahead nearly stops valuing energy.
- **The hold-out.** The workflow's replay was cut by its three-hour limit, so it was run by hand with the same
  commands: seeds 995317 to 995516, mirrored, both sets played as the lookahead, same content. The start's
  column is `lookahead:search-19`.

  | against | found | start | paired difference |
  | --- | --- | --- | --- |
  | search-19 | 0.733 | 0.713 | +0.020 (+0.001 to +0.040) |
  | pressure-floor | 0.948 | 0.780 | +0.168 (+0.108 to +0.228) |
  | stun-first | 0.975 | 0.920 | +0.055 (+0.026 to +0.084) |
  | greedy | 0.985 | 0.953 | +0.033 (+0.004 to +0.061) |
  | mean | 0.910 | 0.841 | |

- **What it says.** A rung. The found set is above the start against every opponent on seeds it never saw,
  and not below it anywhere. The gain is mostly against pressure-floor, the heuristic that beats search-19.
  Against search-19 itself it is small, and its interval barely clears zero. The search's own 0.9213 was the
  best of 25 candidates (100 evaluations, four opponents each), and the hold-out's 0.910 is close to it.
  These weights go in as `lookahead-30.json` in their own pull request.
