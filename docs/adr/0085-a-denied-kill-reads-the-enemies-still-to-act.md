# 0085. A denied kill reads the enemies still to act

Date: 2026-09-28

Status: Accepted

## Context

The scorer's defensive reading (ADR 0022) pays the kill price for a heal or a defense buff that takes a
creature from dying this round to surviving it. It measures "this round" as the best hit of every living,
unstunned enemy. Since ADR 0083 the timeline is known when a spell is declared and when its targets are
chosen, and an action resolves at its slot. An enemy that has already acted, or acts before the cast, cannot
be stopped by it this round, yet it still made the round look lethal. The owner asked for a "survival mode"
for a creature about to die (2026-09-28): the reading that should trigger it was the one counting enemies
that could no longer hit.

## Decision

**The denied kill reads the threat of the enemies whose slot comes after the cast's on the timeline**, when
the agent has a timeline -- at a declaration and at a target. Without one (the Speed choice, an evolution, a
rollout) every living, unstunned enemy counts, as before. The damage a defense buff prevents over its rounds
still reads every enemy: the rounds after this one are whole.

## Consequences

- Good: measured against the reading it replaces, same weights on both seats, 200 benchmark seeds and 400
  hold-out confirmation seeds mirrored: greedy 56.8 % and 59.6 %, search-23 52.2 % and 55.6 %, search-21
  51.2 % and 50.5 %, stun-first 50.0 % and 50.4 %. It never lost.
- Bad: the benchmark digest changes again (journal, 2026-09-28), and every weights file was searched under the
  old reading.
- Neutral: the candidate terms a dataset records read the same way (`CandidateTerms`), so a dataset recorded
  before this reads its denied kills differently from one recorded after; the feature schema does not change.

## Alternatives considered

- The prevented damage read from the enemies still to act too: mixed. It helped search-23 (56.8 %) and hurt
  search-21 (44.8 %) and greedy (49.0 %).
- Speed to Quick under lethal threat: measured with ADR 0084, it cost more than it saved.

## Follow-up

`ActionScorer` (the threat reading), `Foresight.StillToAct`, `HeuristicAgent`, `CandidateTerms`,
`docs/learning/agents.md`, the benchmark digest and a journal entry.
