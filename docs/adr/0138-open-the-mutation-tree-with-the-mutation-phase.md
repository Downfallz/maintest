# 0138 — Open the mutation tree with the mutation phase

Date: 2026-11-06

Status: Accepted for the opt-in battle view.

## Context

[ADR 0128](0128-complete-the-game-loop-in-the-arena.md) made every planning question wait for the player to
open its controls, and opening them acknowledges the asking. #476 moved Evolution into a sheet beside the
battlefield, but kept that rule. In the mutation phase there is nothing else to do: the player had to press a
large "Mutation tree" button first. The host also asks a seat's picks one at a time, so the sheet closed
after every pick and had to be reopened for the next one, while the team still had picks left.

## Decision

In the battle view, an Evolution asking opens the mutation sheet by itself, once per asking. It waits until
the board is unobstructed: no other dialog open, no playback, no pending upkeep receipt or turn-order reveal,
and the first-use tip already closed. Opening it this way acknowledges the asking, as an explicit opening
does, because the controls are then on a visible page. A pick that leaves team picks keeps the sheet open,
disabled until the seat's next Evolution asking arrives. That asking is acknowledged in place and shows the
next creature that still has offers, with the picks remaining. A pass, the last pick, or a next view that asks
this seat nothing more closes it. A player who closes the sheet reopens it from the battlefield or the dock;
it does not open itself again for that asking. Speed, tie order and combat keep the explicit opening of
ADR 0128.

## Consequences

- Good: the mutation phase starts on the tree, and spending several picks is one continuous sheet.
- Good: the acknowledgement still means the controls were shown on a visible page.
- Bad: the time between the asking and its acknowledgement no longer includes the player noticing the phase
  on a visible page; recorded decision times start when the sheet opens.
- Neutral: confirmation is unchanged. Nothing is sent without Confirm or the confirmed team pass.

## Alternatives considered

- Keep the button and only keep the sheet open between picks: the first opening is still a step with no
  choice in it.
- Open every planning question by itself: tempos and ties are answered across the whole team or a group
  and have their own entry points; this decision is limited to the phase that asked for it.

## Follow-up

`table/battle-planning.js` keeps the controls open across a seat's picks; `battle/src/live.ts` opens the
sheet; `battle/README.md` and the battle UX acceptance record describe the phase.
