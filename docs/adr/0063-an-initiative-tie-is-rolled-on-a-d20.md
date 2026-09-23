# 0063. Roll an initiative tie between the sides on a d20, and let each side order its own

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
the rule open: should a table break ties by seat, alternate them, or roll? The maintainer decided two things:
a tie between the sides is rolled on a d20, and a player orders their own creatures.

## Decision

A tie is two questions, and each gets its own answer.

- **Between the sides, a roll-off.** Creatures in the same band, Quick or Standard, with the same Current
  initiative, and not all of one side, each roll a d20. The highest takes the first place. Creatures of
  different sides that roll the same number roll again. The rolls decide which places each side holds in the
  tie. The engine rolls on the match's own `IRandomSource` when it builds the timeline (`TimelineBuilder`), so
  a seeded match replays its rolls exactly. Tied creatures roll in seat order, Player 1's first and then the
  lower Creature id, so the sequence of draws is fixed. That order decides nothing else. A tie held by one side
  alone rolls nothing.
- **Within a side, a decision.** A player who holds two places or more in one tie orders their own creatures
  among those places. It is a new sub-phase, `TieOrder`, between `TurnOrderResolution` and `IntentSelection`:
  both players order at the same time, having seen the rolls, and before either declares an Intent. The other
  side's places never move. A round where no side holds two places in a tie passes through it without asking
  anyone. The order is hidden from the other player until both are in, like a Speed choice, and the timeline
  it produces is public (`TiesOrdered`).

## Consequences

- Good: the seat decides nothing. On the benchmark seeds the greedy mirror goes from **400 Player 1 wins to
  190**. The objective, measured on content `4d7a841c` as it stood before ADR 0062, goes from **288.31 to
  53.18**: `mirror.player1WinShare` reads 0.475 instead of 1.000.
- Good: the order of a player's own creatures is theirs, which is what it is at a table. The dice settle what
  is between the two players; which of your own acts first is a decision like the Speed you gave it. A table
  already needs the die: `docs/tabletop/components.md` recommends two d20s for the critical roll.
- Bad: every agent and host answers a new decision (`IPlayerAgent.DecideTieOrder`). Random agents shuffle
  each tie; exploring agents shuffle it at their rate; greedy, lookahead, minimax and policy agents keep the
  order the rolls left, because nothing they score reads which of two of their own creatures acts first. A
  tie order is not recorded in a dataset, since no encoding of it exists, so no policy learns it yet. Making
  an agent choose well here is its own measured step.
- Bad: the rolls draw on the same random source as the critical rolls, so every match with a tie between the
  sides plays differently from here on. The benchmark digest is regenerated and readings from before this
  change are not comparable with readings after it.
- Bad: the greedy mirror is still one match played 400 times: all 400 entries end by Elimination in 6 rounds,
  38 health to 0, and the dice now decide what the seat decided. ADR 0062's move to the exploring run stands.
- Neutral: the roll itself is not recorded on the Activation slot. A table sees the places the rolls produced,
  not the numbers.

## Alternatives considered

- **Keep the seat**: simple and deterministic, and it decided every greedy mirror under packages.
- **Roll every tie, the owner's included**: the first version of this decision. It takes from a player an
  order a table would let them choose, and rolls dice for a question no opponent is party to.
- **Alternate by round**: fair over a match, but it needs a marker and a rule for who starts, and agents would
  learn to time packages around it.
- **Choose the own-side order with the Speed choices**, before the rolls: no new sub-phase, but the player
  would choose without knowing which places they hold, and the choice would hide in the order of other taps.

## Follow-up

- Domain: `TimelineBuilder`, `TieOrderRules`, `Round`, `Match.SubmitTieOrder`, `RoundSubPhase.TieOrder`, the
  `TieOrderSubmitted` and `TiesOrdered` events.
- Application: `TieOrderOptions`, `PlayerDecision.OrderTies`, `SubmitTieOrder`, the driver, every agent, the
  seat visibility table. Hosts: the console, the table page (`table/ties.js`), the viewer samples.
- `docs/domain/game-rules.md`, `docs/domain/glossary.md` (Roll-off, Tie order), the tabletop rulebook §3.2,
  §5.5, §6.6, the player aid, and `playtest-app.md`.
- `benchmarks/`: the digest for content `4d7a841c`, and the journal entry.
