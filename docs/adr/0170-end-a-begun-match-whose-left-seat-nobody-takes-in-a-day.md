# 0170. End a begun match whose left seat nobody takes in a day

Date: 2026-11-12

Status: Accepted. Builds on [0087](0087-a-player-may-concede.md), [0115](0115-a-player-lobby-keeps-seats-until-the-player-leaves.md)
and [0169](0169-end-a-match-whose-player-let-their-time-to-answer-run-out.md)

## Context

A player who leaves a begun game (ADR 0115) offers their team to another player, and the match waits. Nothing ends
it if nobody takes the seat. The hourly job closes only tables that never began. The time to answer (ADR 0169)
covers only games between two players opened after it, and a seat nobody holds is not a player who could answer.
Such a table stays open in the lobby for good, and holds one of its remaining player's places for games in progress.

## Decision

We will let the hourly job (`table-tend`) end a begun match that has a seat open and no move for 24 hours. The team
nobody holds concedes, as its player would have by conceding (ADR 0087): the other side wins, the result is kept
for the ladder and the finished games, and each seat's account is told. When both seats are open, the match ends
as a timeout does (ADR 0169): whoever it still asks loses, a draw when it asks both. The day counts from the
journal's last write, or from the beginning, so nothing new is stored. A seat taken again since the index was read
is read off the table the job picks up, and nothing ends.

## Consequences

- Good: a game somebody walked away from always ends, including games against a bot and games opened before
  ADR 0169.
- Good: nothing new to store: the record already lists the open seats, and the store already knows the last write.
- Bad: the day counts from the last move, not from the leaving, so a player who leaves long after their last move
  gives the seat less than a day.
- Neutral: the job runs hourly, so a game ends up to an hour after its day.

## Alternatives considered

- Close the table without a result, as a table that never began is closed: a player losing could leave instead of
  conceding.
- Keep, in the record, when the seat was left: an exact day, but a record write more, and older tables would not
  have it.

## Follow-up

- `AbandonedSeats` in the table host, `TableTend`.
- `AGENTS.md`, `docs/adr/README.md`.
