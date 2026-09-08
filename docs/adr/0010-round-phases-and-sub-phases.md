# 0010. Rounds are driven by a phase and sub-phase state machine

Date: 2026-09-08
Status: Accepted

## Context

A round is a fixed sequence of steps, some automatic (energy gain, condition ticks, timeline building), some
waiting on players (evolution picks, speed choices, intents, targets). The legacy prototype modelled this as
two table-driven, forward-only state machines: `RoundPhase` (Start of round, Planning, Combat, End of round)
and `RoundSubPhase` inside each phase. It was the clearest idea in the codebase and the tests around it were
solid.

## Decision

We will keep the two-level machine. Phases and their sub-phases, in order:

1. Start of round: `EnergyGain`, `OngoingEffects`.
2. Planning: `Evolution`, `Speed`, `TurnOrderResolution`.
3. Combat: `IntentSelection`, `RevealAndTarget`, `ActionResolution`.
4. End of round: `Cleanup`, `Finalization`.

`Planning_EvolutionResolution` from the legacy enum is dropped: it was never entered. Transitions are
forward-only within a round; moving to the current phase is a no-op; any other transition is an invariant
violation and throws. `Round` owns the machine and the choices; it knows no rules. Progression gates
(pure domain services) decide when a sub-phase is complete, and the `Match` aggregate advances it.

## Consequences

- Good: every player action is validated against an explicit sub-phase; the UI and bots ask "what is expected
  now" and get one answer.
- Bad: adding a step means touching the enum, the flow table, a gate, and the driver. That is deliberate.
- Neutral: the sequence of automatic steps is recorded in `docs/domain/game-rules.md`.

## Alternatives considered

- A flat list of steps: loses the phase grouping that rules and UI rely on.
- Implicit state derived from what has been submitted: fragile and impossible to explain to a player.

## Follow-up

- Phase 4 implements the machine and the round; phase 7 the driver.
