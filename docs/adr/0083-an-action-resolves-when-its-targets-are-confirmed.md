# 0083. An action resolves when its targets are confirmed

Date: 2026-09-28

Status: Accepted

Supersedes the two-step combat of [ADR 0010](0010-round-phases-and-sub-phases.md),
[0069](0069-reveal-all-spells-before-targeting.md) and [0070](0070-reveal-spells-with-confirmed-targets.md)
where they keep resolution for after every target is bound; [0011](0011-win-condition-and-round-cap.md)
where it checks an elimination only at the end of a round.

## Context

Combat walked the timeline twice. `RevealAndTarget` revealed each intent in turn and its owner bound targets;
only when every slot was bound did `ActionResolution` walk the timeline again and resolve them. A player
therefore chose targets on a board that had not happened yet: a later slot aimed at a creature an earlier one
would kill, and the action fizzled or lost that target (`AllTargetsInvalid`). The agents carried the cost too:
`Foresight` and the lookahead replayed the revealed actions to guess which targets would still be standing.

The owner wants the table's rhythm instead (2026-09-28): everyone still declares a spell at once and hidden,
and the spells are still revealed one by one in timeline order, but each creature chooses its targets when its
turn comes and **its action resolves as soon as they are confirmed**.

## Decision

**`RevealAndTarget` and `ActionResolution` become one sub-phase, `Activation`, walked once with one cursor.**
At each slot of the timeline the creature's spell is revealed, its owner chooses targets on the board as it
stands, and the action resolves on confirmation: energy, critical roll, effects, and the public frame of ADR
0075, exactly as resolution did before.

**A creature that cannot act when its slot comes up is revealed with no targets and fizzles**, at no cost,
without its owner being asked: dead, stunned, no longer able to pay or to cast its spell, or left with no legal
target. The reveal and the fizzle are the same events as any action (`ActionRevealed` with no target, then
`CombatActionResolved` with its fizzle reason), so every reader already knows them.

**The match ends the moment a team is wiped**, mid-round or at upkeep: no further slot activates and no
cleanup runs; the round ends there (`RoundEnded`) and the match with it. Both teams wiped by one action is a
draw, as before. The round cap is still read only at the end of a round.

## Consequences

- A target is always on the board as it is, so `AllTargetsInvalid` and dropped targets no longer happen in a
  match; they stay in `ResolutionRules` as the guard of a rule no command can reach.
- The Target decision, its preview (ADR 0079) and the agents read a board that already includes the actions
  before it: the preview is exact but for rolls and hidden choices, and `Foresight`'s replay of revealed
  actions goes away. Markers "targeted by" have nothing left to show.
- The observation of a Target decision changes meaning, so the feature schema moves to `features:v8` and
  policies fitted on v7 are refused, as for ADR 0056. The benchmark digest changes with the outcomes.
- A match can end on any slot; recaps, statistics and replays read a round that stops short.
- The table shows each action as it resolves rather than a replay at the end of the round; the round's replay
  stays available from its recap.

## Alternatives considered

- Keep the two passes and only reveal later: players still aim at a future board, which is what the owner
  wants gone.
- Resolve on confirmation but finish the round after a wipe: the owner chose an immediate end; a surviving
  side's remaining actions change nothing that matters once the other side is gone.

## Follow-up

Rules, glossary and tabletop documents; domain (`Round`, `Match`, `WinCondition`, `Advance`), application
(options, driver, agents, projections, learning schema v8), hosts, table page, viewer; benchmark digest and a
journal entry.
