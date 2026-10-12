# 0111. At a table, ask both seats their speeds and intents at once

Date: 2026-10-12
Status: Proposed. Amends [0092](0092-a-table-waits-for-its-players-then-both-pick-at-once.md)

## Context

ADR 0092 asks both seats their package picks at the same time, and every other question in turn. It left
speeds and intents for later, if the table wanted them. The table now wants them.

ADR 0110 compares two combat declarations by how long a round takes two people
([free-activation-plan.md](../domain/free-activation-plan.md), §5). At the cardboard table both players choose
their speeds and declare their intents at the same time, face down. In the app, Player 2 waits for Player 1's
last intent. Measured that way, the hidden intent would lose a parallel phase it has at the table, and choosing
at the slot would be credited with the time saved.

## Decision

A table's driver asks both seats at once whenever both are asked the same face-down question: a package pick,
their speeds, or their intents. Simulations, evaluations and the benchmark keep asking speeds and intents seat by
seat. Bots answer at once, so for them asking at once saves nothing, while the order of their events and recorded
steps would stop being fixed by the seed.

**At a table:**

- Each seat answers all of its own on its own thread. The answers are then handed to the match together, the
  first seat's first, so a rebuilt table replays its commands in the order it recorded them.
- Each creature's intent is declared on the board its owner would have read after declaring the earlier ones. The
  driver builds that board from the owner's own answers, because an intent changes nothing else on its owner's
  board and nothing on the other's. `MatchDriverTests` holds the built board to the match's.
- Until the answers reach the match, the host shows each seat its own answered speeds and intents on its board,
  so a player still sees what they chose for the first creature while choosing for the second.
- An answer that cannot set a command off yet is not waited on for the trace, and is not checkpointed. That is a
  seat's speed or intent before its last one, or its last one while the other person is still being asked.
- After any answer the driver goes back to the top of its loop. So a question both seats are asked is never put
  to one seat alone, for instance the intents that follow one side's tie order.
- On a hotseat page, the seat holding the device stays on screen while it is asked. When only the other seat is
  asked, the page keeps the holder on screen for one more poll before putting up the pass screen: the moment
  between two of one player's questions looks the same as the moment they are done.

## Consequences

- Good: on two devices, speeds and intents take as long as the slower player, as at the table. The time readings
  of ADR 0110 compare the two rules on what a table plays.
- Good: simulations do not change at all, so the benchmark digest, the recorded datasets and their step order
  are untouched.
- Bad: a table hands the match its commands in another order than before. Seat 1's speeds now come before seat
  2's even when seat 1 resolved the last slot of the round before. A table recorded by an earlier host and still
  open does not match its checkpoint when a later host rebuilds it, so it is reported as not rebuilt (ADR 0091).
  Close the open tables before deploying this.
- Bad: two seats now decide concurrently through the speed and intent sub-phases as well as evolution.
  - The recorder and the journal already lock.
  - No agent keeps mutable state that both seats share. Practice gives both seats one Greedy fallback, which is
    safe because its scorer's caches are per thread.
  - A table with two people holds two blocked threads for most of a round, where ADR 0081 counted one.
- Bad: if one seat's agent fails, the failure surfaces only once the other seat has answered, as with a pick under
  ADR 0092.
- Bad: on a hotseat page, the pass screen comes up one poll (0.7 s) after a player's last answer.

## Alternatives considered

- **Hand each answer to the match as it comes.** No board would need building and nothing would need showing
  ahead of the match. But the two seats' commands would interleave in whatever order people answer, and a rebuilt
  table, whose journal answers both seats at once, would interleave them differently and fail its checkpoint.
- **Ask at once in every match.** Bots gain nothing from it, and a recorded dataset's step order would follow
  thread timing instead of the seed.
- **Ask at once only on two devices.** The host cannot tell a hotseat page from two devices, since both hold seat
  tokens. The one-poll hold costs less than asking the page to say which it is.
- **Correct the measurement instead.** Subtracting the serial wait from mode A's rounds would compare a corrected
  number with a measured one.

## Follow-up

- `MatchDriver.AsksSpeedsAndIntentsTogether`, set by `TableSession`; `MatchDriverTests`.
- `HumanSeat.Given` and `TableApi` (the board shown, and when a tap is settled and checkpointed).
- `table/seats.js` (`activeSeat`, `holdsOn`) and `table.js`.
- `RecordingAgent` and `DecisionJournal` comments; `table/README.md`, `AGENTS.md`; ADR 0092's status line; plan row
  5b.
