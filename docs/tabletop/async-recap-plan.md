# Plan: the recap when a player comes back to an asynchronous game

Status: accepted, 2026-11-16 (the owner settled the questions at the end). Nothing here is built yet; each stage
below is one pull request.

## What the owner asked for

A recap when a player comes back to an asynchronous game: after several hours away, they get the game's context
back at once.

## What exists today

A game between two players is played over days, one tap at a time (ADR 0169). This is what the table already does
for a player who comes back.

| Concern | What the code and the docs hold |
| --- | --- |
| Being told | "Your turn" on the phone (ADR 0118, `src/DownfallArena.Cli/Table/PushNotices.cs`): the title, then `Round 4: choose a spell.` Nothing about what happened. A warning when a quarter of the turn limit is left (`TurnTimeouts.cs`). |
| The lobby | "Your turn" cards, the most pressing first, with the time left to answer (`table/lobby.js`, `table/players.js#timeLeft`). The inbox lists `Your turn against <name>` (`table/inbox.js`). Nothing about what happened. |
| What was shown | `table/battle-seen.js`: the last feed sequence each seat was shown, in this browser's `localStorage`, twenty games kept. Another device, or a cleared browser, starts from the board as it is. |
| The catch-up | `table/battle-playback.js#receive`: what came after the seen sequence plays as it would have live, one action at a time, each waiting for its OK (ADR 0139, 0140, 0141). A catch-up of more than 12 actions, or across more than two rounds, settles on the latest board with no account of what it skipped. |
| The turn order | `table/battle-order.js` holds play until the round's order is read, from the same seen sequence (ADR 0144). |
| The journal | The history dialog (`battle/src/history.ts`, `table/battle-history.js`): a summary by round (mutations, casts, defeated creatures, result) and every event. On demand only, and it does not mark where the player left off. |
| The whole feed | The page keeps the last two rounds; `connection.history()` (`table/battle-live.js`) asks the host for the seat's feed from its start. The feed is seat-filtered: it holds nothing the seat may not see (docs/tabletop/playtest-app.md). |
| When | The host knows when the journal was last written (`TableComposer.LastMoveAsync`, ADR 0169) and when an account was last seen (ADR 0174). The seat's view carries `answerBy`. Feed entries carry no time. |
| The seat's read | `GET /api/seat/<seat>?since=&shown=` (`TableApi.SeatAsync`): `shown` tells the push notices the question was drawn (ADR 0118), in memory only. Nothing records what the seat was shown of the feed. |
| Reactions | The opponent's reactions (ADR 0173) notify nobody; the page marks them seen in `localStorage` (`downfall.arena.reactions-seen.v1`). |

So a player who comes back after a few hours either steps through every action one by one, or, after a long
absence, lands on a board with no account of how it got there. Nothing says how long ago the last move was, what
changed between the two teams, or what the player had chosen before they left.

## The recap

One sheet, **Since your last visit**, opened over the battle view when a seat comes back to a game that moved
without it. It fits one phone screen at 320 px and reads top to bottom:

1. **Where the game is.** Against whom, the round and its step (Upkeep, Command, Combat, Aftermath), the last move
   so many hours ago, and the time left to answer.
2. **The two teams now.** Each creature with its health and energy, the ones defeated while the player was away
   marked, and the difference since the last visit (`−12 HP`).
3. **What happened.** The rounds crossed, each in the history summary's words: the mutations revealed, each cast
   (who, which spell, critical or not, what it did), the defeated creatures. Most recent last, the older rounds
   folded.
4. **What you had chosen.** The seat's own choices still in force: the tempo of each creature this round, the spells
   it declared, the mutations it bought last.
5. **What you are asked now.** The question in the words of the push notice (`Choose a spell`), and the reactions
   the opponent sent.

Two buttons close it: **Go to my turn** settles the board on the present, and **Replay step by step** hands the
same span to the existing playback. The journal gains a marker, **You left here**, at the seen sequence.

The sheet reads only the seat's own feed and view: it shows what the seat could already read in the journal,
gathered. The page computes no rule. It formats public events, as `table/battle-history.js` does today.

