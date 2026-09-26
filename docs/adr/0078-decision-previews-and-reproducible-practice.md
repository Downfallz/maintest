# ADR 0078: Decision previews and reproducible practice

## Status

Accepted.

## Context

A card describes a spell, but does not explain what choosing it means on this board.
Testing targeting, multiclassing and control also requires too much setup by hand.

## Decision

Application projects decision guidance from `PlayerBoardState` alone. It validates
intents and calls the domain's pure `ResolutionRules` with independent forced rolls.
The projection never reads a match's hidden intents, advances the match, or consumes
its random stream. It shows separate plain and critical effects on the current board,
including caster effects once per cast. These are computed effects before execution
caps and condition stacking, not promised final health or a prediction of the round.
The table explicitly distinguishes these estimates from its recorded resolution replay.

`table --practice` hosts a separate, unrecorded practice table. A fixed catalogue of
recipes uses ordinary agent decisions to reach a named question in the real engine,
then hands player 1 to the person. The browser can select or restart a recipe. Each
start owns a fresh service provider, seeded random stream, match, cancellation source
and seat token. Restart waits for the old driver to stop before disposing its gate.
A separate practice capability authorizes resets; ordinary seat tokens cannot reset
a table. Practice is never a recorded human playtest or a training episode.

## Consequences

The UI formats engine output and contains no second combat calculator. Previews do
not guess future enemy decisions, preceding actions, or random rolls. Recipes depend
on the authored catalogue and have integration tests that assert the actual stopping
question and learned packages. A changed catalogue must update a recipe or fail clearly.
This does not add saved-match restoration or change the rules and balance.
