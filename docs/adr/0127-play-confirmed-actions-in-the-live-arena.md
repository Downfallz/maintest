# 0127. Play confirmed actions in the live arena

Date: 2026-11-03
Status: Accepted for the opt-in arena

## Context

A5 answers combat questions but replaces the battlefield immediately with each current snapshot. Several
resolutions, or the last action followed by upkeep, can arrive together. The host already supplies public
before/after frames on CombatActionResolved; subtracting successive polls would conflate these actions.

## Decision

Keep a bounded, per-viewpoint queue of confirmed resolutions, ordered by sequence. The first snapshot is
always a baseline, including after a hidden page resumes. Advance the seen watermark on receipt, using
feedNext and event sequences; duplicates cannot restart effects, including after Skip. Preserve transport
fencing and the separation of latest legal questions from historical presentation (ADR 0126).

Use feed's resolution labels, replay's historical boards and combat-fx's boundary changes. Render the
before frame during windup and after at impact, with effects derived only from applied outcomes. Damage
uses a directional lunge and impacts on every affected figure; recovery, energy and conditions use pulses;
fizzles never hit a target. Death and all stats come from the frame. The last action plays before the
current snapshot, even when that snapshot already belongs to upkeep or match completion.

Skip, opening combat controls, resize, motion preference changes and connection interruptions immediately
settle to the current snapshot. No timer sends a command or acknowledges a question. Reduced motion retains
short readable outcome frames without displacement or particles. Hidden pages, seat changes and access
loss erase historical and current private content and invalidate callbacks. A legacy/missing frame or a
catch-up beyond twelve actions falls back to the latest board and text of the latest confirmed result;
never fabricate intermediate stats. New arrivals during an existing legal draft update the board without
starting historical playback under its controls.

## Consequences

No Domain, Application, wire protocol or game rules change. Playback is optional presentation and cannot
hold a decision hostage. The last result stays readable in an accessible, expandable result panel; short
labels and Skip fit the fixed shell. Full upkeep/aftermath choreography and finished-session replay remain
A7. Tests cover queue order, event deduplication, cancellation, legacy fallback, multi-target effects,
fizzles, lifecycle clearing, current-question control access, and the final cast crossing a round boundary.
