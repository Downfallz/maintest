# Game rules

This document states what the engine does today and what is decided for next. Ideas belong under
"Open questions". Vocabulary is defined in [glossary.md](glossary.md).

## Implemented

Nothing yet. The domain contains only the building blocks.

## Decided (from the prototypes, to be re-implemented deliberately)

### Match lifecycle

- A Match waits for two Players, starts when both have joined, and ends when the win condition is met.
- Each Player controls one Team built from Creature definitions in the Game resources.

### Round structure

Each Round goes through these Phases in order; no Phase can be skipped or revisited:

1. Start of round: upkeep (Condition ticks, resource regeneration).
2. Planning: each Player submits Evolution choices, then Speed choices. The Combat timeline is built from
   the Speed choices.
3. Combat: for each Activation slot on the timeline, the acting Player submits an Intent; Intents are
   revealed and targeted; the action is resolved.
4. End of round: check the win condition, then start the next Round.

### Determinism

Given the same Rule set, Game resources, Player decisions, and random seed, a Match replays identically.

## Open questions

- Win condition: last Team standing, round limit with score, or objective-based.
- Board: slot-based teams only, or positions with range and adjacency.
- Simultaneous versus sequential Intents within a Round.
- Team size and whether Creatures can be swapped between Matches.
- How much of the ML/simulation work (`legacy/DownfallArena/DA.Game.Tests/ml.md`) shapes the event model.
