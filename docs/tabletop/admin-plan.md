# Plan: the admin panel as the host's headquarters

Status: accepted, 2026-11-13 (the owner settled the questions at the end). Nothing here is built yet; each stage below
is one pull request.

## What the owner asked for

Bring `/admin` up to the battle view and the lobby: the shared header at the top, several tabs, paging everywhere a
list can grow. Beside what it does today (open a table, the tables under way, the recorded sessions), it should
show:

- the players: every account, how many games each has played, and when each last signed in;
- the jobs: the hourly tending job's latest runs, and how many games each closed, timed out, or ended because a
  player never played;
- a switch that lets new accounts be created or not;
- and more of what a host's operator needs at a glance: a headquarters.

## What exists today

| Concern | What the host has | What `/admin` shows |
| --- | --- | --- |
| Operator door | Console token, platform sign-in, admin accounts (ADR 0081, 0172) | Everything, behind the door |
| Tables | The index of open tables, each table's seats, codes, pilot link (`GET /api/tables`) | One unpaged list, "Tables under way" |
| Sessions | Every recording in the store, filtered and paged twenty at a time (`GET /api/sessions`) | Recorded sessions, export, delete |
| Accounts | `IAccountStore`: find by id, subject or name; no listing; `CreatedAt` but no sign-in time | Nothing |
| Results | `IMatchResults.ListAsync` / `ListForAsync` (ADR 0168): every finished game, by match and by account | Nothing |
| Tending job | `table-tend` (ADR 0162) prints one line a run and one line a table to the job's console log | Nothing; the counts live only in Azure's log stream |
| Bot turns | The queues `agent-turns`, `agent-turns-poison`, `agent-turns-dlq` (ADR 0159, phase 5) | Nothing |
| Registration | Always open: a new Google subject is asked for a name (ADR 0122) | Nothing |

The page itself predates the identity work (ADR 0158): its own masthead, one long column, no tabs.

## The shape of the page

One page, `/admin`, under the same header as every other page (`table/navigation.js`): the brand, Lobby, Settings,
the account chip, and the Admin link lit. Under it, the panel's own title row (Headquarters, the rules the host runs,
a Refresh) and a row of tabs. A tab is part of the address (`/admin#players`), so a reload or a shared link opens it,
and Back moves between tabs. On a phone the tabs scroll sideways under the header, as the lobby's do.

