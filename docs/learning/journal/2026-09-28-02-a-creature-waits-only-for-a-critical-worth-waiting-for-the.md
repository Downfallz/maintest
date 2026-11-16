# 2026-09-28. A creature waits only for a critical worth waiting for: the benchmark digest is regenerated

- **What changed.** ADR 0084. The heuristic's Speed was Quick only with a plain kill on the table, Standard
  otherwise, so a creature that could not crit waited for nothing. Now it is Standard only when the critical
  raises the best expected score of its castable spells under the agent's own weights. Every agent built on
  the heuristic inherits it.
- **How it was measured.** A temporary switch, never committed, put the old rule on one seat and a candidate on
  the other, the same weights on both. A control with the same rule on both seats read exactly 50 %. A marker
  in the weights themselves did not: a pressure of 1e-12 alone moved one side to 46 %, because the heuristic's
  exact ties decide real choices. Each candidate played 200 seeds mirrored, then the confirmation seeds.

  | weights | new rule, benchmark | new rule, confirmation | + Quick under lethal threat, vs new rule |
  | --- | --- | --- | --- |
  | greedy | 58.3 % (54.8 to 61.7) | 60.5 % (57.9 to 63.1) | 47.7 % |
  | search-21 | not run | 56.2 % (52.7 to 59.8) | 27.5 % |
  | stun-first | not run | 98.9 % | 50.0 % |
  | search-23 | 99.8 % | 99.8 % | 50.0 % |

  A margin on the critical made it worse with greedy weights: 47.5 % at 1 point, 3.0 % at 3. Lightning Bolt
  crits 62 % of the time and is cast most, so a margin gave that crit away.
- **The digest.** Content `813bb91b`, Greedy against Greedy, 200 seeds. 191 of the 200 AB outcomes moved and 96
  changed winner. Player1 wins 104 → 114, the round cap 1 → 5, average rounds 10.9 → 10.8, rounds 10 to 15
  109 → 95, health the winner kept 29.3 → 33.8.
- **What it says.** Weights that price a kill far above raw damage (search-23, stun-first) were kept waiting
  for a critical they barely valued, and lose almost every match to themselves going first. Every weights file
  was searched under the old Speed; the next search is fitted under this one. Mirrors under the new rule:
  greedy 10.8 rounds (2.5 % at the cap), search-23 18.1 (2.5 %), search-21 27.4 (32 %), stun-first 14.1 (0 %).
