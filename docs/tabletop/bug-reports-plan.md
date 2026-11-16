# Plan: bug reports, from the table to a regression test

Status: accepted, 2026-11-16 (the owner settled the questions at the end). Nothing is built.

## What the owner asked for

A way for a player to report a bug with its context, and for an admin to check it easily, reproduce it and open
an issue. In the owner's words, two examples:

- **QA-01, a reproducible bug.** Attach a redacted position to a report, and reproduce a rules anomaly in a test.
- **A bug reproduction factory.** Turn a report into a playable scenario and a regression test.

## What exists today

| Concern | What the code and the docs hold |
| --- | --- |
| Player notes | `POST /api/notes` (`TableApi.NoteAsync`, `TableNoteBody`) records a `Lookup`, a `Misplay` or a `Comment` in the session's `notes.jsonl`, with the round and sub-phase (docs/tabletop/playtest-app.md, 5.3). Only the classic table sends them (`table/notes.js`, `table/transport.js`). The battle view (`/battle`), where every seat plays since ADR 0157, has no such button. A note has no status, and is read only through a session's export. |
| A position, cut and replayed | A match scenario is a recorded match cut at the top of a round, rebuilt from its journal (ADR 0179, docs/tabletop/scenarios-plan.md). The operator cuts one in the panel (`ScenarioAdminApi`, `/admin#scenarios`), a signed-in player practises it at `/battle?scenario=<id>`, and `scenario.json` carries it anywhere (`ScenarioFile`). |
| A position as a test | Every file under `scenarios/` is rebuilt by `ScenarioFileTests` on each engine change, and its source's later answers must still apply. No file says what the engine should do from the cut: the test proves the position holds, not that a rule behaves. |
| A table that misbehaves | The diagnosis sheet (scenarios plan, S5b): public state, Check replay, Reveal hidden state, Requeue the bot's turn, Void. |
| What the operator did | The Activity tab (admin plan, A6) and the Overview's cards (A5). |
| The page's own errors | Nothing catches them: no `error` or `unhandledrejection` handler in `table/*.js` (searched 2026-11-16). A script error a player meets is lost. |
| Issues | `.github/ISSUE_TEMPLATE/bug_report.yml` asks for the expected and actual behaviour and a reproduction. `/triage` sorts the open issues. The host holds no GitHub credential: it deploys by federation (ADR 0082). |
| Who can read an issue | The repository is public (ADR 0186), so an issue and a file under `scenarios/` are public. A scenario file carries the source's session, run and match ids, and each journal line's time (`source`, `before[].at`). |
| Sizes, measured | `scenarios/greedy-1v1-round-3.json` is 75,589 bytes with its content and 11,031 without. `scenarios/tutorials/your-first-combat.json` is 119,125 and 38,495. A pre-filled new-issue address stays usable under about 8 KB, and an issue's body holds 65,536 characters at most: the file travels as an attachment, not in the issue's text. |

What is missing is the path between these: a report a player can send from the game, a place where the operator
reads it beside the position it names, and a road from that position to a file in `scenarios/` that fails until
the rule is fixed.

## The report

A **bug report** is what a player sends with **Report a problem**: a category, what happened, what they expected,
and the context the host and the page add on their own. It is not a note: a note belongs to a session's record and
feeds the learning loop; a report has a life of its own (new, seen, reproduced, filed, fixed, dismissed).

### Where a player reports from

- **The battle view**: in the top bar's menu, beside Help and Concede. Also on a finished game's recap, a replay,
  a practice and a tutorial.
- **The lobby's menu** (`table/navigation.js`), for anything outside a game.

The sheet asks three things and no more, so it is sent:

- a **category**: *the rules did something wrong*, *something looks wrong*, *it is stuck or slow*, *something else*;
- **what happened** (required, 10 to 2,000 characters) and **what you expected** (optional);
- **attach this position**, ticked by default in a game.

### What the host attaches

The page sends the text and the category. The host adds what it knows and the page cannot be trusted with:

