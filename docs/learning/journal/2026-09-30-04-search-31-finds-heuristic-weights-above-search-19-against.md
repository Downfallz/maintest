# 2026-09-30. Search 31 finds heuristic weights above search-19 against its panel, and they lose to search-19 itself

- **What ran.** #248: `search-weights --kind heuristic` from `search-19`, against greedy, kill-first, search-21,
  stun-first, pressure-floor and `lookahead:learning/weights/search-19.json@50`, all under the start's floor.
  5 rounds of 8, seed 0, on content `9659f610`. 41 candidates, 246 evaluations, 1h23.
- **What it found.** 0.8585 over the panel (0.832 to 0.885), from 0.7598 for the start: the best of 41
  candidates, on the seeds it was chosen on. Against search-19's weights: energy 0.237 to 0.549, defense 1.236
  to 1.416, kill 11.267 to 10.845, pressure 0.831 to 0.896, bleed 0.493 to 0.425, heal 0.391 to 0.362, stun
  9.048 to 9.128, initiative 0.612 to 0.647, damage 0.273 to 0.269. It values energy twice as much.
- **The hold-out.** The workflow's table goes to the run summary, so it was replayed by hand with the same
  commands: seeds 995317 to 995516, mirrored, the lookahead on every seed.

  | against | found | search-19 | paired difference |
  | --- | --- | --- | --- |
  | greedy | 1.000 | 1.000 | +0.000 |
  | kill-first | 0.959 | 0.525 | **+0.434** (+0.376 to +0.492) |
  | search-21 | 0.978 | 0.733 | **+0.245** (+0.199 to +0.292) |
  | stun-first | 0.927 | 0.912 | +0.015 (-0.010 to +0.040) |
  | pressure-floor | 0.995 | 0.988 | +0.008 (-0.006 to +0.021) |
  | `lookahead:search-19` | 0.367 | 0.270 | **+0.098** (+0.044 to +0.152) |
  | mean | 0.871 | 0.738 | |

- **And against search-19 itself**, which was not in its panel: 0.372 (0.326 to 0.419), in 24.46 rounds, on the
  same seeds.
- **What it says.** A rung against the panel it was asked about: above search-19 against kill-first, search-21
  and the lookahead, and not below it anywhere. It is not a better player than search-19 in every sense: head to
  head it loses almost two matches in three. The ladder is not transitive, as pressure-floor showed against
  search-19 under ADR 0083. These weights go in as `search-31.json` beside search-19, not in its place, and the
  next heuristic rung seats search-19 in its panel.
