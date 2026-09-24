# 0075. Capture public state around each combat action

Date: 2026-09-24
Status: Accepted

## Context

The table's action review highlights recorded outcomes against current battlefield totals. Command-level
trace snapshots cannot recover every action boundary: resolving the last action also runs cleanup and
starts the next round, including upkeep. Reconstructing intermediate stats in JavaScript would duplicate
combat rules and misrepresent capped effects and conditions.

## Decision

`CombatActionResolved` carries an optional `CombatActionFrame`: creature snapshots immediately before and
after application, plus the public timeline and roll-offs. Match captures it before driving cleanup. The
frame contains only information already public to both seats, never intents or other private choices.
Applied outcomes remain the authority for damage, healing and conditions actually applied.

The table reads these frames with explicit Before / After steps. Previous reverses those steps and Skip
returns to the current server projection, including subsequent cleanup and upkeep. Historical display
never replaces the live projection or submits a decision. Old events without a frame retain the labelled
current-state recap. Existing artifact readers can ignore the additive field.

## Consequences

The battlefield, spellbooks and timeline can show exact historical state without new game rules or random
draws. Events and recorded traces become larger (two creature snapshots per action); round retention in
the table remains bounded. Domain boundary tests and renderer tests cover final-action upkeep separation,
reversal, failed casts and returning to the next decision.
