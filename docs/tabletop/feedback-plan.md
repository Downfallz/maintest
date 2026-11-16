# Plan: feedback, a bug report to a regression test and an idea to a plan

Status: accepted, 2026-11-16 (the owner settled the questions at the end). R0 to R3 and I1 are built; R4 is next.

## What the owner asked for

A way for a player to report a bug with its context, and for an admin to check it easily, reproduce it and open
an issue. In the owner's words, two examples:

- **QA-01, a reproducible bug.** Attach a redacted position to a report, and reproduce a rules anomaly in a test.
- **A bug reproduction factory.** Turn a report into a playable scenario and a regression test.

Then, the same day: in the same spirit, a player should be able to submit a feature they would like. Where it goes
and how it crosses the bug reports was left to this plan, from what established practice and the repository's own
formats do (see "Bugs and ideas: one door, two roads").

## What exists today

| Concern | What the code and the docs hold |
| --- | --- |
| Player notes | `POST /api/notes` (`TableApi.NoteAsync`, `TableNoteBody`) records a `Lookup`, a `Misplay` or a `Comment` in the session's `notes.jsonl`, with the round and sub-phase (docs/tabletop/playtest-app.md, 5.3). Only the classic table sends them (`table/notes.js`, `table/transport.js`). The battle view (`/battle`), where every seat plays since ADR 0157, has no such button. A note has no status, and is read only through a session's export. |
| A position, cut and replayed | A match scenario is a recorded match cut at the top of a round, rebuilt from its journal (ADR 0179, docs/tabletop/scenarios-plan.md). The operator cuts one in the panel (`ScenarioAdminApi`, `/admin#scenarios`), a signed-in player practises it at `/battle?scenario=<id>`, and `scenario.json` carries it anywhere (`ScenarioFile`). |
| A position as a test | Every file under `scenarios/` is rebuilt by `ScenarioFileTests` on each engine change, and its source's later answers must still apply. No file says what the engine should do from the cut: the test proves the position holds, not that a rule behaves. |
| A table that misbehaves | The diagnosis sheet (scenarios plan, S5b): public state, Check replay, Reveal hidden state, Requeue the bot's turn, Void. |
| What the operator did | The Activity tab (admin plan, A6) and the Overview's cards (A5). |
| The page's own errors | Nothing catches them: no `error` or `unhandledrejection` handler in `table/*.js` (searched 2026-11-16). A script error a player meets is lost. |
| Ideas | `.github/ISSUE_TEMPLATE/feature_request.yml` asks for the problem and the proposal (label `enhancement`). `/proposals` files issues labelled `proposal`, "ideas nobody asked for yet", from what `product-scout` and `ops-analyst` read in the files; `/new-plan` turns an idea into a plan. A player has no way to say what they would like: a `Comment` note is a sentence in one session's record. |
| Issues | `.github/ISSUE_TEMPLATE/bug_report.yml` asks for the expected and actual behaviour and a reproduction. `/triage` sorts the open issues. The host holds no GitHub credential: it deploys by federation (ADR 0082). |
| Who can read an issue | The repository is public (ADR 0186), so an issue and a file under `scenarios/` are public. A scenario file carries the source's session, run and match ids, and each journal line's time (`source`, `before[].at`). |
| Sizes, measured | `scenarios/greedy-1v1-round-3.json` is 75,589 bytes with its content and 11,031 without. `scenarios/tutorials/your-first-combat.json` is 119,125 and 38,495. A pre-filled new-issue address stays usable under about 8 KB, and an issue's body holds 65,536 characters at most: the file travels as an attachment, not in the issue's text. |

What is missing is the path between these: a report a player can send from the game, a place where the operator
reads it beside the position it names, and a road from that position to a file in `scenarios/` that fails until
the rule is fixed. For ideas, the missing piece is smaller: a door, a place where the same idea from many players
becomes one, and a road to `/new-plan`.

## Bugs and ideas: one door, two roads

What feedback tools commonly do, and what this plan keeps:

- **One door, a choice behind it.** A player rarely knows whether what bothers them is a bug or a rule working as
  designed. Asking them to pick a door first loses the report when they pick wrong. One entry, **Report or
  suggest**, opens a sheet whose first question is *Something is wrong* or *I have an idea*. The two share the
  sender, the store, the guards against spam, the operator's tab and the notice that closes the loop.
