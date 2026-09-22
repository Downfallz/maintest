# 0062. Read the seat advantage on the exploring run, not on the greedy mirror

Date: 2026-09-23

Status: Accepted

## Context

`mirror.player1WinShare` asks whether the content favours a seat when both sides play it equally well, and it
reads that on greedy against greedy. Before packages that reading had signal — 0.500 (ADR 0032), 0.510
(ADR 0037), 0.490 (ADR 0042). Since [ADR 0056](0056-a-pick-buys-a-package-every-other-round.md) it does not.
Two identical deterministic agents on a symmetric board buy the same package in the same round, so every
creature's initiative on one side equals its twin's on the other, and the timeline breaks every such tie by
Player slot (`docs/domain/game-rules.md`). Player 1 acts first everywhere and wins: 400 of 400 on the benchmark
seeds. Read on five catalogues measured this week, the mirror says **1.000 on four** and 0.735 on the one
(`tier:prowler` +5) whose change happens to break the symmetry. The term weighs 3 × (0.45 / 0.05)² = **243 of
the 285.77** points the objective scores the shipped content at, so it is not one reading among many — it is
the objective, and it answers the tie-break rule rather than the content. ADR 0061 found the consequence: one
package initiative move bought 202 of those points while every variety term got worse.

## Decision

`player1WinShare` is read on `variety` (`explore:0.2` against itself) instead of `mirror`, with the same band,
scale and weight. That run holds skill equal in distribution while the two sides still diverge, which is what
the question needs; it is the move [ADR 0029](0029-read-variety-on-an-exploring-run.md) made for seven targets
for the same reason, that a greedy argmax degenerates where a question needs play to vary. `mirror` keeps the
readings its determinism does not spoil: match length, draws, the round cap and fizzles. Its metrics are still
reported; only the target moves.

## Consequences

- Good: the objective stops scoring a rule of the engine as if it were the content. Re-scored from the metrics
  already recorded, no match replayed: the shipped content goes **285.77 to 42.77**, and the reading every
  catalogue is judged by is 42 points of content rather than 243 of tie-break.
- Good: it turns ADR 0061's trap around. Under the old objective `tier:prowler` +5 read 101.76 against 285.77,
  a win; under this one it reads **60.81 against 42.77**, a loss, which is what its variety terms said all along
  (`tierUsageShare` +13.31, `tierWinSpread` +3.41). A search given package initiative can no longer buy seat
  asymmetry and call it balance.
- Bad: every score before this is incomparable with every score after it, the third time the `score` note in
  `knobs.json` records it. The readings in earlier ADRs and journal entries keep their meaning for the runs
  they describe.
- Bad: the exploring run is noisier. On 400 matches one standard deviation of a share near one half is about
  0.025, half the band's half-width. It read 0.44 to 0.495 on the five catalogues — inside the band on four,
  0.01 below it on the fifth for a penalty of 0.12.
- Neutral, and open: the rule underneath is untouched. Under packages initiative moves in lumps and ties
  between two sides are common, and a tie always goes to Player 1. On divergent play the seat reads 0.44 to 0.50
  on `variety` and 0.49 to 0.50 on `skill`, so it does not decide matches the way it decides the mirror. Whether
  a table of two people should break ties by seat, alternate, or roll is a rule question for the game and the
  tabletop, and this ADR does not answer it.

## Alternatives considered

- **Change the tie-break** — alternate by round, or roll: the rule question above, and a real one, but a change
  to the game made to fix a measurement would be the measurement deciding the game. It needs its own case and
  its own playtests.
- **Drop the target**: the question is still worth asking, and `variety` answers it; dropping it would leave
  the objective blind to a content change that genuinely favoured a seat.
- **Keep it on the mirror with a lower weight**: a degenerate reading at a smaller weight is still a reading
  that a search can move without improving the game, just more slowly.

## Follow-up

- `data/balance/knobs.json`: the target's `on` and `why`, the mirror's `reads`, and the `score` note.
- `data/balance/README.md`: what `mirror` and `variety` read.
- `docs/learning/journal.md`: the re-scored table.
