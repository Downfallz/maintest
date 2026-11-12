# 0169. End a match whose player let their time to answer run out

Date: 2026-11-12

Status: Accepted. Builds on [0087](0087-a-player-may-concede.md), [0159](0159-advance-a-table-by-submitted-decisions-over-its-journal.md)
and [0162](0162-run-the-table-as-an-api-a-worker-and-an-hourly-job.md)

## Context

A game between two players is played over days, one tap at a time, and nothing ends a game a player stopped
answering. The other player waits for good, and the game holds one of the five places each account has for games
against players (ADR 0147). A table that never began is closed after twenty hours (ADR 0115). A begun one is closed
only when someone concedes.

## Decision

We will let whoever opens a table between two players choose how long a player has to answer: 24 hours, 3 days
(the default) or a week. The limit is kept in the table's record. Tables against a bot, and tables opened before
this ADR, have no limit.

A player who is asked and does not answer within the limit loses: the match ends with the reason **Timed out**,
and the other player wins. When both players are asked at once and both let the time pass, the match ends drawn,
with the same reason. Like a concession, it is a line of the journal (a timeout), so a match rebuilt from its
journal ends where it did. A timed-out game counts on the ladder as any finished game does.

The hourly job (`table-tend`) does it. For each begun table with a limit, the time since the journal was last
written is read from the store, without reading the journal. Only a table past its limit is picked up, to see who
is still asked. A player is told once, on their devices, when a quarter of their time is left.

## Consequences

- Good: a game between players always ends, and a player who walked away frees their opponent and their own place.
- Good: nothing new to store or run: the record carries the limit, the journal's own write time says when the last
  move was made, and the hourly job already picks tables up.
- Bad: the time counts from the journal's last line, so when two players are asked at once, the one who answers
  last gives the other more time. This is lenient, never harsh.
- Bad: the job runs hourly, so a game ends up to an hour after its limit.
- Neutral: a table's limit cannot be changed once it is open.

## Alternatives considered

- A seat that runs out is handed to a bot: the game goes on, but a player who left still "plays", and their
  result is a bot's.
- A timed-out game is void and counts nowhere: a player losing could walk away instead of conceding.
- Keep, in the index, when each table last asked someone: an index write on every decision, when the store already
  knows when the journal was last written.

## Follow-up

- `MatchEndReason.TimedOut`, `Match.TimeOut`, the `TimeOut` command, `TimeoutEntry` and its journal line.
- `TableRecord.TurnLimit`, the lobby's create dialog, `table-tend`.
- The warning a quarter before the limit, and the time left shown in the battle view and the lobby.
- `docs/domain/glossary.md` (Timed out, Turn limit), `docs/domain/game-rules.md`, `AGENTS.md`.
