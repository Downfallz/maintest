# 0163. Keep a table on the content it began on, and void a match its journal no longer replays

Date: 2026-11-11

Status: Accepted. Follows [0159](0159-advance-a-table-by-submitted-decisions-over-its-journal.md) (inventory
question 8)

## Context

A table is its journal (ADR 0159). Any process that picks it up rebuilds the match by replaying the journal on
its own engine, content and rule set. A rebuild is refused when a line no longer answers, or when the boards
differ from the trace checkpointed so far. So any change to a spell's numbers, a creature, the rules file or
the engine's resolution refuses every open match that has already met it. The deploy follows every merge to
main, and a match between friends can take days. A refused table is marked and left open for the operator.
Its players' links stop working, and nobody tells them why.

## Decision

We will keep each table on the content and the rule set it began on.
- The record names the content's hash and the rule set.
- The content itself is kept once per hash in the store, beside the recordings.
- Whatever process picks the table up replays it on those, whatever its own are now.

What still diverges is the engine changing under the journal. That ends the table as a **void match**:
- the record is closed and marked void, and the table leaves the index;
- no result is kept for the ladder;
- each seat's account is told the game was updated under the match;
- a seat's page is answered `410 Gone`, and says so.

One process tells, the one whose claim on closing the session stands (ADR 0162).

## Consequences

- Good: a content or rules change never ends a match that began before it, and a match the engine can no
  longer play ends in words a player reads, not in a dead link.
- Good: a content snapshot is about fifty kilobytes, written once per hash, so a dozen balance passes cost
  less than one recorded session.
- Bad: an engine change that alters how a recorded decision resolves still ends the matches that met it. Such
  changes are rarer than content changes, and tests catch them first.
- Bad: a table opened before its content was pinned carries no hash. It is replayed on the host's content, as
  before, and is voided if that no longer fits.
- Neutral: an open table the operator held after a refusal before this ADR is voided at its next pick-up.

## Alternatives considered

- Keep refusing and leave the table to the operator (today): the players learn nothing, and only a revert
  brings the match back.
- Hold the deployment while begun matches are open: one slow match between friends would block every
  release for days.
- One image per engine version, with each table routed to its own: it would cover the engine too, at the price
  of running and routing several revisions for a playtest's handful of tables.
- Snapshot the match state and stop replaying: ADR 0159 chose the journal over a memento, and a snapshot taken
  under one engine would be read by another all the same.

## Follow-up

- Two slices. **8a**: the void match, with the record's mark, the notices, `410` and the battle view's word.
  **8b**: the content and the rule set kept per table, and replayed on.
- `docs/domain/glossary.md` (Void match).
- `docs/architecture/stateless-table-inventory.md` (question 8).
- `AGENTS.md`.
