# 0194. Keep what each seat was last shown beside the table

Date: 2026-11-16
Status: Accepted
Rulebook: none

## Context

A game between two players is played over days (ADR 0169), and the recap a player gets when they come back
(docs/tabletop/async-recap-plan.md) marks what they have not seen. Today only the browser knows what a seat was
shown (`table/battle-seen.js`, in `localStorage`): the phone does not know what the laptop showed, and a cleared
browser knows nothing. The mark moves several times a minute while a player watches results play, so it must not
be written into the table's record (`table.json`), whose tag every decision is written over: a mark written there
would race a decision for it. The host also knows when the journal was last written (ADR 0169), and the seat's view
does not say it.

## Decision

We will keep, for each seat of a table, its **last visit**: the last feed sequence the seat was shown and when it was
shown it, in a small document of its own beside the table's record, `seen-player1.json` and `seen-player2.json`
(`TableVisits`, through the same artifact store as `reactions.json`, ADR 0173: a file on a developer's machine, a
blob on Azure, a dictionary for a table nobody records). The page sends it with
`PUT /api/seat/<seat>/seen {"sequence": N}` under the seat's token, on the moments it remembers it today, at most
once every five seconds and once more when it is hidden; it sends nothing for a replay. The host keeps the larger of
the two sequences, so a write smaller than the one kept is answered with the kept one and changes nothing; the write
is made only over the version read, and read again and tried again when another process wrote first. The seat's view
gains `seen: { sequence, at }` (none before the first) and `lastMoveAt`, when the journal was last written (none for
a table that keeps no journal). The view reads the record on every read rather than holding it, so a second API
replica never serves an older mark. The browser keeps its copy and reads the larger of the two. A seen write moves
nothing: it is not a change of the table, it wakes no page and sends no notice.

## Consequences

- Good: the recap follows the player from one device to another, and survives a cleared browser and a deploy.
- Good: a seen write never touches the table's record, so it cannot refuse or delay a decision.
- Good: the view says how long ago the last move was, which the lobby, the notice and the recap need (stages R2-R4).
- Bad: one more small read per seat view, and one write at most every five seconds a watching page. Two small blobs
  per table, deleted with its run.
- Bad: the sequence is the seat's own word. A page that claims to have seen more than it was served only hides
  results from its own recap; the host does not bound it by the feed, because a process can lag the one that served
  the page.
- Neutral: the mark is not part of the match: not in the feed, the trace, the journal, the replay or the training
  data.

## Alternatives considered

- In the table's record: one tag for decisions and marks, so a mark would make a decision retry, and the mark moves
  far more often than decisions do.
- A row in Table Storage beside ADR 0174's activity records: a second store a table's files do not include, for a
  record that belongs to one table and goes with it.
- One document for both seats: two seats watching at once would race each other for one tag for nothing.
- Kept in the browser only, as today: the recap would not follow a player to another device.

## Follow-up

- `table/README.md#routes`, docs/reference/commands.md (the route and the view's fields), docs/domain/glossary.md
  (*Last visit*), docs/tabletop/async-recap-plan.md (R1 built).
