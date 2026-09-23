# 0063. Roll an initiative tie on a d20

Date: 2026-09-23

Status: Accepted

## Context

The Combat timeline orders Quick before Standard and initiative descending, and until now it broke every tie
by Player slot, then Creature id. Before packages a tie between the two sides was rare enough not to matter.
Since [ADR 0056](0056-a-pick-buys-a-package-every-other-round.md) initiative moves in lumps: a package pays
one bonus, and two sides that buy the same package in the same round tie on every creature. The greedy mirror
showed where that leads. Two identical agents tied everywhere and Player 1 won **400 of 400** benchmark
matches, all by Elimination in 6 rounds. That reading weighed 243 of the 285.77 points of the balance
objective. [ADR 0062](0062-read-the-seat-on-the-exploring-run.md) moved the measurement off the mirror and left
the rule open: should a table break ties by seat, alternate them, or roll? The maintainer decided to roll.

## Decision

An initiative tie is rolled off on a d20. Creatures in the same band, Quick or Standard, with the same Current
initiative each roll the die, and the highest roll acts first. Creatures that roll the same number roll again
among themselves until every one of them is separated. A creature that ties with nobody rolls nothing. The
engine rolls on the match's own `IRandomSource` when it builds the timeline (`TimelineBuilder`), so a seeded
match replays its rolls exactly. The tied creatures roll in seat order, Player 1's first and then the lower
Creature id, so the sequence of draws is fixed. That order decides nothing else. The rolls are made once, at
`TurnOrderResolution`, before any Intent is declared, and every Player sees the order they produced.

## Consequences

- Good: the seat decides nothing. On the benchmark seeds the greedy mirror goes from **400 Player 1 wins to
  188** (212 for Player 2). Scored on content `6df8dc30`, the objective goes from **285.77 to 58.63** under
  the objective as it stood before ADR 0062, with no content change: `mirror.player1WinShare` reads **0.470**
  instead of 1.000.
- Good: a table already needs the die. `docs/tabletop/components.md` recommends two d20s for the critical
  roll, so a roll-off adds no component. It also retires the "Player 1 is decided first" step of setup.
- Bad: the rolls draw on the same random source as the critical rolls, so every match that has a tie plays
  differently from here on. The benchmark digest is regenerated and readings from before this change are not
  comparable with readings after it. `variety.tierWinSpread` moves most, 0.348 to 0.609: a
  package's win rate now carries the dice as well as the package.
- Bad: the greedy mirror is still one match played 400 times. All 400 entries end by Elimination in 6 rounds,
  38 health to 0, and the winner is whoever the dice favour. The dice now decide what the seat decided before,
  which is why ADR 0062's move to the exploring run still stands.
- Neutral: the roll is not recorded on the Activation slot. A table sees the order the rolls produced, not the
  numbers themselves. Showing the numbers is a change to the table page and its projection.

## Alternatives considered

- **Keep the seat**: simple and deterministic, and it decided every greedy mirror under packages.
- **Alternate by round**: fair over a match, but it needs a marker and a rule for who starts. A tie in an odd
  round would still always go to one side, and agents would learn to time packages around it.
- **Shuffle the tied creatures uniformly without dice**: the same distribution as rerolling a d20, but not
  something a table can do with what is in the box, and the engine should play the game the table plays.

## Follow-up

- `TimelineBuilder` rolls off a tie; `Match` passes its random source. Domain tests script the rolls.
- `docs/domain/game-rules.md`, `docs/domain/glossary.md` (Combat timeline, Roll-off).
- `docs/tabletop/rulebook.md` §3.2, §5.5 and §6.6, and `docs/tabletop/player-aid.md`.
- `benchmarks/`: the digest for content `4d7a841c`, and the journal entry.
