# 0147 — List every finished game, and at most five of each kind in progress

Date: 2026-11-07

Status: Accepted. Amends the finished-table part of
[ADR 0115](0115-a-player-lobby-keeps-seats-until-the-player-leaves.md).

## Context

The lobby's Finished tab listed only the finished tables the host still held in memory. A host forgets a finished
table an hour after anyone last read it, and forgets all of them when it restarts. A player saw one finished game
while the leaderboard still counted seven. The leaderboard reads the kept results, which nothing removes.

The Ongoing tab mixed games against bots and against people. Nothing bounded how many a player could have open.

## Decision

- The Finished tab reads the kept results, not the tables in memory.
  - `GET /api/lobby/finished/<bots|players>/<page>` answers one page of six, newest first, for the signed-in account.
  - Each finished game says the format, who it was against, the result for this account, and the rounds played.
  - A table the host still holds opens with this account's seats there. Any other opens its recorded session.
- A result now names the table it was played at (`MatchResult.Table`). A result kept before that opens nothing.
- Games against bots are listed apart from games against players, in progress and finished alike. A game is
  against bots when a bot holds a seat. Every other game is against players, a shared device's included.
- An account has at most five games in progress against bots and five against players (`LobbyApi.OngoingPerKind`).
  - Opening, joining, claiming by code or accepting a sixth of a kind is refused with `Lobby.TooManyGames`.
  - The lobby shows each kind's count out of five, and says so before the player asks for a sixth.
  - A host without accounts sets no limit.
- Pages are read when the tab first needs them, when the player turns a page, and when one of their games finishes.
  The lobby's ten-second poll never reads them.

## Consequences

- Good: a finished game outlives its table, every restart and every device. The lobby and the leaderboard agree.
- Good: a player's lobby stays a list they can read.
- Neutral: each page reads every kept result to filter this account's. That is enough at today's scale. A per-account
  index would be the next step if results grow.
- Neutral: a player at five games of a kind finishes or concedes one before opening another.

## Follow-up

`FinishedGames`, `LobbyApi.FinishedAsync` and `TooManyOngoing`, `table/lobby.js` (`ongoingGames`, `localFinished`,
`finishedSummary`).