| Tab | Holds |
| --- | --- |
| Overview | The numbers at a glance, each a link into its tab (below) |
| Tables | Open a table (today's form) and the tables under way, filtered and paged |
| Sessions | Today's recorded sessions, unchanged but for the shared list parts |
| Players | Every account: name, created, last signed in, last seen, games (ranked, against players, against bots), rating |
| Jobs | The tending runs, newest first, each with its counts and its sheet; Run a pass now; the bot turn queues, their failed turns, and Requeue or Drop |
| Settings | Registration open or closed, and the host's other switches |
| Activity | What operators did on this page: who closed, deleted, reinvited or switched what, and when |

Every list is the same part: a header row that sorts, a filter row, twenty rows a page, and Newer / Older with the
count between them, as the sessions list already has. A row opens a side sheet with the details and its actions;
nothing in a row acts without a confirmation.

### Overview

Cards, each one number and a word, linking to the tab that explains it:

- players: total, signed in during the last 7 days, new in the last 7 days;
- games: in progress (against players, against bots), finished today and in the last 7 days, void;
- waiting: open seats, invitations not yet answered, games whose time to answer runs out in the next day;
- jobs: the last tending run (when, and whether it ended well), bot turns waiting, turns that failed;
- registration: open or closed.

A card whose number needs attention (a failed run, turns in the poison queue, registration closed) is drawn in the
warning colour.

## The model

### A player's activity is kept beside the account

`Account` stays as it is: it is what a session is checked against. Beside it, one small record per account:

```
AccountActivity(AccountId, DateTimeOffset LastSignIn, DateTimeOffset LastSeen)
```

- `LastSignIn` is written when the account signs in (Google or dev): `AccountApi.SignedInAs`.
- `LastSeen` is written when a request carries the account's session, at most once an hour per account, so a
  playing account costs one write an hour, not one a request.
- Both are forgotten with the account, as its devices and preferences are.

This is new personal data, so the lobby's *What we keep* panel says it: "when you last signed in and last used the
site, so the host can see who still plays". It needs an ADR (below).

### The players list is read, never kept twice

`IAccountStore` gains a paged listing (`ListAsync(skip, take)`); the file store reads its directory, the blob store
lists its prefix. The Players tab joins, per page of twenty:

- the account (name, created);
- its activity (last sign-in, last seen);
- its results from `IMatchResults.ListForAsync`: games in all, ranked, against players, against bots, and wins;
- its rating from the ladder (ADR 0124).

Sorting by games or by last seen needs every account read once. That is fine at the size the host is (tens to
hundreds of accounts), and the plan says so: past a few thousand, the counts move into the activity record, written
when a result is kept.

### A tending run leaves a record

`table-tend` already computes everything the Jobs tab needs (`Tended`: tables that never began, invitations let go
of, games timed out, games left by a player, warnings sent). It writes it as one record per run, beside the
recordings:

```
TendRun(Started, Finished, Outcome: completed | failed, Error?,
        NeverBegan[], InvitationsExpired, TimedOut[], Abandoned[], Warned[])
```

A new port, `ITendLog` (Application), with a file adapter (`runs/tend/<started>.json`) and a blob one (a `tend`
prefix in the recordings' container), keeps the last 500 runs. The job writes its record when it ends, and also when
it fails, so a run that crashed shows as one. Each table id in a record links to its session in the Sessions tab.

The Jobs tab lists the runs newest first, twenty a page, with a total line for the last day and the last week:
"in 24 hours: 3 tables that never began closed, 1 game timed out, 0 abandoned".

A run can also be started by hand: **Run a pass now** in the Jobs tab runs the same tending pass in the API's
process (`TableTend`'s pass, not the Azure job), and records it like any other, marked `manual` with the operator's
name. A pass is safe beside the hourly one: each table it ends is closed through its record and claimed once
(`closing.json`), so two passes never end the same game twice. Each run opens a side sheet: when it started and
ended, who or what started it, its counts, every table it touched with what it did to it (closed, timed out, left,
warned) and a link to the session, and its error when it failed.

### The bot turns can be inspected and put back

The worker's queues already exist (ADR 0159, phase 5):

- `agent-turns`, the turns tables owe;
- `agent-turns-poison`, an item given up on after five deliveries, with its last error;
- `agent-turns-dlq`, where the second worker keeps each poisoned item for good, as a dead letter: the original item,
  the queue it came from, how many times it was delivered, the error, and when and where it was set aside.

Today an item that reaches the dead letters stays there, and its table waits for the next move to ask for its turn
again. The Jobs tab shows the three queues and lets the operator act on them:

- each queue's length, and the age of its oldest item;
- the dead letters (and anything still on the poison queue), twenty a page, newest first: the table and seat, how many
  deliveries, when it was set aside, the error's first line; a row opens the full letter and its error;
- **Requeue** on a letter, on the selected ones or on all of them: the original item goes back on `agent-turns`, as a
  new item with its deliveries counted from zero, and the letter is deleted once the item is sent;
- **Drop** on a letter or the selected ones, after a confirmation: deleted, nothing requeued.

Requeueing is safe whatever the table became: the worker finds the table before it takes a turn, and a table that is
over, closed or gone owes nothing, so the item is simply done with. A requeued item that fails five times again comes
back to the dead letters, with its new error. Every requeue and drop goes to the operator log.

The queue port (`IWorkChannel`) only receives (hiding what it receives) and deletes. It gains `PeekAsync(most)` and
`CountAsync()`, which read without hiding, implemented on the storage queues and on the directory ones; requeue is a
receive, a send to `agent-turns`, then a delete, in that order, so a failure in between leaves the letter where it
was.

### An operator can void a corrupted match, with a reason

Today a match ends as a **void match** only on its own: when its journal no longer replays on the engine that
picks it up (ADR 0163). The record is closed and marked void, the table leaves the index, no result is kept, each
seat's account is told, and a seat's page is answered `410 Gone` with the reason. The operator has no way to do
the same to a match that is stuck, broken by a bug, or abused, and no way to say why.

**Void…** on a table (Tables tab) or a session (Sessions tab) opens a sheet that asks for a reason, and nothing is
voided without one:

- a **category**, one of: the match is stuck, the engine misbehaved, a player abused the game, the operator made a
  mistake, other;
- an **explanation**, required, 10 to 500 characters, in words a player reads;
- a confirmation that names the table, its players and what will happen to its result.

What a void by the operator does is what ADR 0163's void does, and a little more:

- the record is closed and marked void, and keeps the reason: the category, the explanation, who voided it and
  when (`VoidReason` on the record, beside the existing `Void` mark);
- the table leaves the index, its pending bot turn is dropped, and its seats' keys answer `410` with the explanation,
  which the battle view and the seat discovery already show;
- each seat's account is told on its devices, in the same notice as an engine void, with the explanation;
- **a finished match can be voided too**: its kept result is removed from `IMatchResults`, so the ladder (computed
  from the results) and each player's statistics no longer count it. The finished games list shows it as voided,
  with the explanation, and its replay stays available;
- the operator log records the void, its category, explanation and operator.

The session list and the table list show a voided match with its category, and the Overview counts the voids of
the last 7 days. A void is final: there is no un-void, as with ADR 0163.

This changes what a kept result means (a result can now be withdrawn), so it gets an ADR that amends 0163 and 0168.

### Registration is a host setting

A host setting is not an account's preference: one record for the host, kept beside the accounts:

```
HostSettings(bool RegistrationOpen, string? Notice, ... )   settings/host.json
```

- `RegistrationOpen` false: a Google sign-in whose subject has no account is answered
  `409 Account.RegistrationClosed` ("New accounts are closed for now") instead of the ticket that asks for a name.
  Every existing account signs in as before. A dev host is unaffected. The lobby says the same where the name would
  be asked.
- `Notice`: an optional line every page shows under the header (a maintenance window, a new version), set from the
  Settings tab.

Read once a minute by the host, so a change reaches every process without a restart. This is a decision about who
can join, so it gets an ADR.

### What an operator does is recorded

Every action the panel takes (open, close, reinvite, delete sessions, export, change a setting, requeue or drop a
turn) writes one line to an operator log: who (the operator's name from the gate), what, on which id, when. Kept
beside the accounts, the last 1,000 lines, read in the Activity tab. Today these lines go to the console only.

## The routes

All behind the operator gate (ADR 0081, 0172), JSON, paged by `?page=` with twenty a page and `?sort=`/`?q=`:

| Route | Answers |
| --- | --- |
| `GET /api/admin/overview` | The Overview's numbers |
| `GET /api/admin/players?page&sort&q` | A page of players with their activity, game counts and rating |
| `GET /api/admin/players/<id>` | One player: the same, plus friends, devices, games in progress, recent results |
| `POST /api/admin/players/<id>/sign-out` | Signs the account out everywhere (raises its generation) |
| `POST /api/admin/players/<id>/rename {"username"}` | Renames the account, the name checked as a player's own is (ADR 0122) |
| `DELETE /api/admin/players/<id>` | Deletes the account as its player would: the name freed, its seats in unfinished games offered to others |
| `POST /api/admin/players/<id>/suspension`, `DELETE .../suspension` | Suspends or restores the account (ADR below) |
| `POST /api/tables/<id>/void {"category","explanation"}` | Voids the match, in progress or finished, with its reason; `400 Admin.VoidReason` without one |
| `GET /api/admin/jobs/tend?page` | Tending runs, newest first, and the 24-hour and 7-day totals |
| `GET /api/admin/jobs/tend/<run>` | One run, every table it touched and what it did, its error |
| `POST /api/admin/jobs/tend/run` | Runs a tending pass now, in the API's process; answers the run when it ends |
| `GET /api/admin/jobs/turns` | Each turn queue's length and the age of its oldest item |
| `GET /api/admin/jobs/turns/<poison\|dlq>?page` | The failed turns on that queue, newest first |
| `GET /api/admin/jobs/turns/<poison\|dlq>/<id>` | One failed turn: the original item, deliveries, the full error |
| `POST /api/admin/jobs/turns/requeue {"from","ids"\|"all"}` | Puts those turns back on `agent-turns` and deletes them where they were |
| `POST /api/admin/jobs/turns/drop {"from","ids"}` | Deletes those turns, requeuing nothing |
| `GET`/`PUT /api/admin/settings` | The host settings |
| `GET /api/admin/activity?page` | The operator log |

The existing `/api/tables` and `/api/sessions` stay as they are; the Tables tab only gains paging over the index's
`OpenAsync(skip, take)`.

## Where the code goes

| Layer | Adds |
| --- | --- |
| Application | `AccountActivity` and its port, `IAccountStore.ListAsync`, `ITendLog` and `TendRun`, `ITurnQueueStatus`, `HostSettings` and its port, `OperatorLog` and its port, `AdminOverview` (the numbers, computed from the ports) |
| Infrastructure | File and blob adapters for each port, beside the existing ones; the queue status over the storage queues |
| Cli | `AdminApi` routes above; `TableTend` writes its run; `AccountApi` writes activity and honours registration |
| table/ | `admin.html` rebuilt on `navigation.js` and `identity.css`; `admin.js` split into a tab router and one module per tab, each with pure, node-tested model functions as today |

## Stages

Each stage is one pull request, with its tests and docs, and leaves the panel working.

1. **A1, the shell.** The shared header, the title row, the tabs and the hash routing, the shared list part. Tables
   and Sessions move into their tabs unchanged; Tables gains paging. No server change. Browser tests for each tab
   on desktop and phone. *Built (2026-11-13)*: the tabs are links in a navigation row (`#tables`, `#sessions`), and
   the Tables tab pages the host's listing in the page, twenty at a time, as Sessions does; paging over the index
   on the host waits until a host holds more tables than one listing carries. A tab appears with its stage.
2. **A2, players.** ADR for activity. `AccountActivity` (last sign-in and last seen), the account listing, the
   Players tab and its side sheet with sign out everywhere, rename and delete. The *What we keep* panel updated.
   *Built (2026-11-13)*: ADR 0174. The listing is `IAccountStore.ListAsync` (every account, not a page of them: the
   sort needs them all), joined in `PlayerDirectory`; `GET /api/admin/players?q&sort&page` pages it twenty at a time,
   sorted by last seen, last sign-in, newest, name, games or rating. The sheet adds friends, devices and the last
   eight games. A rename keeps the account's sessions. The tab shows only on a host that signs players in.
3. **A2b, suspension.** ADR: a suspended account cannot sign in, its sessions end, its seats in unfinished games are
   kept for a time then offered to others, and its player is told why at the sign-in. The side sheet's Suspend and
   Restore.
4. **A3, jobs: the tending runs.** `ITendLog`; `table-tend` writes its runs; the Jobs tab with runs, totals and each
   run's sheet; Run a pass now.
   *Built (2026-11-13)*: `TendRun` and `ITendLog` (Application), kept in a `tend` directory or container beside the
   recordings rather than under them, since the session store reads every top-level prefix as a session. A run's id
   begins with its start time, so the newest are listed without a read and the week's totals read only the week's
   records. `table-tend` keeps a failed run too, then fails as before. `GET /api/admin/jobs/tend?page` and
   `GET /api/admin/jobs/tend/<id>` read them; `POST /api/admin/jobs/tend` runs the pass in the API's process, one at
   a time, kept as manual with the operator's name. The tab shows only on a host that records.
5. **A3b, jobs: the bot turns.** `PeekAsync` and `CountAsync` on the queues; the queues' lengths, the dead letters and
   the poison queue paged, each letter's sheet; Requeue (one, selected, all) and Drop.
   *Built (2026-11-13)*: `PeekAsync` and `CountAsync` on `IWorkChannel` (storage and in-process queues), and
   `TurnQueues` (Application) over the three queues. A storage queue shows only its first 32 items without taking
   them, so the tab lists the first 32 dead letters, first set aside first, and says how many more wait behind them;
   requeueing or dropping those brings the rest into view. The poison queue is shown by its count, since the
   dead-letter worker sets its items aside within half a minute. Requeue and drop walk the dead letters a batch at a
   time, hiding what they hold and letting back at once what they were not asked for; a letter is deleted only after
   its turn was sent. `GET /api/admin/jobs/turns`, `POST .../requeue {"ids"} | {"all":true}`, `POST .../drop {"ids"}`.
   Each requeue and drop is a console line naming the operator until the activity log (A6) keeps it. The Jobs tab
   shows on a host with either tending runs or turn queues.
6. **A3c, voiding a match.** ADR (amends 0163 and 0168). `VoidReason` on the record, the void sheet on tables and
   sessions, the result withdrawn from `IMatchResults` for a finished match, the players told with the explanation,
   the voided games shown as such in the finished games list.
   *Built (2026-11-14)*: ADR 0176. `MatchVoid` (category, explanation, who, when) on the table's record
   (`TableRecord.VoidReason`) and on its result (`MatchResult.Voided`). A kept result is not deleted but marked void,
   and every reader leaves it out: `MatchResult.Ranked` is false, so the ladder and the statistics skip it, and the
   bots' tallies, the recent opponents and the operator's game counts skip it by name. The finished games list keeps
   it, tagged Voided with the explanation. The route is `POST /api/tables/<id>/void`, beside the other table routes;
   a table this host holds is let go of first. A bot turn still queued for it finds a closed table and is done with.
   The sessions list says which sessions are void, and under what; the Void sheet opens from a session or a table
   card. A void is a console line naming the operator until the activity log (A6) keeps it.
7. **A4, settings.** ADR for registration. `HostSettings`, the Settings tab, `Account.RegistrationClosed` in the
   lobby, the notice line under every page's header.
8. **A5, overview.** The numbers, once A2 to A4 give them their sources.
9. **A6, activity.** The operator log and its tab.

Every stage keeps the panel behind the gate. None of it changes a game.

## Questions the owner settled (2026-11-13)

1. Activity keeps both *last sign-in* and *last seen* (the last request, written at most hourly): a Google sign-in
   lasts weeks, so the sign-in alone says little about who still plays.
2. Closed registration means closed: no new account, invitation or seat code included. Existing accounts sign in as
   before. Invitation codes can follow in a plan of their own.
3. The player actions are sign out everywhere, rename and delete in A2, and suspension in A2b with its own ADR.
4. The plan goes in as a pull request of its own, read before any stage is built.
5. A job can be inspected and run again: a tending run has its sheet and Run a pass now; a failed bot turn has its
   sheet, and the dead letters go back on the main queue with Requeue (added 2026-11-13).

6. An operator can void a corrupted match with a documented reason (added 2026-11-13). Proposed, for the owner to
   confirm when reading the plan: a finished match can be voided too (its result withdrawn from the ladder and the
   statistics), and the reason is a category and a required explanation, both shown to the players.

Still to decide, with the stage that needs it: how long the operator log is kept (proposed: the last 1,000 lines,
A6).
