# 0106. A creature may declare a spell it cannot pay for yet

Date: 2026-10-10
Status: Accepted

## Context

An Intent was legal only if the creature could pay the spell's cost when it was declared. The cost is also
checked again when the creature's slot comes up, and a creature that cannot pay then fizzles (ADR 0083). So the
first check refused a plan the second check would judge fairly. The owner hit this in play: Overdrive gives
energy to allies, and a player who means to cast it first, then cast a costly spell with an ally later in the
same round, could not declare that second spell. The review of 2026-10-07 froze the core and asked for a named
problem before a rule change. This is one, named by the owner at the table.

## Decision

An Intent is legal for an own, living, unstunned creature that knows the spell, whatever its energy. The price
is checked only when the slot comes up. A creature that has the energy then casts. One that does not is
revealed with no targets and fizzles, as ADR 0083 already rules. The player options list the spells a creature
can pay for now apart from the ones it cannot, and the table page draws the second kind dimmed but selectable,
with the energy it is short of. The agents still choose among the spells they can pay for now.

## Consequences

- Good: a player can plan a round around energy given in it, which is what Overdrive is for.
- Good: no bot plays differently, so the benchmark digest and every recorded match are unchanged.
- Bad: a player can declare a spell that will certainly fizzle. The page says so on the card and on the Declare
  button, and the fizzle is the price of the mistake.
- Neutral: at a physical table the opponent can no longer rule out a card from the energy rail alone. The rail
  stays face up because the slot check reads it.

## Alternatives considered

- Keep the old rule: it refuses a legitimate plan, and the slot check already covers every bad outcome.
- Let the agents declare short spells too: no agent plans a round's energy, so they would only fizzle more.

## Follow-up

- `IntentRules`, `IntentOption.UnaffordableSpells`, `PlayerDecisionCheck`, the table page (`hand.js`, `table.js`).
- `docs/domain/game-rules.md`, the rulebook (5.6, 6.1), the player aid, the Automa and the component notes.
