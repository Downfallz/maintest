# 2026-09-16. The worst reply plays worse than the guessed one: minimax is a floor, and floors lose here

- **`minimax` is the lookahead agent with every enemy slot still ahead played as the reply that costs the
  actor most**, among the spells that enemy can cast, one enemy at a time in timeline order. It is what
  "lookahead" promises when the opponent's move is unknown: search over the reply instead of guessing it.
  The expectation, written before the run and in the agent's own summary: at or below the lookahead's 0.458
  against Greedy, because a floor reads the round against an opponent who sees the move and Greedy does not
  see it; and the number to beat against `search-4` was 0.200. Benchmark seeds, mirrored, content `7e199df4`:

  | Minimax against | win rate | 95 % interval | the lookahead on the same seeds |
  | --- | --- | --- | --- |
  | `Greedy` | 0.427 | 0.378 to 0.477 | 0.458 |
  | `search-4` | **0.083** | 0.053 to 0.112 | 0.200 |
  | `Lookahead` | 0.545 | 0.512 to 0.578 | — |

  Below the lookahead against Greedy, inside the intervals; **eleven points below it against `search-4`**,
  well outside them; and a winner head to head against the lookahead, which is the non-transitivity this
  journal keeps meeting and not a result. At 83 s for the 400 matches against 16 s, five times the cost.

- **Why a floor loses.** Against Greedy the guessed reply is Greedy's own rule, so the guess is nearly the
  truth and the floor is strictly more pessimistic than the truth: the agent defends against answers that
  never come. Against `search-4` the floor is worse still, because `search-4` plays the aggressive move and
  the floor spends the actor's slot on the defensive one; 0.083 is a defender being run over. The minimax
  reading is right for a game where the opponent answers the move. Intents here are declared at the same
  time, and the reply that costs most is the one the opponent did not see to play.

- **What the mechanism does, verified apart from the win rate.** `Replies` on the agent hands out the spell
  it took each other creature to play. On a board where the enemy can Rend the actor, which its own reading
  prices above everything, or Strike the ally that has declared a Rend of its own, the lookahead takes the
  enemy at Rend and the minimax takes it at the Strike, because the kill costs the round the ally's action on
  top of the ally. That is the difference between a guess and a floor in one test, and it is not a
  difference that wins matches.

- **Where this leaves ADR 0047's route.** Two agents built on `Advance`, both measured, neither better than
  Greedy: the guess reads the round the way the opponent plays it and adds nothing measurable, the floor
  reads it the way no opponent plays it and loses. What has not been tried is the reading with its own
  weights: `search-weights` plays `heuristic:<file>` and would have to play `lookahead:<file>` to find out
  whether a rollout has a different ceiling from a step, and that is the one experiment left on this route
  before the honest reading is that on this game the strength is in the evaluation and not in the horizon.
  Both agents stay in the catalogue as kinds that play nothing by default; the digest is unchanged.