### What was shown is kept at the host

The seen sequence moves from the browser to the host, one record per seat, so the recap follows the player from the
laptop to the phone. The page sends it when it has shown something new (`PUT /api/seat/<seat>/seen`), at most once
every few seconds, never on a clock: on the same moments `saveSeen` writes today. The seat's view returns
`seen: { sequence, at }` and `lastMoveAt`. The browser keeps its own copy as a fallback and reads the larger of the
two.

The record is small and sits beside the table, never in the table's record: a seen write must not race a decision
for the record's ETag. Its place in the store (a blob per seat, or a row in Table Storage) is the ADR of stage R1.

### When the sheet opens

It opens when the seat comes back after at least an hour (`seen.at`) and the feed holds at least one result the
seat has not seen. It never opens over a question the player is answering, in a tutorial, in a practice game, in a
replay, or for a device holding both seats (ADR 0120). Below the threshold, the catch-up plays as it does today. The
journal's **Since your last visit** button opens it at any time while unseen results remain.

### The routes

| Route | Who | What |
| --- | --- | --- |
| `PUT /api/seat/<seat>/seen` | seat | The last feed sequence this seat was shown; ignored when it is not larger than the one kept. |
| `GET /api/seat/<seat>` | seat | The seat's view gains `seen: { sequence, at }` and `lastMoveAt`. |

### Where the code goes

| Piece | Place |
| --- | --- |
| The seen record and its store | `src/DownfallArena.Cli/Table` (a port in Application if the store is shared with ADR 0174's activity records) |
| The recap model, pure | `table/battle-recap.js` and `table/battle-recap.test.js`, reusing `battle-history.js` and `feed.js` |
| The sheet | `battle/src/recap.ts`, its dialog in `table/battle.html`, its styles with the other dialogs |
| The words | `table/terms.js`, docs/domain/player-terminology.md, docs/domain/glossary.md (*Recap*, *Last visit*) |

## Stages

1. **R1, the seat's last visit at the host.** ADR: keep what each seat was last shown, beside the table. The record,
   its store (in memory, file and Azure), `PUT /api/seat/<seat>/seen`, `seen` and `lastMoveAt` in the seat's view.
   `battle-seen.js` sends it and reads the larger of the two. Tests: host tests for the route (a smaller sequence is
   ignored, another seat's key is refused, a replay writes nothing), the storage tests under Azurite, the Node tests
   of `battle-seen.js`. `table/README.md#routes` and docs/reference/commands.md.
2. **R2, the recap model.** `table/battle-recap.js`: from the seat's feed after the seen sequence, the board now, the
   seat's view and the time now, the five parts of the sheet as data. No page change yet. Tests: Node tests on
   recorded feeds: one cast, a round crossed, three rounds crossed, a creature defeated, a game that ended while the
   player was away, the seat's own choices, nothing unseen.
3. **R3, the sheet in the battle view.** The dialog, its two buttons, the threshold, the cases where it never opens,
   the journal's marker and button. Tests: Node tests of the threshold; a browser spec at 320 px and on desktop on
   the mock host (open, Go to my turn, Replay step by step, never over a question); the battle bundle rebuilt.
4. **R4, the recap reaches the lobby and the notice.** The lobby's "Your turn" card and the inbox say
   `3 casts since your last visit · last move 5 h ago`; the push notice adds the count to its body. The host counts
   the results after the seen sequence; it reads no board. Tests: host tests of the notice's body, Node tests of the
   card's line.

## Questions the owner settled (2026-11-16)

1. The sheet opens on its own after an hour away, with at least one result unseen. Below that, the catch-up plays
   as it does today; the journal's button opens the sheet at any time.
2. What a seat was shown is kept at the host, one record per seat, so the recap follows the player across devices
   (stage R1 and its ADR). The browser keeps its copy as a fallback.
3. **Go to my turn** follows the sheet by default; **Replay step by step** is offered beside it.
4. The lobby's "Your turn" card, the inbox and the push notice each carry a line of the recap (stage R4).
5. Every game a person sits at gets the recap, against a player or a bot.
