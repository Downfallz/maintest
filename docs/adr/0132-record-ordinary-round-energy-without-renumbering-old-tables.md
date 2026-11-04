# 0132. Record ordinary round energy without renumbering old tables

Date: 2026-11-04
Status: Accepted

## Context

Upkeep grants ordinary energy before ongoing effects but records no receipt. A nominal allowance cannot
explain a passive bonus or distinguish an old recording from an actual zero gain. Adding an event changes
trace indices: open-table checkpoints compare each board at its sequence, and connected clients retain
those same indices. Silently reindexing a legacy trace would invalidate both contracts (ADR 0091).

## Decision

Raise `RoundEnergyGranted(MatchId, RoundId, Grants)` after the EnergyGain step and before ongoing effects.
Each `RoundEnergyGrant` records `Creature`, `BaseRequested`, `PassiveRequested`, `Gained`, `Before`, `After`.
Capture `Gained` from the existing single combined `GainEnergy` call; do not split the grant or change its
order. Record zero gains. Skip already-dead creatures; a creature killed by a later ongoing tick retains
its earlier receipt. Energy has no gameplay cap. Record values at the grant boundary, not from the trace's
post-command boards, which may already include ongoing effects or later phases.

The event is public to both seats. Round one is built on join, before human arrivals; the waiting-room
UI defers its presentation until arrivals clear, without changing the recorded boundary. Serialize through the existing kinded event writer. Journal, console,
viewer and recorded replay readers recognize it; the compact automatic receipt is AU3b. Readers of old
traces retain the facts present and never synthesize an exact ordinary gain. Ongoing-effect statistics
remain separate; the new record has no source spell and does not credit passive energy to a cast.

New tables and traces use `eventStreamVersion: 2`. A table record without that field is version 1.
A resumed version-1 table keeps version 1 for its entire remaining lifetime: its trace recorder omits only
`RoundEnergyGranted`, before assigning sequence numbers. It keeps existing events, boards, decisions,
random draws, checkpoint comparison and live feed cursors unchanged. Persist that version again on seat
changes and subsequent restarts. Legacy matches therefore have no ordinary-energy receipts, including
future rounds of that match; create a new match to get them. Do not invent a backfill or renumber an old
prefix. Unknown table versions are refused without replacing their recordings.

Configure the recorder before the first match event. New sessions, unrecorded seats and simulation traces
record receipts normally. Version 2 checkpoint comparison additionally compares the grant payload, not
only boards, so a mismatched receipt cannot be accepted because the final board happens to agree.

## Verification

Non-default allowance, passive/shared bonus, zero gain, dead creature and lethal ongoing damage;
public feed on both seats and arrival gating; plain-value JSON; unchanged benchmark outcomes and RNG;
current and legacy deterministic restore with stable event indices; version persistence on rerecording;
unknown-version refusal and changed-receipt rejection. Browser readers accept mixed old/new records
without reconstructing gains from rule defaults or projected board differences.
