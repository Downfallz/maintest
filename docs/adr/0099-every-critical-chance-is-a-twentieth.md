# 0099. Every critical chance is a twentieth

Date: 2026-10-07
Status: Proposed

## Context

A player at the table rolls a die for a critical, and the die the catalogue can afford is a d20
([components.md §1.6](../tabletop/components.md#16-dice): it moves the fewest Spells, with the smallest worst
move and mean error, and it is the only grid `data/balance/knobs.json` declares, at a step of 0.05). The
engine does not care: it draws a continuous number and compares (`ResolutionRules`). The card does: a chance
that is not a whole number of twentieths prints a percentage and no threshold, and the rulebook had no
faithful way to roll it. Seven of the 44 Spells were off the grid, and none got there by a balance pass
choosing an odd number: 0.33, 0.667 and 0.717 are the legacy prototype's thirds, and a knob moves a value by
its step from wherever it sits, so every tuning pass carried the offset forward. A Creature's own chance is
zero since ADR 0042, which left the mechanism standing; the card prints the Spell's chance alone, so a
Creature with a chance of its own would roll against a number nobody printed.

## Decision

A Critical chance is a whole number of twentieths, 0, 0.05, ..., 1.00, everywhere it is authored: on a Spell,
and in the bounds a balance pass may move it between. A Creature definition's own chance is exactly zero.
The data builder refuses content that breaks either rule and names the nearest twentieth; `check-knobs`
refuses a `/criticalChance` band whose bounds or step are not multiples of 0.05. The seven Spells are snapped
to the nearest twentieth, a tie rounding up (`floor(x * 20 + 0.5) / 20`), by 0.02 at most, and
`lightning_bolt`'s band floor moves from 0.17 to 0.15. The `CriticalChance` type does not carry the rule:
the shared kernel holds no game rules (ADR 0007), and the engine rolls no die.

## Consequences

- Good: every card that rolls prints its threshold (`Crit 35% · d20 14+`), read off the content by the app's
  catalogue projection and by the print generator with no change to either, and the rulebook's "not yet
  built" clause goes. A knob that starts on the grid and moves in twentieths stays on it, so the offset
  cannot come back.
- Bad: a content change. Content `9419f935` becomes `b41ba55e`; on the 400 benchmark matches 96 outcomes
  move, 24 of them in the winner, Player 1 from 196 wins to 188 and the mean length from 11.44 to 11.34
  rounds (journal, 2026-10-07). Test content that gave its creature a chance of its own authors it on the
  card instead.
- Neutral: a Creature that wants a chance of its own is a new ADR, and it moves the threshold out of the
  shared catalogue projection into the per-seat payload ([playtest-app.md §1.3](../tabletop/playtest-app.md)).

## Alternatives considered

- A d6, d8, d10, d12 or d100: measured in components.md §1.6; each moves more Spells or leaves the declared
  knob grid, and the d10 cannot print 0.75.
- Snap once and enforce nothing: the next tuning pass starts from the grid, but nothing stops an author or a
  band off it, which is exactly how the thirds got in.
- Hold the rule in the `CriticalChance` type: the strongest place and the wrong one. A d20 is a rule of how
  this game is played, not of what a probability is (ADR 0007), and it would bind the engine to a die it does
  not roll.

## Follow-up

- `GameSchemaBuilder.CheckTheDie` in the data builder's path, and the twentieths check in `check-knobs`
  (`learning/src/downfall_learning/knobs.py`), each with its tests.
- The benchmark digest for `b41ba55e`, and the journal entry of 2026-10-07.
- The rulebook (§6.7, Part 9), the player aid, `translation.md` 1.9, `components.md` and
  [d20-criticals.md](../tabletop/d20-criticals.md), which priced the pass, say the rule is built and point here.
