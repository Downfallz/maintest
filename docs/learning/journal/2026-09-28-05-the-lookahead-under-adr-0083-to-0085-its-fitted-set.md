# 2026-09-28. The lookahead under ADR 0083 to 0085: its fitted set collapsed, and search-19's weights lead

- **What was measured.** Before its rung, the lookahead with each candidate start against the heuristics that
  matter now, on the 200 benchmark seeds mirrored, content `813bb91b`:

  | the lookahead with | search-19 | stun-first | greedy | pressure-floor | search-23 | search-21 |
  | --- | --- | --- | --- | --- | --- | --- |
  | `lookahead-20` | 0.087 | 0.142 | 0.380 | 0.560 | 0.615 | 0.660 |
  | the built-in weights | 0.000 | 0.090 | 0.547 | | | |
  | `search-19` | **0.738** | **0.900** | **0.943** | **0.715** | | |

- **What it says.** `lookahead-20` was fitted to the two-pass combat, where the lookahead replayed revealed
  actions; under the new rules it loses to search-19, stun-first and greedy, and holds only against
  pressure-floor, search-23 and search-21. Played with search-19's weights, the lookahead
  is the strongest agent measured, above search-19 played as the heuristic. The next lookahead rung starts there.
  Matches against search-23 and search-21 run 26 to 28 rounds (38 % and 60 % at the round cap) and cost three
  times the others, so they stay out of that rung's panel.
