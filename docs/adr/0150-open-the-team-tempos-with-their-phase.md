# 0150 — Open the team's tempos with their phase, docked beside the field

Date: 2026-11-08

Status: Accepted for the opt-in battle view. Extends
[ADR 0138](0138-open-the-mutation-tree-with-the-mutation-phase.md) and
[ADR 0149](0149-open-the-spell-choice-with-the-players-turn.md) to the tempo phase. Amends ADR 0128's explicit
opening for Speed questions.

## Context

When the team's tempos were asked, the dock showed a bar with a **Team tempos** button. Pressing it opened a
full-screen modal over the board. The player chose Instinct or Precision for each creature without seeing the
field: its health, its energy, the opponent's team.

The mutation and spell choices already open by themselves and dock beside the field.

## Decision

- A Speed asking opens the team's tempos by itself, once per asking.
- It waits for the same unobstructed board as the mutation sheet: no other dialog, no playback, no pending upkeep
  receipt or turn-order reveal, and the first-use tip closed.
- Opening it this way acknowledges the asking, as an explicit opening does.
- The tempo sheet docks in the same place as the mutation sheet: a sheet under the field on a phone, a panel at
  its side on a wider screen. The field gives up that room and stays visible. Escape closes it and keeps the draft.
- A player who closes it reopens it from the dock's button. It does not open itself again for that asking.
- Tie order stays a modal.
- Nothing is sent without Confirm tempos, as before.

## Consequences

- Good: every command of a round (mutation, tempo, spell) now starts on its choice, beside the board it is for.
- Good: the sheet reuses the marks the other choices use. Instinct's bolt and Precision's star appear on the tabs
  and on the tempo cards. The energy chip is the spell choice's. Known spells are the spell choice's cards, with
  their crit chance at Precision.
- Bad: as with ADR 0138, recorded decision times start when the sheet opens.
