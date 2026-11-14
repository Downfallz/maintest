# 0176. Let the operator void a match with a reason, and withdraw a finished one's result

Date: 2026-11-13

Status: Accepted. Amends [0163](0163-keep-a-table-on-the-content-it-began-on-and-void-what-no-longer-replays.md) and [0168](0168-keep-match-results-in-azure-table-storage.md)

## Context

A match ends as a void match only on its own: when its journal no longer replays on the engine that picks it up
(ADR 0163). The operator has no way to end the same way a match that is stuck, broken by a bug or abused, and no
way to say why. A finished match is worse off: its result is kept for good (ADR 0168), so a game won through a bug
stays on the ladder and in both players' statistics.

## Decision

We will let the operator void a match, in progress or finished, from the admin panel
(`POST /api/tables/<id>/void`), and only with a reason:

- a **category**: the match is stuck, the engine misbehaved, a player abused the game, the operator made a
  mistake, or other;
- an **explanation**, 10 to 500 characters, in words a player reads.

The reason is kept as `MatchVoid` (category, explanation, who, when). A void by the operator is what ADR 0163's
void is, and a little more:

- the table's record is closed and marked void, and keeps the reason; the table leaves the index, and a table this
  host holds is let go of first;
- each seat's account is told on its devices, with the explanation, and a seat's page is answered `410` with it;
- **a kept result is not deleted but marked void with the same reason.** A void result counts nowhere: not on the
  ladder, not in a player's statistics, not among recent opponents or the operator's game counts. The finished
  games list still shows it, as voided, with the explanation, and its replay stays available.

A void is final, as in ADR 0163. Voiding a match already void is refused, unless a result for it still counts:
that one is withdrawn under the reason the match was voided for. The result is marked void before the record is, so a
store that fails in between leaves a record not yet void, which the operator voids again. It is looked for again
after the record is void, and a closer keeping a result reads the record after it writes: whichever of the two comes
second marks the result void. Every other process is told the table moved, and lets it go even when it is finished,
so its seats are answered with the explanation. ADR 0163's own void keeps its notice and carries no reason.

## Consequences

- Good: the operator can end a broken or abused match in words the players read, and take a result off the ladder.
- Good: nothing is lost: the result stays, marked, so the players still see the game and why it does not count.
- Bad: a kept result can now change after it is written. Every reader of results has to leave out a void one; the
  shared `MatchResult.Ranked` does, and the other readers do so by name.
- Bad: a match that finishes at the moment it is voided may keep its result unmarked: the void reads the results
  once. The operator sees it still counted and voids it again.
- Neutral: a void in progress ends the table the way closing it does; a bot turn still on the queue for it finds a
  closed table, owes nothing, and is done with.

## Alternatives considered

- Delete the result: simpler for the readers, but the players would lose the game from their history with no word
  of why.
- A void without a reason: quicker, but a player whose win disappears is owed an explanation, and the operator a
  record of why.
- An un-void: a void ends a table and tells its players; undoing that is a second message that confuses more than it
  repairs, and no case for it has come up.

## Follow-up

- `MatchVoid`, `MatchResult.Voided` and the readers (`Standings`, `PlayerStats`, `RecentOpponents`,
  `PlayerDirectory`, `FinishedGames`), the result documents.
- `TableRecord.VoidReason`, the void route, the notice and the `410`, the sessions list.
- The admin panel's Void sheet on tables and sessions, and the lobby's finished games.
- The operator log (admin plan, A6) will record each void.
