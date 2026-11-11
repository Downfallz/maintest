# 0163. Read on what the host says changed, never on a clock

Date: 2026-11-11

Status: Accepted. Amends [0119](0119-a-seat-page-is-told-when-its-table-changed.md) (the fallback poll is gone) and
answers the page side of question 9 of [the stateless table inventory](../architecture/stateless-table-inventory.md)

## Context

ADR 0119 gave each seat a stream that says when its table changed, and kept the poll as a fallback: every 700 ms
while no stream is open, every five seconds while one is. Every other page still read on a clock: the lobby every
10 seconds, the bell every 20, the Friends panel and the ladder every 30, the players line of the classic table
every 30. A phone with the lobby open asked the host about fifteen times a minute while nothing happened, and every
one of those requests woke the API app from zero (ADR 0080, 0162).

A poll is also the only reason a missed notice is harmless. Removing it means a page must not miss one: a change
that happens between the page's first read and its stream opening, a stream a sleeping phone or a proxy dropped
without a word, a read that failed.

## Decision

Nothing a player sees reads on a clock. Every page reads on what the host says changed, and on the moments it may
have missed something:

- **The lobby's stream.** `GET /api/lobby/events` (`LobbyChanges`) streams `ready`, then `changed` with
  `lobby <n>` when the public lists moved and `account <n>` when the signed-in player's own lists moved, and a
  keep-alive every twenty seconds. Anybody may open it; only a signed-in player hears of their own lists.
  - The public lists move when a held table's *shape* changes: whether it is over, and per seat who holds it, who
    it is kept for, who declined it, whether it is open, whether its person has arrived and whether they are being
    asked something. A step of a match moves none of that, so a lobby in view reads when a game begins, ends,
    gains a player or waits on one, not at every slot. A table let go of moves it too.
  - An account's lists move with the shape of every table it has or had a part in, with each friendship change
    (asked, accepted, declined, withdrawn, removed: the silent ways out too) for both players, with each
    invitation notice, and when a result of theirs is kept.
  - One stream a page, however many parts of it listen (the lists, the bell, the Friends panel, the ladder), and
    none while the page is hidden: it is closed then and opened again when the page is back in view.
- **Every stream that opens is read on.** Its `ready` is a read, the first one too: what changed before the
  stream opened, or while it was down or closed, is read then.
- **A silent stream is a dropped one.** A stream that has said nothing for 45 seconds, past two of the host's
  keep-alives, is closed by the page and opened again.
- **A failed read is retried**, a second later, then two, up to thirty: a retry until it answers, not a clock.
- **Back in view, back online, restored from the back-forward cache**: the page reads at once and a stream waiting
  to open again opens now. A notice the service worker receives is posted to the pages, and the battle reads its
  seat on it, as the lobby already did: a second word beside the stream.
- **A finished match opens no stream again.** It changes no more, and a quiet connection kept open would keep the
  API awake for nothing.
- A seat's stream still bumps on every change of its table (ADR 0119), and the classic table reads its players
  line on those, three seconds after the first of a burst.

## Consequences

- Good: a page in view asks the host nothing while nothing happens. A lobby, a ladder or a finished game left
  open keeps no host awake, so the API app can scale to zero while somebody's phone has it open in the background.
- Good: an invitation, a friend's request, an opponent reaching the open seat and a game ending show at once in
  every lobby that shows them, instead of within ten to thirty seconds.
- Bad: a change the host never announces is never shown until the next one, a return to the page or a reload.
  Every lobby route that changes a table's seats now bumps the table, and the friends routes bump both accounts,
  but a new writer has to remember to do the same.
- Bad: one more connection per lobby page in view, held by the one API replica. While the API runs one replica the
  changes are counted in its memory; what another process writes reaches it through the table watch (ADR 0162), a
  second late, and a friendship or an invitation written by another process (the hourly job letting one go) is
  seen at the next return to the page.
- Neutral: the operator's admin panel and pilot page still read on a clock; they are tools, open on one screen.
- Neutral: the bots' queue is still read by its worker and measured by its scaler: that is the queue, not a page.

## Several replicas, next

With more than one API replica, a page's stream and the request that changes what it shows can land on two
replicas, and the counts kept in one replica's memory are not the other's. The next step, taken when a second
replica is needed, is a service that holds the connections, Azure Web PubSub: every process that writes (the API,
the worker, the hourly job) publishes "table X moved" or "account Y moved" to a group, and the page connects to
the service with a short-lived token the API issues for its seat's or account's groups. The contract stays this
one: the message says to read again, never what changed, so what a player may read is still decided by the API.
It replaces the table watch's once-a-second read of every held table, and lets the API scale to zero with pages
connected. It needs its own ADR: a WebSocket, which ADR 0119 set aside for a seat token in the address, here with
a short-lived service token instead, and a price to check.

## Alternatives considered

- Keep a slow fallback poll (a minute): it would still wake the API from zero for every page left open, which is
  the cost this decision is about, and the cases it covers are covered by `ready` and the silence watch.
- One stream per part of the page (the bell, the panel, the lists): three connections for one page, saying the
  same thing.
- Bump every lobby on every change of every table: a lobby in view would read at every step of every match.
- Close a seat's stream while its page is hidden, as the lobby's is: a phone whose screen went off keeps reading
  and stays out of the notices (ADR 0118), which is unchanged here.
