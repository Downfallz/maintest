# 0143 — Read the round turnover on the field

Date: 2026-11-07

Status: Accepted for the opt-in battle view. Amends how AU7 shows the aftermath and the paced upkeep.

## Context

The aftermath (conditions ending, stun immunity) showed as a text receipt over the whole battlefield, with no
creatures under it: AU7 would not draw a board the turnover did not record. A round that ended one condition
took the full screen to say so. The paced upkeep had become a sheet below the field, and with the reading card
of [ADR 0141](0141-never-hold-the-player-s-own-cast.md) the screen again carried two bands for one fact.

## Decision

- An aftermath reads on its round's last recorded action frame: the creatures as that action left them, the
  round's own order on the strip, a plaque on each creature naming what ended. Only the creatures' names come
  from the latest board; nothing else later is borrowed. Without such a frame, the plaques stand by name alone.
- A paced upkeep reads the same way, on the board it arrived with while that board is still before combat
  (unchanged), with its plaques and no sheet.
- The reading card says the step and one line (`Aftermath · 2 conditions ended`, `Upkeep · +2 energy each`);
  tapping it opens the full receipt.
- The match result keeps its text receipt over the field.

## Consequences

- Good: the turnover takes the space it needs, the field stays in view, and each fact sits on its creature.
- Bad: the full receipt is one tap away instead of on screen.
- Neutral: History, round notes and the order of the turnover are unchanged; no later board is drawn.

## Follow-up

`table/battle-playback.js` (`pacedAftermath`, `aftermathView`, `paced`), `battle/src/live.ts`,
`battle/src/playback-receipt.ts`, `battle/README.md` (AU7).
