# 0115. A player lobby keeps a seat until its player explicitly leaves

Date: 2026-10-30

Status: Accepted. Amends ADRs 0081, 0091 and 0092.

## Context

Players arrive at a bare table page and need an operator's invitation for every game. The browser keeps
only one active pair of seat tokens. The existing decision journal restores games after deployment, but
an automatic twenty-hour abandonment policy prevents returning after a longer pause.

The player wants a desktop and mobile entrance listing games to resume and places to join, with a way to
create a table. A player who explicitly leaves should make their team available to someone else, even
once the match has started. Closing the page must still allow reconnection.

## Decision

- `/` without a query and `/lobby` serve the player lobby; `/play` serves the game. Existing `/?player1=...`
  invitations and `/lobby?token=...` operator links continue to work.
- `/api/lobby` exposes summaries only: id, format, round, waiting/finished state, players and open-place
  count. No seat/pilot token, join code, trace, hidden decision or admin control is public.
- Players may create a table with another player, Greedy, Random, or a shared device. The server owns the
  offered formats. The first player receives only their seat; shared device receives both. Only the empty
  second place of an online table is initially open. Creation requires recording and the registry's usual
  capacity reservation; arbitrary agent specs and file paths remain an admin capability.
- `POST /api/lobby/resume` accepts saved seat tokens and returns only seats they prove. It does not mark
  anyone present or begin a game. Browser localStorage keeps a history of up to 128 capabilities plus the
  active table. A fresh invitation selects one verified table, avoiding accidental hotseat across games.
- `POST /api/lobby/tables/<id>/join` claims one explicitly offered place. Creation, Join, Leave and arrival
  with an invitation share a host gate. The claim is written to `table.json` before its token is returned.
  The record's `openSeats` defaults to empty so older, operator-created tables stay invitation-only.
- Closing a tab or losing a connection keeps the seat. `POST /api/lobby/leave`, authorized by its seat
  token, replaces that token and its invitation code, resets presence and records the place as open.
  Previous credentials stop controlling it. The same HumanSeat and pending decision stay in the engine;
  a replacement takes the same team, state and question. No game decision is fabricated and no bot is
  substituted. A match naturally waits when it needs that team's answer. Other people in a shared
  browser keep their own seats.
- Open tables do not expire because nobody polls them. They are restored from the existing seed and
  journal regardless of age, with current tokens, codes and open places. The operator can close them;
  finished tables retain their existing one-hour in-memory retention and stored session artifacts.

## Consequences

No account or new hosting service is required. Azure Blob Storage remains the durable store, and the
existing same-origin fence and operator boundary remain in place. Private browsing without storage can
play from a capability link, but cannot keep lobby shortcuts. A different browser needs the invitation
link or code. Explicit Leave invalidates both, including on the next host.

Open games consume the existing sixteen-table capacity until finished or closed. Public visitors can
create tables and take explicitly open places; operating sessions, traces and bots stays behind admin.

The existing replay compatibility limit remains: a deployment that changes engine rules or catalogue
so recorded moves no longer fit can refuse restoration. This change does not introduce snapshot or
versioned-engine migration; such failures are preserved and reported by the restorer, never guessed at.