- **The operator moves it.** *Works as designed* is the most common answer to a rules bug, and the report then is
  an idea in disguise. **This is an idea** turns a bug report into one, keeping its words and its position, and
  **This is a bug** does the reverse. Both are Activity lines.
- **The problem before the solution.** An idea asks *what were you trying to do* and *what got in the way*, then
  *how you would like it*, optional. A request for a button is a guess at a fix; the problem behind it is what a
  plan can weigh. The repository's `feature_request.yml` already asks for the problem first.
- **Group, do not count votes.** The same idea from five players is one idea asked by five. The operator merges
  duplicates into one, which keeps every sender, and the count is the signal. No public board and no votes: at
  the host's size, tens to hundreds of accounts, a board would mostly be empty and need moderation (the owner's
  choice, 2026-11-16).
- **Close the loop.** A player who hears back reports again. A bug report's sender is told when it is fixed; an
  idea's senders, every one of a merged group, when it ships or is declined, with the operator's reason.
- **Two roads.** A bug report goes to a public issue, a scenario and a test (below). An idea stays in the panel
  (the owner's choice): the operator marks it *planned* with the plan's path, and **Copy as a plan prompt** gives
  `/new-plan <the idea, in the operator's words, asked by N players>` to paste into a session. No player's text
  reaches the public repository without the operator writing it.

The word for both is **feedback**: a bug report or an idea. The rest of this plan says *report* for a bug report
and *idea* for an idea.

## The report

A **bug report** is what a player sends with **Report or suggest**, *Something is wrong*: a category, what
happened, what they expected, and the context the host and the page add on their own. It is not a note: a note
belongs to a session's record and feeds the learning loop; a report has a life of its own (new, seen, reproduced,
filed, fixed, dismissed).

### Where a player reports from

- **The battle view**: in the top bar's menu, beside Help and Concede. Also on a finished game's recap, a replay,
  a practice and a tutorial.
- **The lobby's menu** (`table/navigation.js`), for anything outside a game.

The same entry, in both places, opens the sheet on its first question: *Something is wrong* or *I have an idea*.
For a bug report, it then asks three things and no more, so it is sent:

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

`Feedback` (its `FeedbackKind`, bug or idea, and its `FeedbackStatus`) and the port `IFeedbackStore`
(Application), kept one record a piece of feedback beside the recordings, in a `feedback` directory or container
of its own, as the tending runs and the operator log are (admin plan, A3 and A6). One is kept 180 days after it is
closed (fixed, shipped, declined or dismissed), then the tending pass lets it go. A deleted account's feedback
keeps its text and loses the account.

Feedback is personal data: the *What we keep* panel says so, in one line.

### Against spam

Asked by the owner on 2026-11-16: a player must not be able to flood the tab. Five guards, checked by the host in
this order, each refusal a `429` with a code and a sentence the sheet shows. They count bug reports and ideas
together: one sender, one budget.

1. **One at a time.** Two minutes at least between two pieces of feedback from one sender (`Feedback.TooSoon`,
   with the seconds left). A sender is the account when signed in, else the seat's key.
2. **Five a day.** Five a day per sender, reports and ideas together (`Feedback.TooMany`).
3. **Five a day per address, for guests.** A guest opens a table, and so a new seat, in one request, so the seat
   alone bounds nothing. A guest's feedback is also counted per network address, five a day across every seat it
   holds. The address is counted in the process's memory, hashed, and never kept with the report.
