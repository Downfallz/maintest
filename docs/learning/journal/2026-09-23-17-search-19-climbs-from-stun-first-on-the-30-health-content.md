# 2026-09-23. Search 19 climbs from `stun-first` on the 30-health content, and takes every match from its panel on seeds it never saw

- **The run.** `Search the agent weights` run 19 (#188), report-only: kind `heuristic`, starting from
  `stun-first`, panel Greedy, `stun-first` and `pressure-floor` with a candidate ranking last if it fell below
  the start against any, check the built-in lookahead, 10 rounds of 16, seed 0, content `4ab506fa`, the
  benchmark seeds. The score went from **0.537 to 1.000**: the found set won every one of the 1 200 matches.
- **What it changed.** `kill` 9.64 to 11.27, `stun` 10.21 to 9.05, `initiative` 1.19 to 0.61, `energy` 0.77
  to 0.24, `damage` 0.45 to 0.27, `bleed` 0.22 to 0.49, `defense` 0.97 to 1.24, `pressure` 0.58 to 0.83, `heal`
  0.32 to 0.39. Committed as `learning/weights/search-19.json` at the three decimals the run printed, which are
  the numbers played below.
- **On seeds it never saw**, 200 from 995317, the found set and `stun-first`, each against the same opponents:

  | opponent | `search-19` | `stun-first` |
  | --- | --- | --- |
  | Greedy | **1.000** (11.3 rounds) | 0.889 (14.4) |
  | `stun-first` | **1.000** (27.2) | 0.500 (30.0) |
  | `pressure-floor` | **1.000** (26.8) | 0.229 (30.0) |
  | lookahead (check) | 0.780 | 0.818 |
  | `search-4` | 0.996 | — |
  | Random | 1.000 | — |

  Against the check the two differ by -0.0375, anywhere from -0.0964 to +0.0214 read seed by seed with
  `paired`: no difference either way. It takes 0.225 of its matches against `stun-first` and 0.16 against
  `pressure-floor` to the round cap, where those two take all of theirs against each other (the entry above).
- **Verdict.** A rung: it beats every member of its panel on unseen seeds, holds the check, and is added to
  `learning/weights/`. It is not added to the tuner's exploit panel here, which would move the objective.
  The next rung needs it in the panel, since this one's panel is now saturated at 1.000.
