# 2026-10-07. The lookahead can read a combat move past its round. Measured, the rounds after a move buy nothing yet

All on content `9419f935`, weights `lookahead-34` (`@123c9a0d`) in both seats unless named, 20 seeds mirrored
(40 matches) from `runs/review/seeds20.json` unless named, played locally on four cores.

- **What changed.** `lookahead:<rounds>x<rollouts>[:<weights>|:<agent>]` (and `minimax:...`) reads a combat
  move -- a target, an intent, a tie order -- the way a purchase is read (ADR 0094): the rest of the round as
  before, then the next rounds rolled out whole (`RoundRollout.Continue`), every decision in both seats the
  one-step agent's, on dice derived from the agent's seed and the decision, so that every candidate of one
  decision is read on the same rolls. A bare `lookahead` reads the round alone, as every spec written before
  it does, and the benchmark digest does not move.
- **What is read off the rounds after: the match they end, and nothing else.** A candidate whose rollouts end
  in a win outranks every other, one whose rollouts end in a loss ranks below, and between candidates that end
  nothing the one-round reading decides. Three valuations were measured on the way, each against the
  one-round lookahead-34 on the same weights:

  | the rounds after a move count as | depth | matches | wins of the deep reading | Focus casts, deep / one-round |
  |---|---|---|---|---|
  | the outcome, then the round's score plus the rollouts' summed scores | 2x2 | 80 | 11 (13.8 %) | 538 / 72 |
  | the same | 4x4 | 40 | 4 (10.0 %) | 257 / 51 |
  | the same, the sum without the stock terms (energy, defense, initiative) | 2x2 | 40 | 10 (25.0 %) | 162 / 45 |
  | the outcome alone, the score the round's own | 2x2 | 40 | 20 (50.0 %) | 29 / 24 |
  | the outcome alone | 4x4 | 40 | 16 (40.0 %, 28.5 to 51.5) | 58 / 25 |

  Against Greedy on the same 20 seeds, the outcome-alone `4x4` reading wins 34 of 40 (85.0 %) and the first
  valuation at `2x2` won 47 of 80 (58.8 %); the one-round lookahead-34 took 0.983 from Greedy on 30 unseen
  seeds (2026-10-06, above). Different seeds, the same direction.
- **Why the sum failed.** A sum of one-step scores over rounds a bot plays out cannot tell damage now from
  damage later, and prices every stock -- energy kept, defense held, initiative bought -- at every action it
  stays banked through, so the deep agent hoarded: Focus became the most cast spell in the catalogue. Dropping
  the stock terms from the sum moved it from one win in seven to one in four, the sign of a cause and not of a
  fix, so the sum went.
- **Why the outcome alone buys nothing either.** Four rollouts give an outcome in quarters, and the outcome is
  compared before the score, so one rollout that ends differently by its dice outranks any difference the
  round itself shows. Early in a match no rollout ends and the deep reading is the one-round reading at three
  times the clock, which is the 20 to 20 at `2x2`. Late in a match, when rollouts do end, the decision follows
  four dice rather than the round, and the reading is weaker: 16 to 24 at `4x4`, Focus up again. A purchase
  tolerates the same noise because the one-step pricing of a package is weaker still (ADR 0094); a combat
  move has a strong one-round reading to lose.
- **The clock.** One match against Greedy on seed 11, four cores: 9.3 s for 52 activations with the one-round
  reading (0.18 s each), 24.3 s for 46 with `4x4` (0.53 s each). A person waits half a second a move.
- **Where it lands.** The spec stays, opt-in, seated from a shell
  (`table --p2 lookahead:4x4:learning/weights/lookahead/lookahead-34.json`). Not in `learning/seatable.json`,
  which puts forward what measured as worth playing against, and not in the tuner's panel, which it would
  slow by the same factor for nothing measured.
- **What would be worth measuring next.** A deterministic continuation (plain rolls, one line, no dice), so
  that what is compared first is not a sample; or the outcome weighed into the score rather than ranked above
  it; or more rollouts, at the price of the clock. None is measured here.
