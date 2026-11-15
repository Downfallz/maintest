# Commands

Every command the repository runs, one heading per command, with its flags and what it serves. `AGENTS.md`
keeps the gate's commands only; an area's own `AGENTS.md` keeps the ones a change there runs. When a command,
a flag or a route changes, change it here, not in `AGENTS.md` (ADR 0181). The table's routes are also listed,
one line each, in [`table/README.md`](../../table/README.md#routes).

All commands run from the repository root.

## The gate

What CI runs, and what `/verify` runs before a commit. The rules for when each part applies are in
`AGENTS.md` ("Definition of done").

### Build the battle view

```bash
npm --prefix battle ci --ignore-scripts && npm --prefix battle run build
```

The battle assets the host's route tests require (`TableFilesTests` fails without them). See `battle/README.md`.

### Restore, build, test, format

```bash
dotnet restore
dotnet build --no-restore                 # warnings are errors
dotnet test --no-build                    # Microsoft.Testing.Platform runner (see global.json)
dotnet test --no-build -- --coverage      # with code coverage
dotnet format --verify-no-changes         # what CI runs; use `dotnet format` to fix
```

### The static pages' tests

```bash
node --test studio/*.test.js table/*.test.js
```

The static pages' own tests (ADR 0024); needs no install.

### The Python side

```bash
uv sync --project learning && uv run --project learning ruff check learning && (cd learning && uv run pytest)
```

Plus `uv run --project learning ruff format --check learning` when `learning/` changes.

### The browser suite

When `battle/` or `table/battle-*` changes: rebuild the bundle (`npm --prefix battle run build`) and run the
browser suite's `battle-lab.spec.js`, `battle-live.spec.js`, `battle-combat.spec.js`, `battle-playback.spec.js`,
`battle-loop.spec.js`, `battle-history.spec.js`, `battle-guidance.spec.js`, `battle-upkeep.spec.js`,
`battle-mutations.spec.js`, `battle-evolution.spec.js`, `battle-tempo.spec.js`, `battle-spellbook.spec.js`,
`battle-pacing.spec.js`, `battle-growth.spec.js`, `battle-order.spec.js`, `battle-targets.spec.js`,
`battle-inspection.spec.js`, `battle-services.spec.js`, `battle-parity.spec.js`, `battle-reactions.spec.js` and
`battle-release.spec.js`, which live under `studio/browser/` (battle/README.md, studio/README.md).

CI runs these, then sends the build and the coverage reports (C# and Python) to SonarCloud with the scanner for
.NET (`.config/dotnet-tools.json`). The Sonar quality gate covers C#, Python, shell scripts, and workflows, and
must pass on every pull request.

## Content

### DataBuilder

```bash
dotnet run --project tools/DownfallArena.DataBuilder -- data data/dst
```

Validate and consolidate content into `data/dst/game.schema.json` with a content hash (ADR 0009). It refuses a
critical chance off the d20's twentieths and a creature with a chance of its own (ADR 0100).

### studio

```bash
dotnet run --project src/DownfallArena.Cli -- studio
dotnet run --project src/DownfallArena.Cli -- studio --export site/data
```

The content studio on http://127.0.0.1:5099 (studio/README.md). `--export site/data` writes what the published
studio reads, as files (ADR 0023).

### showcase build

```bash
node showcase/build.mjs
GAME_ORIGIN=https://your-game-host.example node showcase/build.mjs
```

Builds the public showcase into `showcase/dist`: eight localized pages, their assets and the `_headers` file
Cloudflare Pages serves (showcase/README.md, ADR 0185). `GAME_ORIGIN` sets the origin of the Play links. It must be
a bare HTTPS origin, with no path, query or credentials. The default is `https://play.downfallinstinct.com`.
`node --test studio/showcase.test.js` rebuilds it and checks it.

## Matches in the console

### play

```bash
dotnet run --project src/DownfallArena.Cli -- play --seed 1
dotnet run --project src/DownfallArena.Cli -- play --seed 1 --format 1v1
dotnet run --project src/DownfallArena.Cli -- play --seed 1 --declaration hidden-intent
dotnet run --project src/DownfallArena.Cli -- play --seed 1 --trace match.trace.json
```

Bot vs bot with a log (needs `data/dst`).

- `--format 1v1`: the same, 1 creature a side. `--format` takes `3v3|2v2|1v1` (or the team size alone) on
  play, human, simulate, evaluate and table, and moves nothing but the team size. Not on benchmark: its digest
  is committed per content hash, so every format would be verified against the same one.
- `--declaration hidden-intent`: each creature declares a hidden intent before combat, the rule before
  ADR 0112, instead of choosing its spell with its targets at its slot (the default); `at-activation` names the
  default (ADR 0110). On play, human, simulate, evaluate and table (where a `--rules` file's
  `"combatDeclaration"` and a table request's `"declaration"` say it too, and the admin panel's Spells
  chooser); refused by benchmark, studio and `--practice`.
- `--trace match.trace.json`: plus the match trace.

### human

```bash
dotnet run --project src/DownfallArena.Cli -- human
```

You against a random bot. Takes `--format` and `--declaration` as `play` does.

### simulate

```bash
dotnet run --project src/DownfallArena.Cli -- simulate --matches 200 --seed 1 --out simulation.csv
dotnet run --project src/DownfallArena.Cli -- simulate --matches 200 --seed 1 --record runs/random
```

`--record runs/random` adds a dataset and traces (`--traces N` caps the traces; 0 keeps none).

### evaluate

```bash
dotnet run --project src/DownfallArena.Cli -- evaluate --p1 greedy --p2 random --seeds benchmarks/benchmark-seeds.json
dotnet run --project src/DownfallArena.Cli -- evaluate --p1 explore:0.2:lookahead --p2 lookahead --seeds benchmarks/benchmark-seeds.json --slot-log runs/slots/slots.jsonl
```

Agents: `random`, `greedy`, `lookahead[:<rounds>x<rollouts>][:<weights.json>|:<agent>]`,
`minimax[:<rounds>x<rollouts>][:<weights.json>|:<agent>]`, `heuristic:<weights.json>`, `policy:<policy.json>`,
`explore:<rate>[:<agent>]`, `opening:<package>+<package>[:<agent>]`.

- `opening` buys those packages with its round-1 picks and plays the rest as greedy or the agent named
  (ADR 0109).
- `explore` deviates from greedy, or from the agent named: `heuristic:<weights.json>`, `policy:<policy.json>`,
  or a bare path as the weights shorthand.
- `lookahead` and `minimax` play the seats they guess as the agent named, e.g. `lookahead:policy:<policy.json>`,
  and keep the built-in weights as the evaluation (ADR 0055). With `<rounds>x<rollouts>` in front, e.g.
  `lookahead:4x4:<weights.json>`, they also play that many rounds after each combat move out on that many
  rollouts, the way they read a purchase, which is a bot for a person to play against at the table rather than
  one for the tuner's panel (docs/learning/agents.md).
- `--slot-log runs/slots/slots.jsonl`: plus every castable spell's value at each slot the lookahead decides,
  one JSON line a slot. `python3 scripts/slot-choices.py runs/slots/slots.jsonl` reads it spell by spell: how
  often each is the best cast, close to it, or behind (the regret a match result never shows).

### benchmark

```bash
dotnet run --project src/DownfallArena.Cli -- benchmark
```

Verify the benchmark digest (CI does); `--write` regenerates it.

## The table

### table

```bash
dotnet run --project src/DownfallArena.Cli -- table --rules <file> --p2 greedy
```

Two people at one screen, or one against a bot (ADR 0054). The table's routes, one line each, are in
[`table/README.md`](../../table/README.md#routes); this section keeps how each behaves.

#### Seats, network and recording

- `--handover N` starts as bots and hands over at round N.
- `--bind <address>` serves a phone on the same network instead of this machine only (`--bind 0.0.0.0` every
  interface, what its container binds, ADR 0080; refused on Windows).
- The session is recorded into `runs/playtest/<id>/` (`--record` names another root, or a blob container by its
  URL for the hosted table (ADR 0080); `--who <initials>` stamps who played, `--no-record` writes nothing at
  all), and `/session/<id>` shows it in the viewer once the match is over.
- `--format` and `--declaration` as on `play`.

#### The pilot page

The console prints a pilot token, and `/pilot?token=<it>` is the operator's own page: who is playing each seat,
what each is being asked and the one swap each is waiting to make, plus the form that hands a seat over from the
top of a round the match has not reached (`POST /api/pilot/seats/player1 {"agent":"greedy","round":7}` is the
same thing from a shell). It shows no board and no hand: the operator is usually one of the two players
(stage 6).

#### The admin panel: tables, agents and sessions

The host plays many tables at once, each found by its own tokens (ADR 0081).
`/admin?token=<operator token the console prints>` is the operator's admin panel:

- It lists them and opens more (`POST /api/tables {"player1":"person","player2":"greedy","who":"mk"}` from a
  shell).
- It offers the bots `learning/seatable.json` puts forward plus every weights file (`GET /api/agents` is folded
  into its listing).
- It lists every recorded session the store holds (`GET /api/sessions`), filtered by state and by who sat and
  paged twenty at a time, with a replay of each finished one (`/play?replay=<id>`, read off
  `GET /api/sessions/<id>/replay`).
- It deletes sessions (`DELETE /api/sessions {"ids":[...]}`, closing a table still being played).
- It diagnoses a table in progress from its card (`GET /api/tables/<id>/diagnosis`: its seats and who is asked,
  its journal's length and last lines by kind and seat, the content, rules and engine it is played on and its
  bot turns' troubles; `POST .../diagnosis/replay` rebuilds it aside and says whether it replays;
  `POST .../diagnosis/hidden` shows what each seat holds face down, noted in Activity; `POST .../diagnosis/turn`
  requeues its dead letters or owes its bots' turn again; docs/tabletop/scenarios-plan.md, S5b).
- It voids a match in progress or finished with a reason the players read
  (`POST /api/tables/<id>/void {"category","explanation"}`: stuck, engine, abuse, mistake or other, 10 to 500
  characters; its record closed and marked void, its seats answered 410 with the explanation, a kept result
  marked void and counted nowhere, its finished-games card tagged Voided; final, ADR 0176).
- It exports one or several as a zip of run directories `train-clone` reads (`GET /api/sessions/<id>/export`,
  `POST /api/sessions/export {"ids":[...]}`).

#### The admin panel: Players

When each account last signed in and was last seen (to the hour) is kept beside them under `activity/` and
forgotten with the account, and the admin panel's Players tab lists every account with it, its games and its
rating (`GET /api/admin/players?q&sort&page`, twenty a page; `GET /api/admin/players/<id>` its sheet), and
signs one out everywhere (`POST .../sign-out`), renames it (`POST .../rename {"username"}`) or deletes it
(`DELETE .../<id> {"username"}`, the name typed to confirm) (ADR 0174), and suspends it with a reason of 10 to
300 characters (`POST .../<id>/suspension {"reason"}`) or restores it (`DELETE .../<id>/suspension`):
suspended, every session ends, its devices are forgotten and signing in is answered 403 `Account.Suspended`
with the reason, and a day later the tending pass offers its seats in unfinished games to other players, writing
no account (ADR 0178).

#### The admin panel: Jobs

Its Jobs tab lists the tending runs twenty a page with the last day's and week's totals
(`GET /api/admin/jobs/tend?page`, `GET /api/admin/jobs/tend/<id>` a run's sheet) and runs a pass now in this
process (`POST /api/admin/jobs/tend`, kept as manual with the operator's name), and, on a host whose bots' turns
go through queues, shows each queue's length and oldest item and the dead letters, first 32 first
(`GET /api/admin/jobs/turns`), and puts a letter's turn back on the queue or drops it
(`POST /api/admin/jobs/turns/requeue {"ids"}` or `{"all":true}`, `POST .../drop {"ids"}`).

#### The admin panel: Scenarios, and practice from a scenario

Its Scenarios tab, and each recorded session's practice from a round…, cut a recorded session at the top of a
round into a match scenario kept beside the recordings (`GET /api/admin/scenarios/rounds?session=<id>` the
rounds it can be cut at, `GET`/`POST /api/admin/scenarios {"session","round","seat","title","goal"}`,
`DELETE /api/admin/scenarios/<id>`, `PUT /api/admin/scenarios/<id>/listing {"listed"}` lists it under Practice
on every signed-in player's home page or takes it off, noted in Activity; ADR 0179,
docs/tabletop/scenarios-plan.md).

Its link `/battle?scenario=<id>` a signed-in player opens as practice (`GET /api/scenarios/<id>`,
`GET`/`POST /api/scenarios/<id>/practice`: resumed, or started from the cut in place of the account's last
practice; in this process's memory, never recorded, indexed or in the ladder, one an account, 200 at most, let
go of after an idle hour, gone with a restart), the battle view saying it is practice in a band with Briefing,
Restart the round and Leave, and opening a practice started from its cut on a briefing card (its title, goal,
seat and round). `GET /api/scenarios` lists the scenarios listed under Practice, which a signed-in player's home
page shows.

#### The admin panel: Overview, Activity and Settings

- Its Overview, the tab it opens on, has the numbers at a glance (`GET /api/admin/overview`: players, games in
  progress and finished, open seats, invitations, games running out of time within a day, the last tending run,
  bot turns waiting and given up on, registration), each card linking to its tab.
- Its Activity tab lists what operators did from the panel, twenty a page, newest first
  (`GET /api/admin/activity?page`: each table opened, closed, reinvited or voided, session deleted or exported,
  player signed out, renamed, deleted, suspended or restored, tending pass, requeue or drop, settings save,
  scenario cut, listed, taken off or removed, a table's hidden state revealed or its bot turn owed again, with
  who and when), the last 1,000 kept beside the recordings under `operator/` (or the operator container).
- Its Settings tab opens or closes registration and posts a notice
  (`GET`/`PUT /api/admin/settings {"registrationOpen","notice"}`, kept beside the accounts as
  `settings/host.json` and read by each process at most once a minute): closed, a Google sign-in with no account
  is answered 409 `Account.RegistrationClosed` instead of being asked for a name, invitation or code included,
  while every existing account signs in and a dev host is unaffected, and the notice, one line of at most 200
  characters, rides on `/api/me` and shows under the header of every page but the game itself (ADR 0177).

#### The lobby and the pages

- `/` and `/lobby` serve the player lobby (ADR 0115): saved browser seats, public summaries and explicitly open
  places (`GET /api/lobby` counts the games in progress and the open seats; `GET /api/lobby/page/<open|all>/<page>`
  serves them eight at a time, newest first, a signed-in player's own open tables left out of open), table
  creation, and a Leave action that rotates the seat token/code and offers the same team to another player.
- `GET /api/lobby/events` streams that the public lists, or the signed-in player's own, changed, and every lobby
  page reads on it and on its seat streams instead of a clock (ADR 0164).
- With `DOWNFALL_RELAY` naming an Azure Web PubSub endpoint (on table, table-worker and table-tend alike), every
  process says what moved through it, a page connects to it at the address `GET /api/seat/<seat>/negotiate` or
  `GET /api/lobby/negotiate` signs, falling back to the stream without one, and a process hears the others there
  instead of looking at its tables every second (ADR 0165).
- `/play` serves the game.
- The table's files are read once, sent compressed with an ETag and answered 304 when unchanged, pages and
  scripts revalidated on every load and pictures kept a day, and the service worker every page registers keeps
  one page, `/offline.html`, shown when a page cannot be loaded at all (ADR 0166).
- `/lobby?token=...` still redirects to admin.
- The table page shows an Admin link to whoever `/api/me` says is the operator.

#### The journal, replay and void matches

- A table is stepped over its journal, `decisions.jsonl`: a decision is written and applied before the tap
  answers, a bot's turn is taken behind it, and a table is picked up where its journal ends at the first request
  that names it, by a key (`<table>.<secret>`) or a join code the index of the tables finds (ADR 0161), under
  the same tokens and codes, and a host starting over a store replays nothing: it puts the open tables back into
  the index (ADR 0159, after ADR 0091).
- A table is replayed on the content and rules it began on, its schema kept once per hash beside the recordings
  (`runs/content`, or the content container), so a deploy that moves content or rules ends no open match.
- One whose journal no longer replays on the engine that picks it up ends as a void match: closed, marked void,
  no ladder result, each seat's account told, and its seats' keys answered 410 with the reason, which the battle
  view and the seat discovery show (ADR 0163).
- A table with a person in it waits for every person to reach their seat before the first question, then both
  pick their first package at once (ADR 0092), and both are asked their speeds and intents at once too
  (ADR 0111).
- A seat's link or code carries an invitation, not the seat: the page asks before accepting it, it is accepted
  once, and the admin panel's New invitation cuts off a lost device (ADR 0116).

#### "Your turn" on a phone

`--push-keys <file>` (or `DOWNFALL_PUSH_KEYS`) lets a seat's phone be told "your turn" when nobody is looking at
its page, a Web Push the page's service worker shows (ADR 0118; `push-keys` prints a pair; https or localhost
only; a device holding both seats is told nothing, ADR 0120).

With push keys too, a signed-in player's lobby offers "Notify me on this device" (`PUT`/`DELETE /api/account/push`,
`POST /api/account/push/test`): the browser becomes one of the account's devices, five at most, kept beside the
accounts under `devices/` and forgotten with "sign out everywhere" and with the account, which is where a
friend's request or an invitation reaches a player who holds no seat (docs/tabletop/friends-plan.md, stage 1).

#### Accounts

- `--accounts google|dev` signs players in on the home page (ADR 0122, docs/tabletop/accounts-plan.md): google
  checks Google's ID token against `--google-client-id` or `DOWNFALL_GOOGLE_CLIENT_ID`, dev signs in by a typed
  name and is refused off the loopback address.
- `--admins <acct_id>[,...]` (or `DOWNFALL_ADMINS`) names the accounts that are the operator once signed in,
  beside the token or the platform's sign-in, so the menu's Admin link shows for them (ADR 0172; by id, never by
  name).
- Accounts are kept beside the recordings (`runs/accounts`, or the accounts container) and need a recording
  host.
- A signed-in player's game settings (reading pace, speed, phase tips, less motion, opponent reactions; Step,
  1×, tips and reactions on by default) are `GET`/`PUT /api/account/preferences`, kept beside the accounts under
  `preferences/` and forgotten with the account; every new game starts from them and its own Match settings
  override them for that game only (ADR 0140).

#### Friends, and a table for a friend

- A signed-in player keeps friends (ADR 0129, stage 2): `GET /api/friends` lists them with who asked and who was
  asked, `POST /api/friends/requests {"username":"..."}` asks by exact name (asking back accepts),
  `POST /api/friends/requests/<id>/accept`, `DELETE /api/friends/requests/<id>` (decline or withdraw) and
  `DELETE /api/friends/<id>` (remove) are silent; kept beside the accounts under `friends/`, a hundred friends
  and a hundred pending requests at most, gone with the account; the lobby's Friends panel is the same thing.
- A table can be opened for a friend (ADR 0129, stage 3):
  `POST /api/lobby/tables {"opponent":"friend","friend":"<accountId>"}` keeps the second seat for that account
  (no open place, no code, no link; one such invitation at a time per account, 24 hours at most), which that
  account alone accepts (`POST /api/lobby/tables/<id>/accept`) or declines (`/decline`); the inviter cancels
  (`DELETE /api/lobby/tables/<id>/invitation`), keeps the seat for another friend
  (`POST .../invitation {"friend"}`), opens it to everyone (`POST .../open`) or closes the unbegun table
  (`DELETE /api/lobby/tables/<id>`, for whoever holds every taken seat).
- `GET /api/lobby/mine` lists the tables waiting for the player under `invitations` and says `invited` on their
  own.
- The friend's devices are told of the invitation, the inviter's of its answer, each seat's account of how the
  match ended and of the opponent leaving a begun match (stage 4).

#### Finished games, opponents, and the cap on games

`GET /api/lobby/finished/<bots|players>/<page>` pages the player's finished games, six at a time, from the kept
results, each opening its whole-match recap in the battle view (`/battle?replay=<table>&seat=<seat>`, read off
`GET /api/lobby/tables/<table>/replay` by an account that sat at it, ADR 0156), `GET /api/lobby/opponents`
lists the eight players met most recently with the record, the last game's format and rating and where the
friendship stands (the lobby's Recent opponents: Challenge for a friend, Add friend otherwise), and an account
has at most five games in progress against bots and five against players (`Lobby.TooManyGames`, ADR 0147).

The outcome card offers Rematch (`/lobby?rematch=<accountId>|bot|hotseat&format=...` opens the create dialog on
the same opponent).

#### The bot's level

A game against the bot is opened at a level, `"level":"easy|medium|hard"` on `POST /api/lobby/tables` (Hard
when none; another refused with `Lobby.BotLevel`), each the bot `learning/seatable.json`'s `levels` names (Easy
greedy exploring 30%, Medium greedy, Hard the strongest), and a player is shown the level and never the bot,
everywhere, while the admin panel shows both (ADR 0180).

#### Reactions

Two people at a begun match send each other reactions, one of a fixed set of eight short messages and never
free text (`POST /api/seat/<seat>/reaction {"reaction":"well-played"}`, one every four seconds a seat, 429
otherwise, refused against a bot and with one account in both seats), kept with the table's record
(`reactions.json`, the last eight), served in each seat's view as `reactions`, shown once to the other player
unless their settings hide them, and never notified, counted in the bell or recorded as an event of the match
(ADR 0173).

#### The ladder, formats and the time to answer

- `/leaderboard` (`GET /api/leaderboard`) ranks the accounts by Glicko-2 over games between two of them
  (ADR 0124), except those opened with `"ranked":false` on `POST /api/lobby/tables` for an open seat or a friend
  (ADR 0155).
- A ranked game is 3v3, another format refused with `Lobby.RankedFormat` and none named played 3v3, while an
  unranked game, a game against a bot and one on a shared device take any format (ADR 0171).
- A game between two players gives each the time to answer `"turnLimit"` says on `POST /api/lobby/tables` (24,
  72 or 168 hours, 72 by default; none against a bot), and a player who lets it run out loses, both drawing when
  both ran out, and is told once on their devices when a quarter of it is left; every table it lists and each
  seat's view say when it runs out (`answerBy`), which the lobby's cards and the battle view count down
  (ADR 0169).
- `/stats` (`GET /api/stats`) is a signed-in player's own statistics, computed from the results kept by account
  (ADR 0168) and counting their ranked 3v3 games alone (ADR 0171): the record, streaks, and the mutations and
  spells they played, which each result keeps from the final board and the match's events.

#### Bots' turns, tending, and the hosted shape

- `--turns queue` (or `DOWNFALL_TURNS=queue`) hands the bots' turns to a worker through a queue instead of
  taking them in the process (ADR 0159, phase 5): the storage account's queues beside a container of
  recordings, this process's own beside a directory; a turn that fails at five deliveries goes on
  `agent-turns-poison` with its error, and a second worker keeps it for good on `agent-turns-dlq`; both workers
  run in the host.
- `--turns worker` (or `DOWNFALL_TURNS=worker`) puts them on the queue and runs no worker, for table-worker to
  take in a process of its own (ADR 0162; a container's queues only).
- `--tended` says the hourly table-tend job tends the tables, so no request closes a table that never began or
  lets an invitation go (a request still lets go of the tables the process holds that nobody is at, and an
  invitation past its day is no longer listed nor in the way), which is the hosted API's shape with the worker
  app and the job (infra/README.md, ADR 0162).
- `--lobby` starts with no table, and `--platform-auth` trusts the sign-in a platform in front of the host
  stamps on a request (`X-MS-CLIENT-PRINCIPAL-NAME`) instead of printing a token, which is the container's shape
  (ADR 0080).

### table --practice

```bash
dotnet run --project src/DownfallArena.Cli -- table --practice
```

Nine reproducible scenarios, selected/restarted in the browser; unrecorded, separate from an ordinary table
(ADR 0079); five of them play spells chosen at the slot (ADR 0110, docs/tabletop/free-activation-playtest.md).

### table --scenario

```bash
dotnet run --project src/DownfallArena.Cli -- table --scenario bug.scenario.json
```

The practice page on that scenario: opened at the top of its round in the seat it names, the other seat the
scripted opponent (the source's recorded answers while they fit, then greedy), Restart from the cut, nothing
recorded and the source untouched.

### table-tend

```bash
dotnet run --project src/DownfallArena.Cli -- table-tend --record runs/playtest
```

One pass of what is due on the tables, then exit (ADR 0162), kept as a run for the admin panel's Jobs tab
(`tend/` beside the recordings, or the tend container; the last 500, a failed one too):

- every table that never began closed after twenty hours, through its record;
- every invitation nobody answered in a day let go of (`--accounts google|dev` reads whether the two are still
  friends);
- every begun game between two players whose journal was not written for longer than its time to answer ended
  for whoever it still asks, its result kept and its seats told with `--push-keys`, and the player asked at a
  game whose time reaches its last quarter in the pass told so once (ADR 0169);
- every begun game with a seat its player left and no move for a day ended with that seat conceding (ADR 0170).

What the hourly job runs. The tables it picks up take no bot turn.

### table-worker

```bash
dotnet run --project src/DownfallArena.Cli -- table-worker --record <container-url>
```

The turn worker and the dead-letter worker in a process of their own, until stopped (ADR 0162): the bots' turns
a host told `--turns worker` puts on the queue, taken at tables picked up from their records. A match its turn
ends is written once, by whichever process claims it (`closing.json`); with `--accounts google|dev` the result
is kept for the ladder and the finished games, and with `--push-keys` (or `DOWNFALL_PUSH_KEYS`) too each seat's
account is told how it ended, so it takes the same `--accounts` and keys as the host it works for. It tells no
seat "your turn", the host serving the seat does. What the worker app runs.

### push-keys

```bash
dotnet run --project src/DownfallArena.Cli -- push-keys
```

Prints a pair of Web Push keys for `--push-keys` (ADR 0118).

### scenario-cut

```bash
dotnet run --project src/DownfallArena.Cli -- scenario-cut --session runs/playtest/<id> --round 7 --with-content --out bug.scenario.json
```

A recorded session cut at the top of a round into a match scenario (ADR 0179, docs/tabletop/scenarios-plan.md):
without `--round` it lists the rounds it can be cut at; `--seat player1|player2` is the seat a person plays,
`--title` and `--goal` what it is called and asks; it replays on the content the session was played on (this
host's, or the one kept beside the runs under its hash) and `--with-content` carries that content in the file.

### scenario-check

```bash
dotnet run --project src/DownfallArena.Cli -- scenario-check --scenario bug.scenario.json
```

Whether a scenario still rebuilds on this engine and stands on the boards recorded at its cut (exit 0 or 1); a
file kept under `tests/DownfallArena.Infrastructure.Tests/Scenarios/` is checked by the tests, so a repro becomes
a regression test.

### The hosted table's image

```bash
docker build -t downfall-table . && docker run --rm -p 8080:8080 downfall-table
```

The hosted table's image, locally: an admin panel on 8080, the operator's token in the log (infra/README.md).

## Learning

### search-weights

```bash
uv run --project learning search-weights -o runs/search
```

Tune the heuristic weights with the built CLI (docs/learning/training.md). `--kind lookahead|minimax` tunes them
for that reading instead; `--opponent greedy,heuristic:<w.json>,random` scores each candidate as the mean over
the list, ranking any candidate that falls below the start against one of them last, so it cannot learn one
opponent; `<spec>@<n>` seats one on the first n seeds only, how the lookahead fits in a panel.

### check-knobs

```bash
uv run --project learning check-knobs
```

The balance knobs against the content they describe (data/balance/README.md).

### tune-content

```bash
uv run --project learning tune-content -o runs/tune-1
```

Search those knobs for a better catalogue (ADR 0021). `--apply` writes it; a new leader has to win on
`benchmarks/confirmation-seeds.json` too (ADR 0074), `--no-confirm` skips that; `--sweep-shard I/N` plays one
slice of the opening sweep into `swept-I.json` and `--swept <files>` hands a search all of them, which is how
`tune.yml` plays the sweep in sixty-four jobs side by side.

### score-content

```bash
uv run --project learning score-content -o runs/score --seeds unseen.json
```

Play the content as it stands on a seed file, no search: how a proposal is checked on seeds it was not searched
on. `--declaration hidden-intent` plays every evaluation with hidden intents, `at-activation` with spells chosen
at the slot, the engine's default since ADR 0112.

### sweep-weight.py

```bash
uv run --project learning python scripts/sweep-weight.py energy 0.2 0.3 0.4
```

One scoring weight alone, on fixed content (ADR 0037); one sweep at a time.

### playtest-timing

```bash
uv run --project learning playtest-timing runs/playtest/<id>/ ...
```

Recorded sessions read for the time per decision, by combat declaration; `--two-devices` says they were played
by two people on two devices and times their rounds too, which nothing a session records can tell from hotseat
(docs/tabletop/free-activation-playtest.md).

### declaration-reading.py

```bash
uv run --project learning python scripts/declaration-reading.py --agent greedy
```

The same agent against itself on the same seeds under both combat declarations, read from traces: rounds, Quick
share, the last actor, fizzles, spell shares; no win rate across the two (ADR 0110).

### train-clone, train-value, export-csv, compare-stamps

```bash
uv run --project learning train-clone runs/greedy -o models/clone/v1
```

Or `train-value`; `export-csv`; `compare-stamps`.

### evaluate-policy

```bash
uv run --project learning evaluate-policy models/clone/v1 --opponent greedy
```

Play a policy with the engine, win rate into its log.

### spread

```bash
uv run --project learning spread runs/<id>
```

Every win rate of a turn ranged across its dataset seeds (ADR 0049).

### mean-policy

```bash
uv run --project learning mean-policy runs/<id>/seeds/*/value -o runs/<id>/mean/value
```

The seeds' value fits as one policy, scoring every candidate as their mean; what a turn plays as its last step.

### jackknife

```bash
uv run --project learning jackknife runs/<id>
```

The standard error of that mean's score, from the means the turn played with one seed left out
(`runs/<id>/mean/without-<seed>/`).

### paired

```bash
uv run --project learning paired a/evaluation.json b/evaluation.json
```

The difference between two evaluations read seed by seed, which is the quantity their two marginal intervals do
not describe: every agent plays the same fixed seeds, so their scores move together and neither interval is a
test of the difference either way (journal, 2026-09-18). Refuses different seeds or different content.

### iterate.sh

```bash
scripts/iterate.sh --against <previous-run-id>
scripts/iterate.sh --seeds "1 5001 10001"
scripts/iterate.sh --explore 0.2
scripts/iterate.sh --teacher heuristic:learning/weights/search-4.json
scripts/iterate.sh --clone-control
scripts/iterate.sh --explore 0.2 --clone-on-explore
```

One full turn of the loop into `runs/<id>/`; `--help` lists every tuning flag.

- `--seeds "1 5001 10001"`: the default at 5000 matches: three dataset seeds spaced by the match count, because
  closer seeds record the same matches shifted, and the turn reports the spread, because one seed is a sample
  (ADR 0049).
- `--explore 0.2`: plus an exploring dataset for the value policy (ADR 0014).
- `--teacher heuristic:learning/weights/search-4.json`: record a stronger player than greedy; a clone is capped
  by what it imitates (journal, 2026-09-15).
- `--clone-control`: plus a clone blind to the candidate terms, played as `clone-blind`: what the terms bought,
  on one dataset and one learner (ADR 0051).
- `--explore 0.2 --clone-on-explore`: fit the clone on the exploring dataset, for a clone that will be a
  searching agent's inner agent rather than a player: search asks it to guess on hypothetical boards pure
  self-play never visits (journal, 2026-09-18).

### The searches on GitHub Actions

- The same two searches run on GitHub Actions, each able to push a proposal branch: "Tune the catalogue"
  changes the content, "Search the agent weights" adds what it found next to `greedy.json`
  (docs/learning/training.md).
- A pull request touching `learning/experiments/search.json` runs that search with what the file says, the way
  one touching `next.json` runs the loop; a committed experiment reports and never applies
  (learning/experiments/README.md).
- "Learning loop" runs `scripts/iterate.sh` there on `learning/experiments/next.json`, and with `commit=true`
  proposes a policy that clears its bar under `models/` (models/README.md).

## Other scripts

### workflow-metrics.py

```bash
python3 scripts/workflow-metrics.py
```

The numbers of docs/engineering/ai-workflow-plan.md measured again: git readings first, `--github` for the rest.
