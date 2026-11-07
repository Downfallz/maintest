# 0145 — End the match in one moment

Date: 2026-11-07

Status: Accepted for the opt-in battle view. Amends the match-end part of
[ADR 0143](0143-read-the-turnover-on-the-field.md).

## Context

A match that ended showed three things in turn:
- the match-end event as a step in the reading;
- a full-screen text receipt saying *Match complete*;
- the match result card, behind its own button.

Each repeated the one before it.

## Decision

- The match's end is not a step of the reading. The last action or turnover is read as usual.
- Once that last result is read, the match result opens by itself, once per seat: *Match complete* above
  Victory, Defeat or Draw, the reason and each side's survivors, with Replay and History.
- The full-screen text receipt is removed. The match result was the last thing it showed.

## Consequences

- Good: one moment says the match is over and how.
- Neutral: History keeps the match-end event. Closing the result leaves **Match result** in the dock to reopen it.

## Follow-up

`table/battle-playback.js` (`receipts`), `battle/src/match.ts` (`open`), `battle/src/live.ts`; the
`playback-receipt` overlay and its module are gone.
