# 0084. A creature waits only for a critical worth waiting for

Date: 2026-09-28

Status: Accepted

## Context

The heuristic agent chose Speed with one rule: Quick when a castable spell kills an enemy without a critical,
Standard otherwise. Its reasoning was that the scorer cannot see what acting first is worth, so Quick is
"strictly worse in everything it can measure". But a creature whose spells cannot crit gains nothing by
waiting: no creature has a base critical chance, and a third of the catalogue has none either (Basic Attack,
Heavy Strike, Throwing Star, Tranquilizer Dart, Guard...). The owner saw the bots go Standard for nothing
(2026-09-28). Every agent built on the heuristic -- the lookahead and the minimax included -- inherits its
Speed.

## Decision

**Standard only when the critical it keeps raises the best expected score** among the creature's castable
spells, read by the scorer under the agent's own weights; **Quick otherwise**, and always when a castable spell
kills an enemy without a critical. The rule still does not price acting first. It only asks whether waiting
buys anything the scorer can see, and goes first when it does not.

## Consequences

- Good: measured against the rule it replaces, on the benchmark seeds and on the hold-out confirmation seeds
  (400 matches each, mirrored), it wins with every weight set tried: greedy 58.3 % and 60.5 %, search-21
  56.2 % on hold-out, stun-first 98.9 % and search-23 99.8 % on hold-out. Weights that price a kill far above
  damage were kept in Standard for a critical they barely valued.
- Bad: every weights file was searched under the old rule, so each is now read under a Speed it was not fitted
  to. The benchmark digest changes (journal, 2026-09-28).
- Neutral: the policies under `models/` choose their own Speed and are untouched.

## Alternatives considered

- Standard only when the critical is worth more than a margin (1 or 3 points of score): worse than no margin
  with greedy weights (47.5 % and 3.0 % against the old rule), because a critical on Lightning Bolt, cast most,
  is worth a lot.
- Also Quick whenever the creature is under lethal threat, so it acts before it dies: no better or worse than
  the rule alone (47.7 % with greedy, 27.5 % with search-21, 50 % elsewhere). The threat reading assumes every
  living enemy hits this creature with its best spell, so it fires far too often and gives critical hits away.

## Follow-up

`HeuristicAgent.DecideSpeed`, `docs/learning/agents.md`, the benchmark digest and a journal entry. The next
weight search starts from this rule.
