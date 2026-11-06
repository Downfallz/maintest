# 0139 — Confirm every incoming action before the next choice

Date: 2026-11-06

Status: Accepted for the opt-in battle view. Amends the AU7 extension of
[ADR 0127](0127-play-confirmed-actions-in-the-live-arena.md).

## Context

AU7 kept Auto as the default reading pace, with no per-cast confirmation. A player could also open the next
choice at any time, which skipped whatever was still being read. In play, that meant the next round's
mutation was offered in the dock before the opponent's closing attack had been seen. An opponent acting last
left four or five results to read while the next phase was already being asked.

## Decision

Every incoming action, one the other team's creature took, waits for the player's **OK** after its
animation, in Auto and in Step. Only after it does the queue move on: to the next action, to the round
turnover (aftermath, upkeep, the result), or back to the live board. The player's own casts, which they
chose and confirmed, move on by themselves in Auto, as the turnover receipts do; Step waits for **Next** on
every item. While results remain to be read, no command is offered: the
action, mutation and match-result entries are withdrawn, and nothing opens by itself, the mutation tree
included. **Skip to present** remains the deliberate way to the current board. The button reads **Next**
while more follows and **OK** on the last result, and takes the focus when it becomes usable and focus has
nowhere else to be.

## Consequences

- Good: the player always sees what led to the question before being asked it.
- Good: the player's own casts cost no press; only what they did not choose waits for them.
- Bad: a round costs one press per incoming action. Skip covers a player who wants the board at once.
- Neutral: the host, the events and the acknowledgement of a question are unchanged. A question is still
  acknowledged when its controls are shown, which now happens after the reading.

## Alternatives considered

- Keep Auto unconfirmed and only withdraw the commands during playback: the opponent's actions would still
  pass by without the player having read them.
- Remove Auto: the turnover receipts are reading time with nothing to decide, and Auto still serves them.
- Confirm the player's own casts too: they were chosen a moment before, and a press on each only slows the
  round without teaching anything.

## Follow-up

`table/battle-playback.js` holds actions; `battle/src/live.ts` withdraws the commands and labels the
button; `battle/README.md` (AU7) and the browser specs that relied on Auto moving past actions.
