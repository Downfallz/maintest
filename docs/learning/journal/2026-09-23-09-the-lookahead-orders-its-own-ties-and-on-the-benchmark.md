# 2026-09-23. The lookahead orders its own ties, and on the benchmark seeds it changes nothing: every seating ends the round alike

- **What changed.** The lookahead and minimax agents used to keep a tie in the order the roll-off left it
  (ADR 0063). They now play every seating of their own tied creatures out from the first slot, with no intent
  declared on either side, and keep the seating whose round ends best, the order as rolled on a tie
  ([agents.md](../agents.md#the-round-played-out)). Greedy, heuristic and policy agents still keep the roll. The
  content and the engine do not move, and neither does the digest, which is the greedy mirror.
- **The measurement.** Lookahead against three opponents on the benchmark seeds, the same 400 mirrored matches,
  on content `4d7a841c`, `main` against this change, read seed by seed with `paired`:

  | opponent | before | after | paired difference | tie orders asked | moved from the roll |
  | --- | --- | --- | --- | --- | --- |
  | greedy | 0.7550 | 0.7550 | +0.0000 | 800 | 0 |
  | random | 0.9988 | 0.9988 | +0.0000 | 1609 | 4 |
  | stun-first | 0.3150 | 0.3150 | +0.0000 | 1611 | 0 |

  Measured before ADR 0066, with stacked picks. Played again on `main` once ADR 0066 was in (one package a
  creature an opportunity, longer matches), the same three read **0.3825, 1.0000 and 0.9925** before and after,
  the paired difference exactly zero each time: the rule change moved the lookahead's standing a long way,
  from beating greedy to losing to it and from losing to stun-first to beating it, and the tie order still
  moved nothing.

  The minimax agent against greedy, under stacked picks: 800 asked, none moved. The scores and the paired differences replay with
  `evaluate --p1 lookahead --p2 <opponent> --seeds benchmarks/benchmark-seeds.json` on each build and
  `paired`; the two right-hand columns were counted with a temporary trace that is not in the change.
- **Why nothing moves.** Every tie against greedy falls in round 1 or 2, while the two teams, the same roster,
  still mirror each other: both seatings are worth exactly 0, since each side deals the other the same damage
  whichever creature of a tie acts first. Against the other two opponents the seatings are worth
  something, but the same for both. A tie order only matters when one creature of it kills, stuns or buffs
  before the other acts, and ties are rare by the time a creature is low enough to be killed in one slot: the
  purchases have spread the initiatives by then. The 4 seatings that did move were in rounds 5 and 6 of
  matches the lookahead was already winning, and changed no result.
- **What this licenses.** The tie order a player now gives is a real decision at the table: a human can see
  what the rollout sees, and the rule costs the engine nothing. For the agents it is worth nothing on this
  catalogue, so it cannot explain any gap between two of them, and the weight search in #181, whose heuristic
  keeps the roll, loses nothing by it. It would start to matter with content that ties late, or a roster whose
  first rounds can kill.
