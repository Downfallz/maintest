# 0092. A table waits for its players, then both pick their first package at once

Date: 2026-10-02

Status: Accepted. Amends [0054](0054-a-table-for-two-people-at-one-screen.md) and [0081](0081-a-host-of-tables-found-by-their-tokens.md)

## Context

A table's match starts the moment the table is composed (ADR 0054): the driver asks seat 1 its first package
pick, and a person's seat blocks the driver on that question until somebody answers it. Two people invited
to one table therefore arrive at a match already under way: the first to open their link is asked to pick
while the other has not even reached the table, and the second is asked only once the first has picked,
because the driver asks the seats in turn. There is no moment where two people sit down together, and a
table opened for an evening holds a thread from the moment it is opened.

The package picks are made face down and revealed together (ADR 0089). Asking them in turn was an artefact
of the driver's loop, not a rule.

## Decision

**A table with a person in it waits for every person to reach their seat before the first question. Then
both players are asked their first package pick at once.**

- A person reaches their seat when a request carrying its token arrives: the link, or the code typed at
  `/j/<code>`. Until every person has, the match exists and its boards read, but the driver has not started
  and no thread is held. A bot's seat needs nobody; a table of bots begins when it is composed.
- While it waits, a seat is shown a room rather than a board: who is still to come, with that seat's join code
  and link, so the player who is here can pass them on. The code is the seat's key, and showing it to the
  other player is what inviting them is. `GET /api/seat/<slot>` carries it as `waiting`, null once the match
  has begun; `/api/session` and the pilot's view say `begun`; the admin panel lists the table as waiting.
- The driver asks both seats their package pick at the same time whenever both are being asked for one, and
  submits the two answers in seat order, so the engine sees exactly the commands it saw before. Every other
  question is still asked in turn: speeds, intents and targets are asked of one seat while the other has
  nothing to decide, and a seat is asked several of them inside one sub-phase (ADR 0039).
- A concession made while the table waits begins it, so the driver reads the end and the session closes.
- A rebuilt table (ADR 0091) that had begun begins at once, since it has decisions to replay; one that was
  still waiting waits again. A journal line is matched to its seat rather than to its place in the file,
  because which of two people picking at once answered first is not a fact the record can promise to repeat.

## Consequences

- Good: two people sit down together, and the first thing either of them does is the thing they do at the
  same time. The second player no longer opens a match the first has already started shaping.
- Good: a table waiting for its players costs no thread. The sixteen-table ceiling (ADR 0081) counts the
  tables being played, not the tables opened for later.
- Good: the engine sees the same commands in the same order, so the benchmark digest and every recorded run
  are unchanged.
- Bad: a table nobody ever joins waits, readable, until the sweep lets it go after twenty idle hours. It is
  the same fate as before, without a blocked thread in the meantime.
- Bad: two seats now decide concurrently inside the recorder, which records both through one list, and inside
  the session's journal. Both take a lock for it. Anything else that wraps a seat has to tolerate being asked
  from two threads at once during the evolution sub-phase.
- Neutral: a person watching a bot play their seat until a handover (`--handover`) still has to reach their
  seat before the bots start. The table waits for its people, whoever plays first.

## Alternatives considered

- **Start the match, show a lobby in front of it.** The first player would still have been asked their pick
  while the second was absent, and a page that hides a question the host has asked is a page that lies about
  the host.
- **Ask every independent question concurrently** (speeds, then intents). Speeds are also chosen without
  seeing the other side's, but a seat is asked one speed per creature and the page shows them one at a time;
  the gain is small and the reasoning about hotseat, where one device holds both seats, is not. Left for a
  later record if the table wants it.
- **A table-wide code** that seats the first comer in seat 1 and the second in seat 2. Simpler to say out
  loud, but it changes what a code is (ADR 0081: a code is one seat of one table) and what a seat's record
  says about who was asked to sit there. The room shows the other seat's code instead, which is the same
  invitation with the seats kept apart.

## Follow-up

- `MatchDriver.EvolveTogetherAsync`; `RecordingAgent` locks its steps.
- `TableSession.Begin` / `HasBegun` / `waitToBegin`; `HumanSeat.Arrive`; `TableApi` arrives a seat on every
  request it carries the token of, and serves `waiting`; `TableComposer` decides whether a table waits.
- `table/waiting.js` and the room in `index.html`; the admin panel's and the pilot's waiting states.
- `docs/tabletop/playtest-app.md` §2 and the route table, `table/README.md`, `AGENTS.md`.
