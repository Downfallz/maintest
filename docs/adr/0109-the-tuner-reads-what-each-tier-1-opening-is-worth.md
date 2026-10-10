# 0109. Let the tuner read what each tier-1 opening is worth

Date: 2026-10-10
Status: Proposed

## Context

Every creature opens on one of three level-1 packages (Brute, Warped, Predator), and a player buys two at round
1. The owner wants each opening to be a real choice, the doubles included, and finds the tier 1 good at the
table. The tuner had no reading of it: the mirror is Greedy against itself, which opens one way only, and the
exploring run opens at random one decision in five, which mixes every opening into one average.

Against the Greedy of ADR 0108, which opens Brute + Warped, the six openings forced on one side score, on content
`95a78999` and the 200 benchmark seeds played from both seats: Brute + Warped 0.500 (its own opening), Predator +
Predator 0.435, Warped + Predator 0.390, Brute + Brute 0.350, Warped + Warped 0.282, Brute + Predator 0.220.

## Decision

The engine seats an `opening` agent: `opening:<package>+<package>` is Greedy whose round-1 picks buy those
packages, one for one, and who plays the rest of the match as Greedy; a third segment names another inner agent,
as `explore:` does. The objective plays six evaluations, one per tier-1 opening, agent A that opening and agent B
Greedy, and holds each one's win rate between 0.40 and 0.60 (scale 0.05, weight 1).

## Consequences

- Good: what the owner asked of the tier 1 is a number the tuner moves content towards, and one it cannot trade
  away silently: a pass that makes one opening a trap scores worse for it.
- Good: the reading is Greedy's, so it is deterministic, cheap and comparable from pass to pass: about 45
  seconds an evaluation on a four-core machine, six of them, beside a candidate of 31 to 53 minutes today.
- Bad: every score before it is incomparable with every score after it. On `95a78999` the six add four readings
  below their band, the furthest Brute + Predator at 0.220.
- Bad: a forced opening is played by Greedy after round 1, so an opening Greedy plays badly reads worse than it is.
  The band is wide for that reason, and an opening below it is a question before it is a verdict.
- Neutral: Greedy's own opening scores 0.5 against itself by construction; it is read anyway, because content
  that moves Greedy's opening moves which of the six that is.

## Alternatives considered

- Seat a random opening in the mirror: the mirror would read matches decided at round 1 by a dice roll, and no
  reading would say which opening lost.
- Read only the openings of one package beside a free pick: it leaves the doubles out, which the owner wants
  playable too.

## Follow-up

- `docs/learning/agents.md` lists the agent; `data/balance/README.md` and `tune.yml` price the six evaluations.
