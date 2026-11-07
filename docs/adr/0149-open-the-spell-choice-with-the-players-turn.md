# 0149 — Open the spell choice with the player's turn, docked beside the field

Date: 2026-11-07

Status: Accepted for the opt-in battle view. Extends
[ADR 0138](0138-open-the-mutation-tree-with-the-mutation-phase.md) to combat. Amends ADR 0128's explicit opening
for combat questions.

## Context

On the player's turn, the dock showed a bar with the creature, the phase and a large **Choose spell** button.
Pressing it opened the spell choice as a full-screen modal. That covered the board the player was choosing for:
which enemies are hurt, who is stunned, who comes next.

The mutation choice already opens by itself (ADR 0138) and docks beside the field instead of covering it.

## Decision

- An Action, Intent or Target asking opens the combat choice by itself, once per asking:
  - its spells for Action and Intent;
  - its targets on the field for Target.
- It waits for the same unobstructed board as the mutation sheet: no other dialog, no playback, no pending upkeep
  receipt or turn-order reveal, and the first-use tip closed.
- Opening it this way acknowledges the asking, as an explicit opening does.
- The spell choice is docked, not modal: a sheet under the field on a phone, a panel at its side on a wider
  screen. The field gives up that room and stays visible. Escape closes the choice.
- A player who closes it reopens it from **Choose spell** in the dock. It does not open itself again for that
  asking.
- Nothing is sent without Confirm cast or Declare, as before.

## Consequences

- Good: the turn starts on the choice, and the board stays readable while choosing.
- Bad: as with ADR 0138, recorded decision times start when the choice opens, not when the player noticed the turn.
- Neutral: a spell's targets are still chosen on the field, with the target dock under it.
