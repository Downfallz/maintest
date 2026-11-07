# 0144 — Read the turn order on the field, and hold play until it is read

Date: 2026-11-07

Status: Accepted for the opt-in battle view. Amends AU11 and
[ADR 0139](0139-confirm-every-action-before-the-next-choice.md).

## Context

A round's revealed turn order opened a modal over the field. Anything that arrived under a dialog settled at
once, so it would not play unseen. An opponent who acted first therefore had their action resolve behind the
turn order, without the player seeing it or confirming it. That broke ADR 0139's rule that an opponent's action
always waits for an OK. The modal also listed the order as text, beside a field that already showed the
creatures it named.

## Decision

- A newly revealed order reads on the field: a badge on each creature with its place, gold for Instinct and
  blue for Precision. The dock's card says the round, each team's tempos, and whether a d20 broke a tie, with
  **Continue**. Tapping the card opens the full order: rules, initiatives and rolls.
- Until Continue, the field is held. A result that arrives meanwhile does not play.
- An opponent's action that arrives under any dialog, the turn order included, is queued unplayed instead of
  settled. It plays once the field is in view and then holds for its OK. The player's own actions under a
  dialog still settle, as before.
- The manual **Order** reference keeps its dialog.

## Consequences

- Good: an opponent's action is never passed over unseen. The order is read where the creatures stand.
- Bad: one press per round for the order, as the dialog's close already was.
- Neutral: the reveal's timing, its baseline on load, and History's record of the order are unchanged.

## Follow-up

`battle/src/order.ts` (`isReading`, `finish`), `battle/src/live.ts`, `table/battle-playback.js` (`resume`).
