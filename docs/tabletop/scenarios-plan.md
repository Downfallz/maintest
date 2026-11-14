# Scenarios: a recorded match cut at the top of a round

Status: plan. Decisions taken with the owner on 2026-11-14 are marked **decided**; what is still to settle is in
"Open questions". Each stage ends green on the full gate (`/verify`, and the browser suite when `table/` or
`battle/` change).

## Goal

Three uses, one mechanism:

- **Bug reproduction.** A finished match becomes an exact, shareable starting point: the board as it stood at the
  top of round N, with the same random stream ahead of it.
- **Debugging and teaching.** Play a different choice from that point. The official result, its record, its
  ladder rating and its statistics stay as they are.
- **Tutorials and puzzles, later.** The same starting point with a title, a goal and the seat to play.

Beside it, an admin **diagnosis** of a table in progress that seems stuck.

## Why it is cheap

A table is already its journal (ADR 0159): seed, roster, rule set and content hash (ADR 0163), then
`decisions.jsonl`. `MatchReplay.RebuildAsync` rebuilds the match from those through the ordinary commands, with
no memento. A scenario is therefore **a source and a cut**: the entries before the first decision of round N,
replayed into a new match created under the same seed. The random stream is at the same position because
the same commands drew from it. A different choice afterwards draws differently, as it would have at the
table. Nothing in the domain changes.

## Decisions

- **decided**: only the operator creates a scenario, from any recorded session (admin panel, operator token).
  Players do not, for now.
- **decided**: the opponent replays its recorded answers while they still fit, then a bot takes over (see
  "The scripted opponent").
- **decided**: a scenario is shared two ways: a link in the app (`/battle?scenario=<id>`) and a self-contained
  `scenario.json`.
- **decided**: playthroughs are ephemeral, as practice is today (ADR 0079). Only the scenario is kept. Nothing
  reaches the ladder, the statistics, the finished games, the session list or a training export.
- **decided**: diagnosis shows the public state by default. The hidden state (answers held, intents submitted)
  takes an explicit "Reveal hidden state", written to the console with the operator's name.
- **decided**: diagnosis actions in this plan are **re-queue the bot's turn** and **void the match** with a reason.
  Both exist already (the Jobs tab's Requeue, `TurnsApi`; the operator's void, ADR 0176). Diagnosis opens them
  on the table at hand.
- **decided**: tutorials and puzzles get metadata only (title, goal, seat). Success conditions come later.
- **decided**: any signed-in player who has a scenario's link may open it as practice. Creating one stays the
  operator's.
- **decided**: the person plays one seat, chosen when the scenario is cut, against the scripted opponent. No
  hotseat.
- **decided**: the practice band offers **Restart the round** only. Another round is another scenario.
- **asked, conflicts with ADR 0176**: the owner chose that a voided match tell its players a fixed "Ended by the
  operator, without a result", the reason staying in the admin panel. ADR 0176, accepted and shipped, shows
  the operator's explanation to the players. See "Open questions".

## The scenario file

```json
{
  "format": "downfall.scenario.v1",
  "id": "sc_…",
  "title": "Bleed tick after a stun expires",
  "goal": "Optional. What the person is asked to try.",
  "seat": "player1",
  "source": { "session": "<run id>", "round": 7, "cutAt": 143 },
  "match": { "seed": 1234, "roster": ["creature:…", "…"], "rules": { "…RecordedRules…" }, "content": "<hash>" },
  "engine": "<commit the cut was verified on>",
  "entries": [ "…the journal lines before the cut, as decisions.jsonl writes them…" ],
  "recorded": [ "…the source's lines after the cut, the scripted opponent's reading…" ],
  "checkpoint": { "round": 7, "board": "…both seats' boards at the cut, to verify a replay…" }
}
```

It is self-contained except for the content, which is named by hash. The store keeps content once per hash
(ADR 0163). An exported file can also embed the content, so it plays on a machine that never saw it
(`--with-content`).

## Stages

### S0 — ADR (done)

[ADR 0178](../adr/0178-cut-a-recorded-match-at-the-top-of-a-round.md): a match scenario is a recorded match cut at
the top of a round, replayed into a match of its own, never snapshotted. Glossary: **Match scenario** (not a
Simulation scenario), **Scenario cut**, **Scenario checkpoint**, **Scripted opponent**, **Diagnosis**.

### S1 — Cut and rebuild (Application, Infrastructure) (done)

Under `Application/Matches/Scenarios`, each one class and one public method, all through `MatchReplay`:

- `ScenarioRounds.ReadAsync`: the rounds a recorded match can be cut at. It steps through the journal one
  entry at a time and keeps each round the board reaches with no answer held, with where it begins (`RoundCut`).
  A journal that stops rebuilding still offers the round it stops in, and says why (`ScenarioReading.StoppedBy`).
  That round is the one an engine change shows in, the one to reproduce.
- `ScenarioCutter.CutAsync`: the match cut at a round (`ScenarioCut`): the entries before and after the cut,
  and both seats' boards there (`ScenarioCheckpoint`). `Scenario.RoundUnavailable` for a round not offered.
