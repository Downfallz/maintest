# 0086. A match lasts eight to fourteen rounds

Date: 2026-09-30

Status: Accepted. Amends [0068](0068-a-match-lasts-ten-to-fifteen-rounds.md)

## Context

[ADR 0068](0068-a-match-lasts-ten-to-fifteen-rounds.md) set the designed match length at ten to fifteen rounds and
the exploiter's floor at ten. Having played the table since, the owner wants matches a little shorter: eight to
fourteen rounds (2026-09-30). The exploring run reads 9.9 to 10.2 rounds on content `ab944cfb`, at the bottom edge
of the old band, where any content move that shortens a match by a tenth of a round is penalised however it plays.
The same day's content pass strengthens tier-3 spells, which shortens matches on purpose.

## Decision

We will hold the exploring run's `averageRounds` to **8 to 14 rounds** where it was held to 10 to 15, and lower
the exploiter's clock floor from 10 rounds to **8** to match, as ADR 0068 raised both together. Base health stays
at 30: this changes the target, not the lever, and the tier-3 spells are what is expected to move the length.

## Consequences

- Good: the objective says the length the owner wants, and today's 10 rounds sits inside the band with room on
  both sides instead of on its lower edge.
- Good: stronger tier-3 spells can shorten matches without the length term fighting them.
- Bad: every score before this is incomparable with every score after, because two bands move. The journal entry
  of the day scores both sides of the change so the step is readable.
- Bad: a floor of 8 lets an exploiter close out faster before the objective objects. That is the same trade ADR
  0068 made in the other direction.
- Neutral: the tabletop's Round cap of 20 and its Round track are unchanged; the rulebook's stated length moves.

## Alternatives considered

- **Lower base health** to shorten matches. It moves every spell's worth at once; the owner prefers to move the
  target and let tier 3 carry the change.
- **Keep the exploiter's floor at 10.** It would then sit inside the band rather than at its bottom, penalising a
  fair eight-round game won by a player who only wants to win.

## Follow-up

- `data/balance/knobs.json`: the `variety` and `exploit` `averageRounds` targets and their `why`.
- `docs/tabletop/rulebook.md`, `docs/tabletop/plan.md` and the other tabletop docs that state 10 to 15 rounds.
- The journal entry of 2026-09-30.
