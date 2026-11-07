# 0141 — Never hold the player's own cast; Skip all from two results

Date: 2026-11-06

Status: Accepted for the opt-in battle view. Amends [ADR 0139](0139-confirm-every-action-before-the-next-choice.md)
(Step no longer waits on every item) and renames its **Skip to present**.

## Context

ADR 0139 held every incoming action for the player's **OK**, and Step held every item, the player's own casts
included. Since [ADR 0140](0140-keep-game-settings-with-the-account.md) Step is the default, so every own cast
asked for a press too. In play, that press confirmed what the player had chosen a moment before. The player asked
for none after their own creature acts: what follows is the next question, the round turnover, their next
creature, or the opponent's action, which plays and then waits for its OK.

**Skip to present** read as jargon, and it was offered with a single result left, beside the OK that already
moves past it.

## Decision

- The player's own cast animates and moves on by itself, in Step as in Auto.
- An incoming action waits for its **OK** in both paces (unchanged).
- Step holds the turnover receipts (aftermath, upkeep, the result) for **Next**; Auto plays them by itself.
- **Skip to present** is renamed **Skip all**. It is offered only while two or more results remain, and never
  while an incoming action waits for its OK.

## Consequences

- Good: a round costs one press per opponent action, and none for the player's own.
- Good: Step and Auto now differ in one thing, the turnover, which is what the setting names.
- Bad: a player who wanted to linger on their own cast's result reads it in History or round review.
- Neutral: arrivals under a dialog queue behind a held item (an incoming action, or a receipt in Step) and
  settle otherwise, as before.

## Follow-up

`table/battle-playback.js` (`waits`), `battle/src/live.ts` (Skip all), `table/battle.html` and the menu's Game
settings text, `battle/README.md` (AU7).
