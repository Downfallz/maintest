# 0117. At a table, ask each player every pick of an opportunity before either reaches the match

Date: 2026-10-31
Status: Accepted. Amends [0092](0092-a-table-waits-for-its-players-then-both-pick-at-once.md) and
[0111](0111-both-seats-are-asked-their-speeds-and-intents-at-once.md)

## Context

ADR 0092 asks both seats their package pick at once, but a pick is one package. With two picks an opportunity
(the default rules), the driver asked both players their first pick, waited for both, submitted them, and only
then asked both their second. A player who chose fast waited for the other between their own two picks, which
the owner reported from a game on two devices. ADR 0111 already removed that wait for speeds and intents: each
seat answers all of its own, and the answers reach the match together. Picks are face down until both players
are done (ADR 0089), so the same reasoning holds for them.

## Decision

**At a table, each seat makes every pick of the opportunity, up to its pass, on its own thread. The picks are
then submitted in seat order, all of seat 1's before seat 2's.**

- Between two picks the driver builds the next board and options itself: a pick adds itself to its owner's
  `EvolutionChoices`, and takes one pick and its creature out of the options (`EvolutionOptions.After`). It
  changes nothing else on either board. `MatchDriverTests` holds both to what the match serves.
- The host does the same for a person. `HumanSeat` keeps the picks answered in the round, as it keeps speeds
  and intents. The page is served the board with those picks on it and the options without them, and an answer
  is checked against those options. A seat whose picks are spent, or that passed, is asked nothing more until
  the other is done.
- Only a pick that ends a seat's opportunity can set a command off. That is its pass, or the pick that spends
  its last pick while the other seat is not being asked one. Only that pick waits for the trace and checkpoints.
- Simulations, evaluations and the benchmark still ask one pick of each seat at a time. Bots answer at once,
  and the order of their events and recorded steps stays fixed by the seed.

## Consequences

- Good: on two devices, an opportunity takes as long as the slower player's picks, as at the table.
- Good: the benchmark digest and every simulated dataset are unchanged.
- Bad: the order of the picks a table submits changes, from first picks then second picks to seat by seat. A
  table already past a two-pick opportunity does not match its checkpoint when a later host rebuilds it, so it
  is reported as not rebuilt (ADR 0091). Close the open tables before deploying this, as for ADR 0111.
- Neutral: the rule that a creature takes at most one package an opportunity now lives in two places: the domain
  (`EvolutionRules.Offers`) and `EvolutionOptions.After`. The test keeps them together.

## Alternatives considered

- **Submit each pick as it is made, from both seats.** No local options are needed. But the two seats' commands
  would interleave in whatever order people answer, and a rebuilt table could not replay them in that order
  (ADR 0111).
- **Ask every match this way.** Bots gain nothing from it, and every recorded dataset's step order would move.

## Follow-up

- `table/README.md` describes the picks asked at once.
