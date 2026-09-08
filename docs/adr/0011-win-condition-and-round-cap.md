# 0011. Win condition: last team standing, with a round cap

Date: 2026-09-08
Status: Accepted

## Context

The prototypes ended a match when one team had no living creature and did nothing otherwise. Simulations
with defensive bots could loop forever. The design also keeps open other formats (objectives, score), but the
engine needs one rule now.

## Decision

A match ends at the end of a round when at least one team is defeated (all its creatures dead). If both are
defeated in the same round, the match is a draw. Independently, the rule set carries a maximum number of
rounds; when it is reached, the match ends and the winner is the team with the highest total remaining
health, a draw on equality. The outcome (winner, reason: `Elimination` or `RoundCap`) is part of the
`MatchEnded` event.

## Consequences

- Good: every match terminates; simulations report a reason; ties are defined.
- Bad: the round-cap tiebreak is arbitrary and may shape bot behaviour; it is a rule-set value, not a constant.
- Neutral: other win conditions get their own ADR and a rule-set switch when they are designed.

## Alternatives considered

- No cap: acceptable for humans, not for batch simulation.
- Sudden-death damage after the cap: more interesting, more rules; deferred.

## Follow-up

- `docs/domain/game-rules.md` records the rule; phase 7 implements it.