| Field | From |
| --- | --- |
| `table`, `seat`, `round`, `subPhase`, `journalLength` | The seat the request's key names, as `NoteAsync` finds them (`WhereAsync`). |
| `content`, `rules`, `engine` | The table's record and the host's build (ADR 0163). |
| `position` | The seat's own view at that moment: the projection the page already receives, which holds nothing the seat may not see (docs/tabletop/playtest-app.md, the hidden-information boundary). |
| `scenario`, `practiceEntries` | In a practice: the scenario's id and the practice's journal since its cut, copied, since a practice is kept nowhere (ADR 0079). |
| `account`, `at` | The session, and `TimeProvider`. |

The page adds `page` (the address without its query), the browser's user agent, and its **last errors**: the
last ten `error` and `unhandledrejection` messages it caught, each a message and a file:line, kept in memory and
never stored on the device.

The journal is not copied: the host keeps it with the session, and a cut reads it there. A report holds the
journal's length, so the operator knows where the player stood when they wrote.

### Where it is kept

`BugReport` and the port `IBugReports` (Application), kept one record a report beside the recordings, in a
`reports` directory or container of its own, as the tending runs and the operator log are (admin plan, A3 and A6).
A report is kept 180 days after it is closed (fixed or dismissed), then the tending pass lets it go. A deleted
account's reports keep their text and lose the account.

A seat or an account sends five reports a day at most (`429 Report.TooMany`). A report is personal data: the
*What we keep* panel says so, in one line.

## The operator's side: the Reports tab

`/admin#reports`, the admin plan's shared list: newest first, twenty a page, filtered by status and category. A
row is the category, the first line of what happened, the table and round, and the status. The Overview gains a
card, *reports waiting*, drawn in the warning colour while one is new.

A row opens the sheet:

- the report's words, its category, the page, the browser and the page's errors;
- the **position**: the seat's view drawn read-only, with the round and sub-phase it was sent at;
- the table: a link to its session, and **Diagnose…** when it is still under way (S5b, reused, not rebuilt);
- **Reproduce**: cuts a scenario at the top of the reported round, in the reporter's seat, through the existing
  `POST /api/admin/scenarios`. Its title is *Report <id>, round N* and its goal is empty, so no player's words
  reach a file that may become public; the operator may write both. The answer is the practice link: the operator plays the round and sees the bug, or does not. A report from a
  practice is cut from its copied entries instead. A round the journal does not offer answers with the rounds it
  does, as the scenarios sheet already says;
- **File an issue** (below);
- the status, changed by hand: *seen*, *reproduced*, *dismissed* with a reason. *Filed* and *fixed* are set by
  the issue.

Every change is a line in the Activity tab (`report.status`, `report.reproduce`, `report.file`).

## The redacted scenario

The repository is public, so what leaves the host for GitHub is a **redacted scenario**: the scenario the
Reproduce button cut, written as `scenario.json` with its content (`--with-content`), and stripped of what names a
person or a session:

- `source.session`, `source.run` and `source.match` are replaced by the report's own id;
- every journal line's `at` is set to the epoch, as `scenario-generate` already writes them;
- the title and goal are the operator's, never the player's text verbatim;
- no account id, player name, seat key or table id. A check refuses a file that holds any of the report's
  account id, the table id or the player names (`RedactedScenario`, Application, tested on its own).

**Download the scenario** in the sheet answers it as a zip, `report-<id>.zip`, holding `scenario.json` (GitHub
attaches a zip to an issue, not a `.json`). The operator attaches it to the issue by hand, from the phone as well:
the issue form's attachment field takes a file the browser just downloaded. Once attached it is public, which is
why it is redacted before it is offered at all.

## The issue

