# Scenarios: a recorded match cut at the top of a round

Status: done, S0 to S6 (one open question remains). Decisions taken with the owner on 2026-11-14 are marked **decided**; what is still to settle is in
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

[ADR 0179](../adr/0179-cut-a-recorded-match-at-the-top-of-a-round.md): a match scenario is a recorded match cut at
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

### S2 — The scripted opponent (done)

- `ScriptedOpponent` (`Application/Matches/Scenarios`): asked a question, it answers with the next recorded answer
  of its seat when `PlayerDecisionCheck` says that answer answers what is asked (same kind, same creature) and is
  among the options offered. At the first question it cannot answer that way, or once the recorded answers run out,
  it hands over to its fallback for good (`TookOver`, and `HandedOver` said once). `ScriptedOpponent.Of(cut, seat,
  fallback)` takes the seat's answers after the cut.
- Under hidden intents, the opponent's answers of the round being replayed were given before seeing the person's.
  Replaying them is what the opponent would have done, which is the point of "play it differently".
- Tests: two scripted seats play a scenario to its source's very outcome without taking over, under both combat
  declarations; a seat whose answers run out hands over once and the match plays out; an answer not offered, or
  about another creature, hands over for good.
- Not done here: the battle view's "The opponent is now a bot" (S4). The local practice table says it on the console.

### S3 — The file and the CLI (done)

- `scenario.json`, version 1 (`Infrastructure/Matches/Scenarios/ScenarioFile`): the cut (`source`: session, run,
  match, round, `cutAt`), the source's seed, roster and rules, the content's hash, the engine it was verified on,
  the seat played, an optional title and goal, the journal entries before and after the cut in `decisions.jsonl`'s
  own dialect, the checkpoint, and with `--with-content` the content's schema itself.
- The checkpoint is compared by inclusion, not equality: every value recorded at the cut must be found unchanged
  on the rebuilt boards, and a field the boards gained since is not asked for. Without that, any field added to the
  board projection would refuse every scenario cut before it.
- `scenario-cut --session runs/playtest/<id>` lists the rounds a session can be cut at, and with `--round N` writes
  `scenario.json` (`--out`, `--seat player1|player2`, `--title`, `--goal`, `--with-content`). It replays on the
  content the session was played on: this host's own, or the one kept beside the runs under its hash (ADR 0163).
- `scenario-check --scenario <file>` rebuilds the scenario on this engine and says whether it holds, exit code 0
  or 1. It plays on the content the file carries, or on this host's when that is the content the file names.
- `table --scenario <file>`: the practice page on that one scenario, opened at once in the seat it names, the
  scripted opponent in the other seat over greedy, nothing recorded; the page's Restart starts it again from its cut.
- A scenario file kept under `tests/DownfallArena.Infrastructure.Tests/Scenarios/` is a regression test: it must
  carry its content, and it must still rebuild, and its source's answers after the cut still apply, on every engine
  change. One is kept: a greedy 1v1 mirror cut at round 3.
- Not done here: `IScenarioStore` (scenarios kept in the store, beside the recordings). It comes with S4, which is
  the first stage that keeps them.

### S4 — Hosted play (done)

- **Where a practice lives (decided with the owner).** In this process's memory, over the services the host already
  built for the scenario's content: no host is built for a practice, so an onboarding where twenty players open a
  tutorial at once is twenty small matches. One practice per signed-in account (opening one, a tutorial included,
  replaces the last), 200 at most per process, one nobody reads for an hour let go of. A restart or a deploy ends
  them; the page then offers the round again. The API runs one replica today; a host on several would need the
  practice journaled in the store, which this does not do.
- **The operator** (`ScenarioAdminApi`, behind the admin panel's gate): `GET /api/admin/scenarios` lists them;
  `POST` with `{"session","round","seat?","title?","goal?"}` cuts a recorded session on the content it was played
  on and keeps it, answering `{ id, link }`, or 409 with the rounds the session does offer; `DELETE
  /api/admin/scenarios/<id>` removes one. Kept beside the recordings (`scenarios/<id>/scenario.json`, or the
  `scenarios` container); the content is not carried, since the store keeps it under its hash (ADR 0163).
- **A signed-in player** (`ScenariosApi`, 401 signed out): `GET /api/scenarios/<id>` says what it is, `GET
  .../practice` resumes the account's practice of it, `POST .../practice` starts one from the cut in place of
  whatever practice the account had. A practice's seat token reaches its table only for the account that opened it.
- **`/battle?scenario=<id>`**: the battle view resumes the account's practice or starts one, and a band in the
  field's top right corner says "Practice", its title, "From the top of round N · the recorded match is unchanged",
  with **Restart the round** and **Leave**. The seat's view carries `practice`. The opponent's seat reads as a bot.
- Tests: the operator's cut, its refusals and its list; a signed-out player refused; a practice opened from the cut
  in the scenario's seat with nothing recorded; resumed, replaced, let go of after an hour, refused past the bound,
  refused once the scenario is removed (`ScenariosApiTests`); the page's practice module (`battle-practice.test.js`)
  and the band in the browser (`battle-services.spec.js`).
- Not done here: the scripted opponent's handover said in the battle view (the host's console says it).

