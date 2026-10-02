# 0081. A host of tables, each found by its tokens, opened from a lobby behind the operator's door

Date: 2026-09-27

Status: Accepted

Amended 2026-10-02: the lobby is the **admin panel**, at `/admin` (`/lobby` redirects). The game is not open,
so the page is what its one operator does with the host rather than a room players wait in: it opens tables,
offers the bots `learning/seatable.json` puts forward (read by the host, not written into a page), lists every
recorded session the store holds, deletes sessions and exports them as zips of run directories. `/api/me`
tells a table page whether its browser is the operator's, which is the one thing a player's page may ask.
Tables still live in memory and are still let go of; persisting one is a later record.

## Context

The table is one match per process ([ADR 0054](0054-a-table-for-two-people-at-one-screen.md)): the command
line says who sits where, the console prints the join codes and the pilot token, and the host is stopped by
whoever started it. Hosting it ([ADR 0080](0080-host-the-table-on-azure-container-apps.md)) removes every one
of those: nobody is at a console, a container is started once and left running, and two tables on one
evening would need two containers. The open question in `docs/tabletop/playtest-app.md` said the choice was
two ports or a session id in every route.

Three things have to be decided together: how a request says which table it is for, who may open a table,
and what becomes of a table nobody is at.

## Decision

**A seat token names its table. Tables are opened from a lobby behind the operator's door, kept in memory,
and let go of when nobody is at them.**

- **The token routes.** A seat token and a pilot token are 128 random bits each, minted per table. The host
  keeps a registry from every token to its table, and a request is answered by the table its token belongs
  to, through that table's own API, unchanged. No route carries a session id: the page, the transport, the
  pilot page and the seat's link are exactly what they were. A join code is the host's rather than one
  table's, minted against every code in use and forgotten with its table, so `/j/<code>` still reaches one
  seat of one table.
- **The lobby opens tables.** `GET /api/tables` lists them (where each one is, who is in each seat, the codes,
  the pilot link, where the session is written) and `POST /api/tables` opens one from the same request the
  command line composes (`person` or an agent spec per seat, initials, a handover round, a seed);
  `DELETE /api/tables/<id>` closes one. `/lobby` is the page over it. What the console printed of a table,
  the lobby answers with, to the operator only.
- **The operator's door has two forms, and a host is started with one.** At a console the host prints an
  operator token and the lobby is opened with it, the way the pilot page is (`?token=`, then the
  `X-Seat-Token` header). Behind the platform (`--platform-auth`) the host trusts the principal the platform
  stamps a signed-in request with, `X-MS-CLIENT-PRINCIPAL-NAME`, and answers a request without one with `401`
  and where to sign in. The header is trusted only when the flag says a platform is there. The platform is
  configured to let anonymous requests through: the players never sign in, a seat is still its code.
- **`--lobby` starts with no table.** The command line without it composes the one table it describes, as it
  always did, and the lobby is there beside it. With it, the lobby is the only way a table opens, which is
  the container's shape.
- **Tables live in memory and are let go of.** A table is kept an hour after it is finished and written, and
  twenty hours if nobody asks it anything, then disposed: its match stopped, its tokens and codes forgotten,
  its recording kept as far as it got. At most sixteen tables are under way at once. Tokens are not
  persisted anywhere: a token outlives nothing, because the match it names is in memory (ADR 0080) and
  gone with the replica.

## Consequences

- Good: the page and the pilot do not change. A phone that joined by code before this decision joins the
  same way after it, on a host with one table or twenty.
- Good: one container serves an evening of tables, and the recordings land side by side under one store.
- Good: the operator's door is the platform's, with no credential in the repository or the image. On a
  laptop it is a printed token, as the pilot's already was.
- Bad: sixteen blocked threads is the ceiling this host takes, because a seat with a person in it blocks a
  thread while they think (ADR 0054). The bound is a constant, not a queue.
- Bad: the lobby trusts a header. It is the platform's contract that it strips a client-sent
  `X-MS-CLIENT-PRINCIPAL-*` before stamping its own, and the flag that turns the trust on is the host's
  statement that a platform is there. A host started with the flag and no platform in front of it lets
  anyone in who types the header.
- Bad: a table nobody finishes is forgotten after twenty hours, with its recording saying it was abandoned.
  That is the same fate a laptop's Ctrl+C gave it, on a timer.
- Neutral: `/api/session` and `/api/catalogue` are asked with a seat token, as they already were; there is no
  route a stranger can list tables on.

## Alternatives considered

- **A session id in every route** (`/t/<id>/api/seat/player1`). The page, the transport, the join code's
  redirect and the pilot would all have changed, to carry a second thing beside a token that already named
  the seat uniquely.
- **One container per table.** Two people would need somebody to start a container for them, which is the
  console this decision removes, and a replica per table costs a replica per table.
- **Require sign-in for everything.** The people who sit down at a table are not the people who have an
  account (ADR 0080). A seat is its code.
- **Persist the tokens** in Table Storage, so a seat survives a restart. A token whose match is gone reaches
  nothing; persisting the match is the aggregate's memento, left open by ADR 0080.

## Follow-up

- `TableRegistry`, `PlayedTable`, `TableComposer`, `TableRequest`, `LobbyApi`, `OperatorGate` in
  `src/DownfallArena.Cli/Table/`; `TableServer` routes by token; `JoinCodes` is host-wide.
- `table/lobby.html`, `lobby.js`, `lobby.css` and their tests; `TableFiles` serves them.
- `CliOptions`: `--lobby`, `--platform-auth`.
- The container of ADR 0080 runs `table --lobby --platform-auth --bind 0.0.0.0 --record <container url>`, and
  its built-in authentication is set to allow anonymous requests.
- `AGENTS.md`, `table/README.md`, `docs/tabletop/playtest-app.md` (the route table; open question 2 closes).
