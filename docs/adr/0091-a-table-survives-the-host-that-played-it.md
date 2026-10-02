# 0091. A table survives the host that played it, rebuilt from its seed and its decisions

Date: 2026-10-02

Status: Accepted. Amends [0080](0080-host-the-table-on-azure-container-apps.md) and [0081](0081-a-host-of-tables-found-by-their-tokens.md)

## Context

A match lives in memory (ADR 0080): a `Match` is an aggregate with a private constructor and a random source
of its own, it has no memento, and the hosted table's one replica goes away a few minutes after the last
request and with every deploy. Every table being played goes with it: the people come back to a `403` on the
seat they were playing, and the operator to an empty admin panel. ADR 0080 left persisting a match open as
"a memento of the aggregate and of its random source", and ADR 0081 declined to persist the tokens because a
token whose match is gone reaches nothing.

What the recording already keeps: the trace, checkpointed after every decision, the notes as they happen,
the catalogue. What it does not: the decisions themselves in a form the engine can be fed, and the table
around the match -- who was asked to sit where, the seed, the tokens and codes the players hold.

## Decision

**A table is rebuilt from its seed and its decisions, not restored from a snapshot.** The engine draws every
roll from the seed (`CreateMatch`), so the same decisions in the same order rebuild the same board. The host
writes, as the match is played:

- `decisions.jsonl`: every decision whoever is seated makes -- a person or a bot, tie orders included, which
  the dataset leaves out (ADR 0063) -- and every swap the pilot asks for, one line each in the order the match
  took them, as the wire would have posted them (ids as the page carries them). It is written the moment the
  decision is handed to the engine.
- `table.json`: the request the table was composed from (agents, initials, handover, seed), the match's id,
  each seat's token and code, the pilot's token, and a status: `open`, `finished` when the run closes, `closed`
  when the operator closes the table or the host lets it go.

When a host starts over a store, it reads every run whose record says `open` and whose last decision is more
recent than the registry would have kept an idle table for (`TableRegistry.IdleFor`, twenty hours), and
rebuilds each: the same request and seed, a match created under the same id, the same tokens in the seats and
the same pilot token, and every recorded line replayed into the fresh match before anybody is asked anything.
A swap line is applied where it was asked, through the pilot's own seating. The journal goes around the
occupant and inside the seat, so the seat still says who is sitting and applies its handovers; while the
record lasts the journal answers for whoever is seated, and once it is spent it writes again. The join codes
are minted as they were, so what a player wrote down still works, and the links they hold reach the same seat.

A line that does not answer the question the rebuilt match asks -- the content or the engine changed under
the record -- is a divergence: the replay is refused, the table is let go of, and the run stays in the store
as it was, listed in the admin panel as not finished. A replay that does not finish in twenty seconds is
treated the same way. Nothing is guessed at.

`CreateMatch` takes an optional id for this, which is the one change outside the host.

## Consequences

- Good: a deploy, or a replica that scaled to zero, no longer ends every match being played. Two people who
  close their pages for an hour come back to their match.
- Good: the domain is untouched. No memento, no reconstruction path on `Match`, no public setter: the
  aggregate is rebuilt by the same commands that built it the first time, through the same gates.
- Good: the record is readable. `decisions.jsonl` is the match as a list of moves, which is also what a
  "replay" or an "undo by replay" (playtest-app.md §7) would read.
- Bad: a bot that played before the restart draws from a fresh random source afterwards: its recorded moves
  are replayed, not re-decided, so its source is not advanced through them. The match's own rolls are the
  seed's and identical; what the bot would have chosen next may differ from what it would have chosen had the
  host stayed up. The stamp and the trace are unaffected.
- Bad: the dataset files are started over on a rebuild and the manifest's `createdAt` is the rebuild's. The
  steps are re-recorded by the replay, so a finished session's dataset is whole; the record keeps the table's
  own `createdAt`.
- Bad: a decision handed to the engine a moment before the host died may not have reached the store, and the
  rebuilt table asks it again. One question, never a divergence: the line was never written.
- Neutral: the tokens are in the store in the clear. The store is reachable by the app's identity and nothing
  else (ADR 0080), and a token is what a seat's link carries anyway; hashed, a seat could not be reached after
  a restart, which is what this record is for.
- Neutral: a table told `--no-record` is not rebuilt. It never was anything but memory.

## Alternatives considered

- **A memento of the aggregate and its random source.** The domain would grow a snapshot of every entity and
  of a random source's state, and a reconstruction path beside `Create`, for one host's convenience. The
  decisions are a smaller record and are already what the game is.
- **Replay `steps.jsonl`.** It is buffered until the match ends, it encodes a decision as a candidate index
  against a feature schema rather than as the decision, and it leaves tie orders out (ADR 0063). It is the
  learning dataset, and bending it toward the host would weaken both.
- **Rebuild from the trace.** The trace carries boards, not decisions; a board is the result and cannot be
  fed back to the engine.
- **A file-backed `IMatchRepository`** (playtest-app.md §7). It would persist the aggregate's state, which is
  the memento by another name, with the random source still to be solved.

## Follow-up

- `DecisionJournal`, `JournalEntry`, `TableRecord`, `TableRestorer`, `TableComposer.ResumeAsync`,
  `PlaytestRun.Resume` in `src/DownfallArena.Cli/Table/`; `JoinCodes.Mint(seat, code)`,
  `TableRegistry.TryRestore`.
- `CreateMatch(RuleSet, Seed, Id?)` in Application.
- `docs/tabletop/playtest-app.md` §7 (the row on surviving a restart), `infra/README.md`, `table/README.md`.
