# 0155. Let a game between two players be opened as unranked

Date: 2026-11-09
Status: Accepted. Amends [ADR 0124](0124-rank-players-with-glicko-2-over-ranked-results.md), which ranks every
finished game between two accounts.

## Context

ADR 0124 ranks a result when its two seats are held by two different accounts. Players asked for a friendly or
practice game against another person, at an open seat or for a friend, that the ladder does not read. The
ranked rule lives in `MatchResult.Ranked`, read off the kept results, so the choice has to travel from the
table to its result and survive a host restart like the rest of the table.

## Decision

- **The table carries it.** `POST /api/lobby/tables` takes `"ranked": false` for an open seat (`person`) or a
  friend. The table is then `Casual`, kept in its record (`TableRecord.Casual`) and restored on a rebuild. A bot
  or a shared device ignores the flag: those games are never ranked anyway.
- **The result carries it.** `MatchResult.Casual` is written with the result. `Ranked` is false for a casual
  result, so the ladder neither rates it nor counts it. Older results lack the field and read as not casual.
- **The lobby says it.**
  - The create dialog has a Ranked switch, on by default, shown only for another player or a friend.
  - A casual game's card, ongoing or finished, reads "Unranked" beside its title.
  - A rematch of a casual game is offered unranked again.

## Consequences

- Good: two people can play without staking their rating, and the ladder stays about games meant to count.
- Neutral: an unranked game still counts toward the five games against players a person may have in progress
  (ADR 0147), and it is kept and listed with the other finished games.
- Bad: a player who forgets the switch plays a ranked game. The default stays ranked, so a ladder game is the
  one a player gets without thinking.

## Alternatives considered

- **Only friends' games unranked:** an open seat is where a practice game with a stranger happens too.
- **Decide per finished game:** either player could then drop a loss from the ladder after the fact.

## Follow-up

- The leaderboard page names unranked games among those that do not count, and explains provisional ratings.