- `ScenarioStarter.StartAsync`: a match of its own under a new id and run, rebuilt from the entries before the
  cut and held to the checkpoint. It is refused with `Scenario.Diverged` when it does not stand on those boards.
- Tests: Application tests over two seeds and both combat declarations (every round offered in order; every cut
  starts on its source's boards; a scenario played on with the source's later decisions ends as the source did;
  an unavailable round; drifted boards; a journal that stops rebuilding). One Infrastructure test on the
  repository's content: a greedy mirror under both declarations, cut at every round, each scenario played on
  to the source's very outcome.

### S2 — The scripted opponent

- `ScriptedAgent(recorded, fallback)`: asked a question, it answers with the next recorded entry of its seat
  when that entry answers the same question (same kind, same creature) and is among the legal options. At the
  first question it cannot answer that way, it hands over to the fallback for good (`greedy` by default, any
  seatable agent by choice).
- Under hidden intents, the opponent's answers of the round being replayed were given before seeing the
  person's. Replaying them is what the opponent would have done, which is the point of "play it differently".
- Tests: it follows a record until the person deviates and an answer stops fitting. The handover is said once
  (`ScriptedAgent.TookOver`), shown in the battle view as "The opponent is now a bot".

### S3 — Store and CLI

- `IScenarioStore`: `scenarios/<id>.json` beside the recordings, or a `scenarios` container. Same store
  selection as recordings (`--record`).
- `dotnet run --project src/DownfallArena.Cli -- scenario cut --session <id> --round N [--seat player1] [-o file]`
  writes a scenario. `--with-content` embeds the content.
- `scenario check <file>` rebuilds it and says whether it still holds on this engine, so a scenario can be a
  regression test.
- `table --scenario <file|id>`: a practice host started on it, unrecorded, as `--practice` is.
- An xUnit test reads `tests/…/Scenarios/*.json` and rebuilds each one. Committing a repro file is how a bug
  becomes a test.

### S4 — Hosted play (Cli host, battle view)

- `POST /api/scenarios {"session","round","seat","title?","goal?","opponent?"}` (operator) cuts and stores it, and
  returns `{ id, link }`. `GET /api/scenarios` lists them. `DELETE /api/scenarios/<id>` removes one.
- `POST /api/scenarios/<id>/play` starts an ephemeral practice table, the way `PracticeRun` does, and returns a
  seat key. Unrecorded, never indexed, never in the lobby or the ladder. It ends at the match's end, or after
  an idle hour.
- `/battle?scenario=<id>`: the battle view on that seat, with a band "Practice from <session>, round N. The
  official result is unchanged", **Restart the round** and **Leave**.
- Who opens the link: any signed-in account (`401` otherwise, which offers sign-in). Each opening is a table of
  its own, so two people on one link never share a board.

### S5 — Admin panel

The panel's shape, tabs and shared list parts are [admin-plan.md](admin-plan.md)'s. This stage adds to its tabs
and reuses its actions rather than building them twice.

**Sessions tab**, on a recorded session row:

- **Practice from a round…**: a round chooser listing only compatible rounds, the seat, the opponent's
  fallback, an optional title and goal. **Create** stores the scenario and offers **Open**, **Copy link** and
  **Download scenario.json**.

**Scenarios tab** (`/admin#scenarios`), a new tab: the scenarios, paged as every list there, each with its source
(a link to its session), round, seat, title, link, download and delete.

**Tables tab**, on a table under way, **Diagnose** opens a sheet:

- **Public**: status, both seats (person, bot, empty), who is asked and since when (`answerBy`), journal length
  and its last lines (kind, seat, time, no content), the table's version, the record's basis (engine, content,
  rules), and the owed bot turn with its poison or dead-letter entries for this table.
- **Check replay**: rebuilds from the record on the recorded content and rules, writes nothing, and says
  "replays" or the first line that diverges and why.
- **Reveal hidden state**, explicit and logged with the operator's name: the answers held and the intents
  submitted, per seat.
- **Requeue this table's turn**: the Jobs tab's Requeue (`TurnsApi`), scoped to this table's letters, or a fresh
  `OwedTurn` when the turn was never queued. Refused when no bot seat is asked.
- **Void…**: the void sheet of ADR 0176 (category, explanation, confirmation), opened on this table.

### S6 — Tutorials and puzzles (metadata)

- A scenario's title, goal and seat are shown as a short card before the first question, and from the band.
- The operator can mark a scenario **listed**. Listed scenarios appear under Practice for signed-in players,
  who open them as practice (they still create nothing).
- Later, not in this plan: success conditions, and moving the nine `--practice` recipes into scenario files.

## Docs to update along the way

`AGENTS.md` (the commands and routes), `docs/glossary` terms, `table/` README lines for the admin panel,
`docs/architecture/stateless-table-inventory.md` (a second kind of ephemeral table), the ADR index.

## Open questions

1. **What players read when the operator voids a match.** ADR 0176 shows them the operator's explanation; the
   owner leaned toward a fixed sentence here. Either keep ADR 0176 as it is, or supersede it with an ADR that
   keeps the explanation for the operator and shows the players a fixed sentence. Nothing in this plan depends
   on the answer.
