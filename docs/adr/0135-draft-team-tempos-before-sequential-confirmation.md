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

Between two of the seat's askings the host serves a snapshot that asks nothing: the question is dropped as
soon as the seat answers and the next one is set once the match runs again. While creatures of the round's
roster still have no receipt, the batch and the drafts wait through that gap; the round's tempo scope ends
when every creature of the roster has one, when the round or the seat changes, or when another kind of
question is asked.

## Consequences

- Good: one explicit player action answers several sequential host questions, and the draft is the whole
  team's at once, the way the classic table asks it.
- Bad: partial acceptance is visible and cannot be rolled back. A refusal or a lost response midway leaves
  the accepted tempos committed and the rest to confirm again.
- Neutral: reconnection never silently continues a previously confirmed batch. The domain, wire values,
  simultaneous reveal, checkpoint sequencing and benchmark are unchanged.

Tests cover one/two/three eligible creatures, frozen batches, late acknowledgements, refusals, uncertain
acceptance, external changes, the gap between askings, privacy boundaries and explicit continuation.
Real-host two-seat testing compares the opponent's entire board, options, feed and asking across partial
submission, reload and completion.

## Alternatives considered

- One wire command carrying every tempo: the host would acquire a second shape of the same question, and the
  classic table, the recordings and the replays would have to learn it. The protocol asks one creature at a
  time and stays that way.
- Calling the client-side batch atomic, with a rollback on a refusal: the host has already applied the
  accepted tempos and told nobody but this seat; nothing can take them back without a rule the game does
  not have. The batch is honest about being sequential instead.
- Keeping ADR 0128's one-creature confirmation and only speeding it up: each confirmation still interrupts
  the player's reading of the team, which is what #411 found unworkable on the classic table.
- Persisting the draft in browser storage across a reload: a draft is a private choice of one seat on one
  device; what the host holds is reconstructed from its receipts, and nothing else survives a reload.

## Follow-up

- `table/arena-tempo.js` and `table/arena-planning.js`, with `arena-tempo.test.js` as the record of the
  cases above; `arena/src/tempo.ts` for the panel.
- `docs/tabletop/arena-ux-parity-plan.md`, AU5.
