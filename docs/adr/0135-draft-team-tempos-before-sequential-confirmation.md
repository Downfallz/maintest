# 0135. Draft team tempos before sequential confirmation

Date: 2026-11-04
Status: Accepted for the opt-in arena
Supersedes: ADR 0128's one-creature tempo confirmation UI only

## Context

AU5 restores the classic table's team tempo draft (#411) inside the arena. The host still asks one creature
at a time and holds both seats' private choices until simultaneous reveal (ADR 0111). Requiring a separate
player confirmation for each creature adds friction; calling a client-side batch atomic would be incorrect.

## Decision

Keep the host protocol and rules. `arena-tempo.js` owns a seat/round-scoped in-memory draft; the planning
facade delegates Speed to it while mutation and tie controllers retain their existing asking-scoped behavior.
Each eligible creature has an editable tab. Selecting advances to the next incomplete tab; all tabs remain
reachable. Confirm is enabled only when every remaining eligible creature has a valid draft.

Opening visible planning acknowledges the first asking, so that creature's recorded timing includes the
team's draft time, matching #411. Selecting never submits. Confirm freezes the draft and sends only the
currently asked creature, after acknowledging that asking. A successful response must be followed by a
fresh authoritative question and matching private receipt before the next write. Duplicate confirmations
and polls cannot send an asking twice within one attempt.

Refusal, uncertain response, changed offers, external answers, covered controls, disconnect, hidden page,
seat or round changes stop the batch. After uncertainty, reread the selected seat before enabling explicit
continuation. Already accepted choices remain committed; remaining drafts may be edited. A hidden page,
seat change or reload discards local drafts and reconstructs only the host's own accepted choices. No draft,
tempo or capability is stored in browser storage, added to public history or read from another seat.

The tab shows recorded energy and known-spell references. Only the currently asked creature may use its
host guidance. Other tabs label catalogue-only critical chances; they never borrow another creature's
modifiers. Details remain expandable, and keyboard tabs plus a sticky confirmation footer work in short
portrait and landscape layouts. Help describes the team workflow and preserves drafts.

## Consequences

One explicit player action can answer several sequential host questions. Partial acceptance remains visible
and cannot be rolled back. Reconnection never silently continues a previously confirmed batch. The domain,
wire values, simultaneous reveal, checkpoint sequencing and benchmark remain unchanged.

Tests cover one/two/three eligible creatures, frozen batches, late acknowledgements, refusals, uncertain
acceptance, external changes, privacy boundaries and explicit continuation. Real-host two-seat testing
compares the opponent's entire board, options, feed and asking across partial submission, reload and completion.
