# 0066. A creature buys one package an opportunity

Date: 2026-09-23

Status: Accepted

## Context

[ADR 0056](0056-a-pick-buys-a-package-every-other-round.md) gives each player two picks at round 1 and every
second round after it, and resolves the two **in sequence**: the second is judged on the board the first
left, so one creature may buy a package and then the package above it. It chose that over simultaneous
resolution because it puts the top of a family at round 3 of a median six-round match. In practice the picks
stack: the greedy mirror put both picks of an opportunity on one creature in half of them (120 of 240 over
40 matches), two packages at once, and all 400 benchmark matches ended the same way in 6 rounds. The owner
does not want the picks to stack (2026-09-23): a creature that takes two packages at once, or climbs two
levels, is a spike, and the pick stops being a choice between creatures.

## Decision

**A creature buys at most one package an opportunity.** The two picks go to two different living creatures,
and a pick aimed at a creature that has already bought this round is refused with
`Planning.CreatureAlreadyEvolved`. A player's effective picks are the schedule's, capped by how many of their
living creatures have not bought yet and still have something to buy, so a player down to one creature has
one pick. The picks are still submitted one at a time, but they cannot depend on each other any more:
prerequisites are per creature, and each creature takes one of them. The schedule, the package and its
initiative bonus are unchanged.

## Consequences

- Good: an opportunity is a choice between creatures, and no creature spikes two levels in one round. The
  greedy mirror stops being one match: its 400 benchmark matches last 6 to 13 rounds, 7.8 on average, where
  every one lasted 6.
- Good: the rule is one check on the round's own history, and it is the same check in the Domain, the options
  a client is offered and the table.
- Good: the objective on the shipped content goes from 11.11 to 5.30 with the content unchanged, most of it
  `tierUsageShare` (7.77 to 1.89): a creature that takes one package a round spreads its casts wider.
- Bad: the top of a family now arrives at round 5 at the earliest. That is what ADR 0056 rejected simultaneous
  resolution for, and the owner accepts it. Random agents buy 3.55 level-3 packages a match where they bought
  4.99, and two more spells go uncast on the exploring run (4, from 2).
- Bad: every measurement taken before this is incomparable once more: the benchmark digest is regenerated, and
  the agent weights, the tuner's runs and the objective are retaken rather than adjusted.
- Neutral: ADR 0056 stands for everything else it decided: the package, the schedule, prerequisites as the
  only eligibility rule, and the initiative bonus.

## Alternatives considered

- **Keep the sequence and cap a creature at one level an opportunity.** Two packages of the same level on one
  creature would still be allowed. It keeps the spike in initiative and spells and only removes the climb.
- **Simultaneous submission, both picks hidden and revealed together.** That is a second hidden decision in
  the planning phase for no gain: with one package per creature, the two picks already cannot interact.

## Follow-up

- `EvolutionRules` (the refusal and the effective picks) and `PlayerOptionsProjection` (a creature that has
  bought is not offered), with tests.
- The benchmark digest, `docs/domain/game-rules.md`, the glossary, the tabletop rulebook and player aid, and
  the journal.