4. **Once per position.** A second report from the same sender, table and round is not a new report: its text is
   added to the first one (`Feedback.Merged`, answered 200 with the first one's id), so a player who taps twice
   leaves one row. For an idea, the same holds for a second one from the same sender within the hour.
5. **A host-wide ceiling.** Past 100 new pieces of feedback in a day the host takes no more
   (`Feedback.HostFull`) and the Overview's card says so in the warning colour. A flood from many senders costs
   the operator one look, not a thousand rows.

The operator has the last word. **Dismiss as spam** on a report or an idea counts against its sender; a sender
with three dismissed as spam in 30 days, or one the operator mutes from the sheet (**Stop feedback from this
player**), no longer sees **Report or suggest**: the host answers `403 Feedback.Muted` and the page hides the
entry. A mute is lifted from the sheet, or ends on its own after 30 days. Mutes and spam marks are lines in the
Activity tab (`feedback.spam`, `feedback.mute`, `feedback.unmute`). An account's mute is kept with its activity
record (ADR 0174); a guest's dies with its seat.

The sheet itself discourages noise without a captcha: **Send** waits until the required text holds 10 characters, and
the sheet says what the host will attach, so a player knows a short report is enough.

## The operator's side: the Feedback tab

`/admin#feedback`, the admin plan's shared list: newest first, twenty a page, filtered by kind, status and
category. A row is the kind, the category, the first line of what happened (or of the idea), the table and round
for a report, *asked by N* for an idea, and the status. The Overview gains a card, *feedback waiting*, drawn in the
warning colour while a bug report is new.

A bug report's row opens its sheet:

- the report's words, its category, the page, the browser and the page's errors;
- the **position**: the seat's view drawn read-only, with the round and sub-phase it was sent at;
- the table: a link to its session, and **Diagnose…** when it is still under way (S5b, reused, not rebuilt);
- **Reproduce**: cuts a scenario at the top of the reported round, in the reporter's seat, through the existing
  `POST /api/admin/scenarios`. Its title is *Report <id>, round N* and its goal is empty, so no player's words
  reach a file that may become public; the operator may write both. The answer is the practice link: the operator
  plays the round and sees the bug, or does not. A report from a practice is cut from its copied entries instead. A
  round the journal does not offer answers with the rounds it does, as the scenarios sheet already says;
- **File an issue** (below);
- **This is an idea**, when the rules worked as designed;
- the status, changed by hand: *seen*, *reproduced*, *dismissed* with a reason. *Filed* and *fixed* are set by
  the issue.

Every change is a line in the Activity tab (`feedback.status`, `feedback.reproduce`, `feedback.file`,
`feedback.convert`).

## Ideas

**The sheet.** *I have an idea* asks:

- **where**: *the rules*, *the battle*, *the lobby and friends*, *something else*;
- **what were you trying to do, and what got in the way** (required, 10 to 2,000 characters);
- **how you would like it** (optional).

No position is attached: an idea is about the game, not a moment of it. The host adds the account, the page, and
the table and round when it was sent from one, so the operator can see where it came from.

**The operator's sheet.** The idea's words, every sender's when merged, and:

- **Merge into…**: picks another idea; this one joins it, its sender and words kept, and the target's count goes up.
  A merged idea leaves the list and is read inside the one it joined (`feedback.merge`);
- **This is a bug**, when the idea describes a rule not doing what the rules say. It carries no position, so
  **Reproduce** cuts from the table and round it was sent from, when there was one;
- the status: *seen*, *planned* with the plan's path (`docs/…-plan.md`), *shipped*, *declined* with a reason the
  players read;
- **Copy as a plan prompt**: `/new-plan` followed by a text the operator writes from the idea, never the player's
  words pasted unread, and "asked by N players since <date>".

The list sorts ideas by *asked by*, then by the newest ask, so what many players want comes first.

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
| Application | `Feedback`, `FeedbackKind`, `FeedbackStatus`, `IFeedbackStore`, `RedactedScenario` |
| Infrastructure | The file and blob adapters for `IFeedbackStore` |
| Cli | `FeedbackApi` (the seat's and the account's `POST /api/feedback`), `FeedbackAdminApi` (the tab's routes and the zip), `scenario-import`, `scenario-check --trace` |
| table/ | `feedback.js` (the sheet and the error catcher, pure parts node-tested), `admin-feedback.js`, the entries in the battle view's menu and the lobby's |
| Tests | `BugScenario` and one class per bug under `tests/DownfallArena.Infrastructure.Tests/Matches/Bugs/` |
| Repository | `.github/ISSUE_TEMPLATE/bug_from_report.yml`, the `from-report` label in `.github/labels.json`, `scenarios/bugs/`, `.claude/skills/repro/` |

## The routes

| Route | Who | What |
| --- | --- | --- |
| `POST /api/feedback` | seat, account | Sends a bug report `{"kind":"bug","category","happened","expected?","attach","page","errors"}` or an idea `{"kind":"idea","where","problem","wish?","page"}`; `429` `Feedback.TooSoon`, `Feedback.TooMany` or `Feedback.HostFull`, `403 Feedback.Muted`, `200 Feedback.Merged` (see "Against spam"). |
| `GET /api/admin/feedback?kind&status&category&page`, `GET /api/admin/feedback/<id>` | operator | The Feedback tab and one sheet. |
| `PUT /api/admin/feedback/<id>/status {"status","reason?","issue?","plan?"}` | operator | Seen, reproduced, filed with the issue's number, planned with the plan's path, shipped, declined or dismissed with a reason (`spam` among them). |
| `PUT /api/admin/feedback/<id>/kind {"kind"}` | operator | This is an idea, this is a bug. |
| `POST /api/admin/feedback/<id>/merge {"into"}` | operator | Merges an idea into another. |
| `POST`/`DELETE /api/admin/feedback/<id>/mute` | operator | Stops the sender from sending feedback for 30 days; lifts it. |
| `POST /api/admin/feedback/<id>/reproduce` | operator | Cuts the scenario at the reported round, in the reporter's seat; answers the practice link. |
| `GET /api/admin/feedback/<id>/scenario.zip` | operator | The redacted scenario, as a zip to attach to the issue. |

## Stages

Each stage is one pull request that leaves the gate green. Ideas come right after the tab: they need no scenario,
and they are the first thing every player can use.

1. **R0, the ADR and the words.** An ADR: what feedback keeps, who reads it, how long, that ideas stay with the
   host, and that only a redacted scenario leaves it, downloaded by the operator for a public issue they file by
   hand. Glossary: **Feedback**, **Bug report**, **Idea**, **Redacted scenario**, **Bug scenario**. No code.

   *Built (2026-11-16)*: ADR 0189 and the five glossary terms. The ADR decides that feedback is kept at the
   host, read by the operator only, and deleted 180 days after it is closed; that one budget guards against spam;
   that ideas are never filed; and that a bug report leaves only as a redacted scenario the operator attaches by
   hand. No code changed. R1 is next.

2. **R1, feedback kept.** `Feedback`, `IFeedbackStore` and its two adapters, `POST /api/feedback` for both kinds
   from a seat key or a signed-in account, the host's context for a report (table, seat, round, sub-phase, journal
   length, content, rules, engine, the seat's view, a practice's entries), the guards against spam over both kinds
   (two minutes apart, five a day per sender, five a day per address for guests, merged per position, the host's
   ceiling), the 180 days in the tending pass. Tests: `FeedbackApiTests` (a report from a seat, an idea from an
   account, signed out and seatless refused, each guard refused or merged with its code, a guest's new seat still
   counted against its address, a practice's entries copied, the other seat's hidden state absent from the
   position), the adapters' tests on Azurite.

   *Built (2026-11-16)*: `Feedback`, `FeedbackKind`, `FeedbackStatus` and the port `IFeedbackStore` in Application
   (`FeedbackBox/`), the file and blob adapters in a `feedback` directory or container beside the recordings, and
   `POST /api/feedback` (`FeedbackApi`). The guards are `SendFeedback`, one write at a time in the process
   (`FeedbackWrites`), so a burst sent at once cannot pass them all. Text merged into earlier feedback counts
   against the five a day, though it adds no row. The same words sent again within two minutes are told they were
   kept, so a page that lost its answer is not told it was too soon. A guest's address is counted by
   `GuestAddresses`, hashed with a salt the process draws (`RequestAddress`): the one Cloudflare saw when the peer
   is Cloudflare, else the last hop the ingress added, an IPv6 address by its /64. A sender is the account, or a
   hash of the seat's key, never the key. The page is kept as its path alone, and the errors without their
   addresses' queries. The tending pass, hourly or from the panel, deletes what was closed 180 days ago
   (`LetGoOfClosedFeedback`). Deleting an account removes the account from its feedback first
   (`ForgetFeedbackAccount`). Left to R3: `Feedback.Muted` and the spam marks, since nothing can mute before the
   tab exists. Left to R2: the *What we keep* line.

3. **R2, Report or suggest.** The entry in the battle view's menu, on the recap, in a practice and in the lobby's
   menu; the sheet's two choices and their fields; the page's error catcher; the *What we keep* line. Tests:
   `feedback.test.js` (the sheet's model for both kinds, the errors kept to ten, nothing written to storage, each
   refusal's sentence, the entry hidden once muted), `terminology.test.js`, a browser spec sending a report from a
   seat and an idea from the lobby at 320 px.

   *Built (2026-11-16)*: `table/feedback-sheet.js`, named so because `feedback.js` already holds the sound and
   vibration cues. **Report or suggest…** sits in the battle view's Match settings, above Concede (Match settings
   also opens in a practice, a tutorial and a replay), and on the result's last line. Outside a game it sits in the
   account menu, beside Settings, on every page: a guest outside a game has no seat to send from, and the navigation
   bar has no room for one more item on a laptop. In the battle view the account menu's entry opens the battle's own
   sheet, with the seat on screen. The sheet asks the kind, then the three questions of each; Send stays off until
   the required answer has ten characters; a refusal keeps the words; a sheet closed while it sends is emptied once
   the host kept it. `403 Feedback.Muted` hides every entry for the page's life (`data-feedback-muted` on the root);
   the host sends it from R3, which must answer it 403, not 429. The battle view and every other page with the menu,
   the archived classic table included, catch their last ten errors in memory, each address's query cut. The *What we
   keep* panel has its line, and the game website's privacy policy names feedback too, in English and French. Tests:
   `feedback-sheet.test.js`, `studio/browser/feedback.spec.js` (a report from a seat with its position and error, a
   refusal and a mute, a sheet closed while sending, the same entry from the account menu in a game, the result's
   entry, an idea from the lobby's account menu, a guest offered nothing, the privacy line), at 320 px, 390 px and on
   a desktop.

4. **R3, the Feedback tab.** The list filtered by kind, a report's sheet with the position drawn read-only,
   Diagnose… on a table under way, the status by hand, **This is an idea** and **This is a bug**, **Dismiss as
   spam** and **Stop feedback from this player** (and lifting it), the Activity lines, the Overview card. Tests: a
   kind changed both ways with its words kept, a muted sender refused with `Feedback.Muted`, three spam marks in 30
   days muting, a mute ending after 30 days (`FeedbackAdminApiTests`), `admin-feedback.test.js`,
   `admin.ux.spec.js`.

   *Built (2026-11-16)*: `OperatorFeedback` (Application) and `FeedbackAdminApi` behind the panel's gate:
   `GET /api/admin/feedback` filtered by kind, status (`open`, the tab's default, for what is not closed) and
   category, twenty a page; a sheet with the words, what the page and the host added, the position drawn read-only
   as each side's creatures, the session's link, **Diagnose…** while its table is under way, and the `actions` that
   apply, which the page shows and decides none of. *Seen* reopens, *reproduced* is a bug report's, *dismissed* takes
   a one-line reason, `spam` being the mark. Turning a report into the other kind keeps its words, position and
   sender and marks it seen. A category the other kind lacks becomes *something else*. A closed one is refused.
   Every change asked again writes and notes nothing, so a phone's retry adds no Activity line and re-mutes nobody;
   dismissed again, it keeps the time it was first closed. A mute (`FeedbackMute`, owned by Accounts, 30 days) is
   kept with an account's activity record (ADR 0174) and dropped at its first write after the mute ends; a guest's
   is kept in the process's memory, as its address count is, so a restart lifts it. The third spam mark within 30
   days mutes on its own, counted outside the write gate so players' sending never waits on it. `SendFeedback`
   refuses a muted sender `403 Feedback.Muted` before its guards. The Activity lines are `feedback.status`,
   `feedback.convert`, `feedback.spam`, `feedback.mute` and `feedback.unmute`; a spam mark and the mute it earns
   are noted at one instant, so their order between them is not kept. The Overview's *Feedback waiting* card is in
   the warning colour while a bug report is new or the host is full. The tab's ids start with `admin-feedback-`,
   because every page's Report or suggest sheet already uses `feedback-`. The privacy panel and policy say the
   operator can stop a player's feedback for 30 days. Left: a page learns that its player is muted only when it
   sends (R2's behaviour). The list, the Overview and the spam count read every record kept: up to 100 a day, each
   kept until 180 days after it is closed, so a few thousand at most, read by the operator alone.
5. **I1, ideas grouped.** **Merge into…**, *asked by N* and the sort by it, *planned* with a plan's path,
   *shipped*, *declined* with a reason, **Copy as a plan prompt**. Tests: a merge keeping every sender and adding
   to the count, a merged idea out of the list and read inside its target, a merge into a bug report or into
   itself refused, the prompt built from the operator's text (`admin-feedback.test.js`).

   *Built (2026-11-16)*: a merged idea keeps its own record, so the spam guards still count its sender, and names the
   group's idea it joined (`Feedback.MergedInto`). The group's idea lists the ideas that joined it (`Feedback.Joined`).
   Groups stay flat: merging an idea others joined moves them along, and a merge into an idea merged since joins its
   group. A change to a group reads its records, never the whole store, under the write gate.

   *Asked by* counts each sender once and each deleted account apart (`FeedbackAsks`). The list filtered to ideas sorts
   by it, then by the newest ask. Merging a new idea marks it seen, so it leaves the Overview's count.

   `POST /api/admin/feedback/<id>/merge` merges an open idea into an open idea. It refuses a bug report, the idea
   itself and its own group. A merged idea takes no status but dismissed, which takes it out of its group, so a flood
   merged by mistake is dismissed as spam and counted. It takes no kind (`Feedback.MergedAway`). An idea others
   joined stays an idea (`Feedback.Grouped`). A player's next idea within the hour is not added to one merged away.

   *Planned* takes a path shaped like the repository's plans (`PlanPath`). The plan stays until another replaces it.
   *Shipped* closes the idea; *declined* takes a reason of one line and closes it. The tending pass deletes a merged
   idea with the group's idea, or once that one is no longer listed; one listed but unreadable keeps what joined it.
   The Activity line is `feedback.merge`.

   **Copy as a plan prompt** writes the operator's words on one line with "Asked by N players since <date>". It shows
   the prompt to select when the browser refuses the clipboard. **Merge into…** reads the open ideas when the
   operator asks for them, the first five pages, 100 at most, and says so when more are open. An idea's sheet reads
   every record kept, as the list does.

   Left: no undo for a merge. Merging again moves an idea to another group; dismissing takes it out. R8 will tell the
   players when their idea is shipped or declined.

6. **R4, Reproduce.** The button over `ScenarioAdminApi`: the cut at the reported round in the reporter's seat,
   from the session or from a practice's copied entries, the practice link. Tests: a report on a recorded greedy
   3v3 cut and opened as a practice on its board; a round the journal does not offer; a practice's report cut.
7. **R5, the redacted scenario and the issue.** `RedactedScenario`, **Download the scenario** as a zip, the issue
   form `bug_from_report.yml`, the `from-report` label, **File an issue** with its pre-filled address, *filed #N*,
   and *fixed* read from GitHub. Tests: `RedactedScenarioTests` (every id replaced, times at the epoch, a file
   holding a name refused), the zip holding one `scenario.json` that rebuilds, the address built under 8 KB
   (`admin-feedback.test.js`), `studio/prompts.test.js` for the label.
8. **R6, bug scenarios and their tests.** `scenarios/bugs/` read by the host and listed as *Bug #N*;
   `ScenarioFileTests` asking a bug file's answers only up to its reported point; the `BugScenario` test helper;
   `scenario-import` and `scenario-check --trace`. Tests: `RepositoryScenariosTests` (a bug file listed),
   `CliOptionsTests`, a zip refused when it is not redacted or does not rebuild, and one bug file with its test,
   made from a recorded greedy match.
9. **R7, the /repro skill.** `.claude/skills/repro/`, `scenarios/README.md`, the command reference. Tested by its
   first real report: a bug filed from the panel, turned into a file under `scenarios/bugs/` and a test that was
   red before its fix.
10. **R8, the player told.** In the bell, once, for a signed-in sender: a report *fixed*; an idea *shipped*, or
    *declined* with the operator's reason, to every sender of a merged group. Tests: each notice written once,
    none for a guest, every sender of a merged idea told, the bell's count (`FeedbackApiTests`, the bell's page
    test).

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
7. **Against spam** (asked after the first six): two minutes between reports, five a day per sender and per guest
   address, one report per position, a ceiling of 100 a day for the host, and the operator's spam mark and mute.
8. **Ideas, one door with bugs** (asked after the first seven): one entry, **Report or suggest**, whose first
   question is a bug or an idea; one store, one budget against spam, one tab filtered by kind; the operator moves
   one kind to the other.
9. **Ideas are private and grouped:** only the operator reads them, merges duplicates, and reads *asked by N*. No
   public board and no votes.
10. **An idea stays in the panel:** no issue; *planned* with the plan's path, and **Copy as a plan prompt** for
    `/new-plan`.
11. **An idea's senders are told** when it ships, or is declined with the operator's reason.
