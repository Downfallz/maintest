# 0113. Break a tie between two purchases with the seat's dice

Date: 2026-10-25
Status: Accepted

## Context

The heuristic agent buys the package worth the most (ADR 0102, 0108), and when two are worth the same the first
option in the content's order wins. At round 1 Warped and Predator are worth exactly the same to the built-in
weights, on content `6198d349` as before it. Shock's 4 damage with a half chance to crit and Quill's 3 on two
enemies both come to 6 expected damage, 3 over Strike. Both packages overtake as much initiative (1.5), so both
read 6.15 beside Brute's 6.70. `occultist` sorts before `prowler`, so Greedy opened Brute + Warped in every
match and never bought a Predator package at any point: 6 Predator casts in 9,295 across the 200 benchmark
mirrors.

Forced on one side against that Greedy, on the same 200 seeds from both seats, Warped + Predator scored 0.585
(0.54 to 0.63): Greedy passed over an opening that beats its own on a coin it never tossed. And Greedy is the
tuner's mirror, the benchmark's player and the opponent of every opening reading, so a third of the catalogue
was invisible to all three.

## Decision

When two or more purchases are worth the same, to within 1e-9, the heuristic agent buys one of them at random
with the seat's dice: the agent dice a batch already hands each seat, seeded from the match's seed, so a match
still replays. Only purchases are touched. The agent factory gives the dice to `greedy`, to `heuristic:<file>`
and to the Greedy an exploring or opening agent wraps. An agent built without dice keeps the first option, as
before. That is the heuristic a lookahead plays its guesses and rollouts with, including the Greedy or
heuristic a searching spec names (`lookahead:greedy`), and the table's practice fallback. A heuristic with
dice holds state, so a lookahead built on one plays its rollouts one after another rather than side by side.
A purchase worth more than every other is bought without a roll.

## Consequences

- Good: Greedy opens Brute and one of Warped or Predator, half the matches each (60 mirrors: 60 Warped picks,
  60 Predator), and buys the whole Predator family later. Predator casts in the benchmark mirror go from 6 to
  1,205.
- Good: no tier-1 opening beats it any more. Forced against the new Greedy on the same seeds, the best opening
  is Brute + Predator at 0.516 (0.47 to 0.56), and Warped + Predator falls from 0.585 to 0.405. Its old opening,
  Brute + Warped, reads 0.469 (0.42 to 0.51) against it.
- Good: its mirror is shorter: 10.8 rounds against 11.4 on the benchmark seeds, none at the round cap against
  0.5 %.
- Bad: the benchmark digest of `6198d349` is rewritten, and every reading the tuner and the journal took with
  Greedy before this one moves. The opening readings of ADR 0109 now read every opening against a mix of two.
- Neutral: Greedy is still deterministic for a seed. The two seats roll their own dice, so a mirror can open
  differently on each side, and a mirrored pair is still one match played twice.

## Alternatives considered

- Break the tie by the package's raw initiative: Predator's +3 beats Warped's +2, so Greedy would open Brute +
  Predator every time and Warped would vanish from its openings instead. It moves the blind spot.
- Leave it, since the tuner's variety reading plays Predator through the exploring lookahead: the mirror, the
  benchmark and the opening readings would stay blind to a third of the content, and the opening readings would
  keep measuring against a Greedy that misses its best opening.
- A random opening, which ADR 0108 rejected: it plays worse on purpose. This draws only between purchases the
  agent prices the same, so it buys nothing it thinks is worse, and the measurements above say it plays better.

## Follow-up

- `benchmarks/6198d349….json` is rewritten with `benchmark --write`.
- `docs/learning/agents.md` says Greedy breaks a purchase tie with its dice; the journal records the readings.