**File an issue** opens GitHub's new-issue form in the operator's browser, pre-filled from a new issue form
`.github/ISSUE_TEMPLATE/bug_from_report.yml` (labels `bug` and `from-report`): the category, what happened and
what was expected (the operator edits the player's words before submitting), the round and sub-phase, the engine
commit and the content hash, and the page's errors. Its last field asks for the zip, which the operator drops in.
The operator reads what becomes public and submits it; the host never writes to GitHub.

The operator pastes the issue's number back in the sheet, and the report reads *filed #N*. When the issue closes,
the report reads *fixed*: the panel asks GitHub's public API for the state of the filed issues when the tab
opens, which needs no credential on a public repository.

## The factory: from the issue to a red test

**The files' place.** `scenarios/bugs/<issue>-<slug>.json`, the redacted scenario, its content carried. The host
lists them in the Scenarios tab with the others, marked *Bug #N*, so the operator plays the fixed round from the
panel once it deploys. `ScenarioFileTests` rebuilds each at its cut, as it does every file; a bug file's recorded
answers are asked to apply only up to the point the player reported, since a fix may change what the recorded
player could do next.

**One C# test per bug.** `tests/DownfallArena.Infrastructure.Tests/Matches/Bugs/`, one class per issue, named as
the behaviour the fix restores (`A_bleed_ticks_in_the_upkeep_after_the_stun_that_hid_it_expires`), its issue number
in a comment. A test helper, `BugScenario`, opens a file under `scenarios/bugs/`, replays it to a number of entries
past the cut, and hands back both seats' boards and the events raised, so the test asserts with Shouldly what the
rule says. The test lands with its fix, in the same pull request: red before, green after, which is the testing
convention's "a bug fix ships with a test that failed before the fix".

**The command.** `scenario-import --zip <file> --issue <N>` unpacks the zip a session downloaded from the issue,
checks it is a redacted scenario that rebuilds on this engine, and writes `scenarios/bugs/<N>-<slug>.json`.
`scenario-check --trace` prints each seat's board after each entry past the cut, so the session finds the entry
where the rule went wrong.

**The skill.** `/repro <issue>` runs the road in a session: read the issue, download its zip, import it, trace it to
the reported point, write the test from the rule the issue quotes, see it red, fix the rule, see it green, ship. It
is `/stage` for a bug.

**The player told.** When a report reads *fixed*, its reporter's account gets a notice in the bell, "The problem
you reported on <date> is fixed", once (`AccountNotices`). A guest's report tells nobody.

## Where the code goes

| Layer | Adds |
| --- | --- |
| Application | `BugReport`, `BugReportStatus`, `IBugReports`, `RedactedScenario` |
| Infrastructure | The file and blob adapters for `IBugReports` |
| Cli | `ReportsApi` (the seat's and the account's `POST /api/reports`), `ReportAdminApi` (the tab's routes and the zip), `scenario-import`, `scenario-check --trace` |
| table/ | `report.js` (the sheet and the error catcher, pure parts node-tested), `admin-reports.js`, the entries in the battle view's menu and the lobby's |
| Tests | `BugScenario` and one class per bug under `tests/DownfallArena.Infrastructure.Tests/Matches/Bugs/` |
| Repository | `.github/ISSUE_TEMPLATE/bug_from_report.yml`, the `from-report` label in `.github/labels.json`, `scenarios/bugs/`, `.claude/skills/repro/` |

## The routes

| Route | Who | What |
| --- | --- | --- |
| `POST /api/reports` | seat, account | Sends a report: `{"category","happened","expected?","attach","page","errors"}`; `429 Report.TooMany` past five a day. |
| `GET /api/admin/reports?status&category&page`, `GET /api/admin/reports/<id>` | operator | The Reports tab and one report's sheet. |
| `PUT /api/admin/reports/<id>/status {"status","reason?","issue?"}` | operator | Seen, reproduced, dismissed with a reason, filed with the issue's number. |
| `POST /api/admin/reports/<id>/reproduce` | operator | Cuts the scenario at the reported round, in the reporter's seat; answers the practice link. |
| `GET /api/admin/reports/<id>/scenario.zip` | operator | The redacted scenario, as a zip to attach to the issue. |

## Stages

Each stage is one pull request that leaves the gate green.

1. **R0, the ADR and the words.** An ADR: what a bug report keeps, who reads it, how long, and that only a redacted
   scenario leaves the host, downloaded by the operator for a public issue they file by hand. Glossary:
   **Bug report**, **Redacted scenario**, **Bug scenario**. No code.
2. **R1, the report kept.** `BugReport`, `IBugReports` and its two adapters, `POST /api/reports` from a seat key
   or a signed-in account, the host's context (table, seat, round, sub-phase, journal length, content, rules,
   engine, the seat's view, a practice's entries), the daily bound, the 180 days in the tending pass. Tests:
   `ReportsApiTests` (from a seat, from an account, signed out and seatless refused, the bound, a practice's
   entries copied, the other seat's hidden state absent from the position), the adapters' tests on Azurite.
3. **R2, Report a problem.** The sheet in the battle view's menu, on the recap, in a practice and in the lobby's
   menu; the page's error catcher; the *What we keep* line. Tests: `report.test.js` (the sheet's model, the errors
   kept to ten, nothing written to storage), `terminology.test.js`, a browser spec sending one from a seat and one
   from the lobby at 320 px.
4. **R3, the Reports tab.** The list, the sheet with the position drawn read-only, Diagnose… on a table under way,
   the status by hand, the Activity lines, the Overview card. Tests: `ReportAdminApiTests`,
   `admin-reports.test.js`, `admin.ux.spec.js`.
5. **R4, Reproduce.** The button over `ScenarioAdminApi`: the cut at the reported round in the reporter's seat,
   from the session or from a practice's copied entries, the practice link. Tests: a report on a recorded greedy
   3v3 cut and opened as a practice on its board; a round the journal does not offer; a practice's report cut.
6. **R5, the redacted scenario and the issue.** `RedactedScenario`, **Download the scenario** as a zip, the issue
   form `bug_from_report.yml`, the `from-report` label, **File an issue** with its pre-filled address, *filed #N*,
   and *fixed* read from GitHub. Tests: `RedactedScenarioTests` (every id replaced, times at the epoch, a file
   holding a name refused), the zip holding one `scenario.json` that rebuilds, the address built under 8 KB
   (`admin-reports.test.js`), `studio/prompts.test.js` for the label.
7. **R6, bug scenarios and their tests.** `scenarios/bugs/` read by the host and listed as *Bug #N*;
   `ScenarioFileTests` asking a bug file's answers only up to its reported point; the `BugScenario` test helper;
   `scenario-import` and `scenario-check --trace`. Tests: `RepositoryScenariosTests` (a bug file listed),
   `CliOptionsTests`, a zip refused when it is not redacted or does not rebuild, and one bug file with its test,
   made from a recorded greedy match.
8. **R7, the /repro skill.** `.claude/skills/repro/`, `scenarios/README.md`, the command reference. Tested by its
   first real report: a bug filed from the panel, turned into a file under `scenarios/bugs/` and a test that was
   red before its fix.
9. **R8, the player told.** The *fixed* notice in the bell for a signed-in reporter, once. Tests: the notice
   written once when a report turns fixed, none for a guest, the bell's count (`ReportsApiTests`, the bell's
   page test).

## Questions the owner settled (2026-11-16)

1. **Who may report:** anyone holding a seat, guests included, and any signed-in account from the lobby.
2. **Who files the issue:** the operator, through GitHub's form pre-filled from the panel. The host holds no GitHub
   credential.
3. **How the position reaches the repository:** the operator downloads the redacted scenario as a zip in the panel
   and attaches it to the issue by hand. No link is served.
4. **The regression test:** one C# test per bug, reading its file under `scenarios/bugs/`, not an expectation
   written in the scenario file.
5. **The player is told:** yes, a notice in the bell once the report is fixed (R8).
6. **A closed report is kept** 180 days, then the tending pass lets it go.