### S5 — Admin panel

Two pull requests: S5a, the scenarios in the panel; S5b, the diagnosis of a table in progress, which does not
depend on the scenarios.

The panel's shape, tabs and shared list parts are [admin-plan.md](admin-plan.md)'s. This stage adds to its tabs
and reuses its actions rather than building them twice.

**S5a (done).** On a recorded session's row not being played, **practice from a round…** opens a sheet: it reads
the rounds the session can be cut at (`GET /api/admin/scenarios/rounds?session=<id>`, and why the journal stopped
when it did), then the round, the seat, a title and a goal. **Cut the scenario** answers with the link to open or
copy. The **Scenarios** tab (`/admin#scenarios`) lists them: title, session, round and seat, goal, and the link to
open or copy, the session's page, and **remove…**. A host that keeps no scenarios shows no tab. The page is
`table/admin-scenarios.js` (pure parts tested by `admin-scenarios.test.js`); `admin.ux.spec.js` drives the sheet and
the tab.

**S5b (done).** On a table under way, the Tables tab's **Diagnose…** opens a sheet (`TableDiagnosisApi`, behind the
admin gate; the page is `table/admin-diagnosis.js`, its pure parts tested by `admin-diagnosis.test.js`):

- **What anyone may see** (`GET /api/tables/<id>/diagnosis`): its state (open, closed or void; held by this process
  or not; begun, over, round), each seat (who, person or bot, played by a bot now, and what it is asked: a person's
  question, or a bot's options), when the asked player must answer by, the journal's length, when it was last
  written and its last twelve lines by kind and seat (never what was chosen), the content, rules and engine it is
  played on (and the basis it was refused under), and its bot turns' troubles (this process's last failure, this
  table's dead letters and the last one's error). An open table is picked up as any request naming it would be; a
  closed one is read from its record alone.
- **Check replay** (`POST .../diagnosis/replay`): the match rebuilt from the record and journal on the content and
  rules it began on, under a match id of its own and forgotten once read, so the table being played is untouched;
  it says "replays" or why not.
- **Reveal hidden state…** (`POST .../diagnosis/hidden`), after a confirmation: each seat's speeds, intents and
  picks face down, and the answers held for it, read on the same match rebuilt aside, and noted in the Activity tab
  with the operator's name (`table.reveal`).
- **Requeue the bot's turn** (`POST .../diagnosis/turn`): this table's dead letters back on the queue, or else the
  bots' turn owed again, which a bot that is not asked anything takes nothing of; refused only for a table of two
  people. Noted in the Activity tab (`table.turn`).
- **Void…**: ADR 0176's sheet, opened on the same table.

### S6 — Tutorials and puzzles (metadata) (done)

- **The briefing.** A practice started from its cut (a first open, or Restart the round) opens on a card before its
  first question: the scenario's title, its goal (or "play the position your way"), the seat the person plays and
  the round, against the recorded opponent, and that nothing played here counts. **Begin** closes it. The band's
  **Briefing** shows it again. A practice resumed after a reload goes on where it was, without it. The page's part is
  `practiceBrief` in `table/battle-practice.js`.
- **Listed under Practice.** The Scenarios tab's **list under Practice** / **take off Practice**
  (`PUT /api/admin/scenarios/<id>/listing {"listed"}`, noted in Activity as `scenario.list` and `scenario.unlist`)
  marks a scenario listed. A scenario is cut unlisted. Being listed is the host's: it is kept beside the scenario as
  `listing.json`, never in the `scenario.json` it is shared as. `GET /api/scenarios` (signed in, 401 otherwise)
  lists the listed ones, newest first. A signed-in player's home page shows them in a **Practice** section, each card
  opening its link (`table/lobby-practice.js`); a guest, or a host with nothing listed, shows none. One left off the
  list still opens by its link.
- Tests: `ScenariosApiTests` (listed and taken off, the operator's list, Activity, refusals, signed out),
  `admin-scenarios.test.js`, `lobby-practice.test.js`, `battle-practice.test.js`, and in the browser
  `admin.ux.spec.js` (the toggle), `lobby-home.spec.js` (the section) and `battle-services.spec.js` (the briefing on a
  fresh practice, again from the band, and not on a resumed one).
- Later, not in this plan: success conditions, an order to the listed scenarios, and moving the nine `--practice`
  recipes into scenario files.

## Docs to update along the way

`AGENTS.md` (the commands and routes), `docs/glossary` terms, `table/` README lines for the admin panel,
`docs/architecture/stateless-table-inventory.md` (a second kind of ephemeral table), the ADR index.

## Open questions

1. **What players read when the operator voids a match.** ADR 0176 shows them the operator's explanation; the
   owner leaned toward a fixed sentence here. Either keep ADR 0176 as it is, or supersede it with an ADR that
   keeps the explanation for the operator and shows the players a fixed sentence. Nothing in this plan depends
   on the answer.
