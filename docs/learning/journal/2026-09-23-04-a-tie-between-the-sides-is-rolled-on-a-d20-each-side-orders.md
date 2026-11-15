# 2026-09-23. A tie between the sides is rolled on a d20, each side orders its own, and the greedy mirror goes from 400 Player 1 wins to 190

- **What changed.** The Combat timeline no longer breaks a tie by Player slot, then Creature id (ADR 0063). A
  tie between the two sides is rolled off on a d20, which decides the places each side holds, and a player who
  holds two places in one tie orders their own creatures among them in a new sub-phase, `TieOrder`, before any
  Intent. A tie held by one side alone rolls nothing. The rolls draw on the match's random source, the one the
  critical rolls use, so every match with a tie between the sides plays differently from here on. Greedy,
  lookahead, minimax and policy agents keep the order the rolls left; random agents shuffle it. The content
  does not move. The engine does, and the digest for content `4d7a841c` is regenerated.
- **The mirror.** Greedy against greedy on the benchmark seeds: Player 1 won **400 of 400** before, and wins
  **190** now (210 to Player 2). It is still one match played 400 times: every entry ends by Elimination in 6
  rounds, 38 health to 0. What changed is who wins it. The dice decide now, where the seat decided before.
- **The objective**, on content `4d7a841c` with its own knobs and weights, the objective as it stood before
  ADR 0062, the benchmark seeds, before and after the rule:

  | reading | seat | d20 and own order |
  | --- | --- | --- |
  | score | 288.31 | **53.18** |
  | `mirror.player1WinShare` | 1.000 | 0.475 |
  | `variety.player1WinShare` | 0.495 | 0.480 |
  | `variety.tierWinSpread` | 0.379 | 0.511 |
  | `variety.tierUsageShare` | 0.921 | 0.934 |
  | `variety.averageRounds` | 7.82 | 7.42 |
  | `skill.player1WinShare` | 0.502 | 0.502 |
  | `exploit.winRateA` | 0.968 | 0.958 |

  The 243 points the seat was worth are gone. The largest remaining terms are `variety.tierUsageShare` (37.60)
  and `variety.tierWinSpread` (13.03). The second one grew: a package's win rate on the exploring run now
  carries the dice as well as the package. A first version of the rule rolled every tie, a side's own
  included; on content `6df8dc30` it read 58.63 against the seat's 285.77, the same move.
- **What it means for ADR 0062.** The mirror's seat reading is back in its band, but by chance: the mirror
  plays one board, and a coin now decides it. Reading the seat on the exploring run is still the reading with
  signal, so ADR 0062 stands. What no agent does yet is choose its own order well: they keep the rolls' order,
  and whether choosing better is worth anything is a measurement of its own.
