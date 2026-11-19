# Commands

Every command the repository runs, one heading per command, with its flags and what it serves. `AGENTS.md`
keeps the gate's commands only; an area's own `AGENTS.md` keeps the ones a change there runs. When a command,
a flag or a route changes, change it here, not in `AGENTS.md` (ADR 0181). The table's routes are also listed,
one line each, in [`table/README.md`](../../table/README.md#routes).

All commands run from the repository root.

## The gate

What CI runs, and what `/verify` runs before a commit. The rules for when each part applies are in
`AGENTS.md` ("Definition of done").

### verify.sh

```bash
scripts/verify.sh          # the engine and the pages always; the rest as the diff against origin/main calls for
scripts/verify.sh --all    # every step, whatever the diff
```

The local gate, as `/verify` runs it. Of the steps above, the battle bundle, the Python side and DataBuilder run
only when the diff calls for them, or when what they build is missing; everything else always runs. It starts Azurite when it is installed (`scripts/gate/azurite.sh`), always
restores, builds, format-checks and tests the engine and runs the pages' tests, and adds the battle bundle, the
learning project and the content builder when `node studio/browser/plan.js --gate` calls for them. It stops at
the first failure and prints a summary; its last lines say whether the storage tests were skipped and what the
browser suite would play (`/browser-check`). Once every step passed, it records the tree it saw.

### gate/state.sh

```bash
scripts/gate/state.sh changed    # the paths changed since the last green gate on this branch, markdown included
scripts/gate/state.sh tree       # the working tree's fingerprint
scripts/gate/state.sh record     # what verify.sh runs once it passed, with the tree it started on
scripts/gate/state.sh running    # exits 0 while verify.sh runs
```

What the last green gate saw, kept under the git directory (`claude-gate/`), never committed. With no gate
recorded on this branch, `changed` starts from where the branch left `main`. The `Stop` hook
(`.claude/hooks/stop.sh`) reads it and says once, per tree, that files changed after the last gate; it waits
while a gate runs. `studio/gate-hook.test.js` tests both on a throwaway repository.

### review/reviewers.mjs

```bash
git diff origin/main... | node scripts/review/reviewers.mjs               # the reviewers a diff calls for, one a line
git diff --name-only origin/main... | node scripts/review/reviewers.mjs   # the same from the paths alone
```

What `/reviewers` reads to choose the agents under `.claude/agents` that review a change: `code-reviewer` for
anything but markdown, then `domain-reviewer`, `host-reviewer`, `pages-reviewer`, `design-reviewer`, `copy-editor`,
`infra-reviewer`, `security-reviewer`, `stats-reviewer` and `rules-keeper` by the paths they own (the keeper:
the domain, `game-rules.md`, the words, an ADR, the content, the hosted rule set, `rules/`, `table/terms.js`,
`table/battle-guidance.js`, `battle/src/tempo.ts`, `showcase/guide.mjs` and `scenarios/tutorials/`). Given the whole diff, it also
calls `design-reviewer` for a page script (`table/`, `studio/`, `viewer/`, not a test) whose added lines hold a
raw colour, an inline style or a `className`. `legacy/` calls for nobody.
`studio/reviewers.test.js` tests the choice, that every reviewer it names is an agent that lists no tool that
edits, and that the two that keep a memory name its folder in their prompt: a memory turns Write and Edit on, and
only the prompt holds them to `.claude/agent-memory/<agent>/`.

### digest/digest.mjs

```bash
git fetch origin main:refs/remotes/origin/main && node scripts/digest/digest.mjs [facts.json]   # the morning digest, one phone screen in French
```

What `/digest` runs. It reads what merged on `origin/main` in the 24 hours before the run. From GitHub's REST API,
through `curl` (the session's proxy signs the requests), it reads the CI and nightly runs on `main`, the newest
deployment to the `azure` environment (from CI or by hand), the open `red-main` and `red-nightly` issues, the open
pull requests with their checks, and the open `follow-up`, `flaky` and `ops` issues. A field it cannot read is
named on its last line, never guessed. The optional facts, on stdin with `--stdin`, add what only the session
tools know (the sessions waiting on the owner) and override any field; their shape is in the script's header.
`studio/digest.test.js` tests it. The scheduled task "Bilan du matin" starts a fresh session every day at 6:40,
Montreal time, whose prompt (in `.claude/skills/digest/SKILL.md`) clones `main` and follows the skill; it pushes
the answer to the owner's phone.

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
node --test studio/*.test.js table/*.test.js showcase/*.test.js
```

The static pages' own tests (ADR 0024); needs no install. `studio/battle-craft.test.js` also holds the craft book
to the Phaser types that building the battle view (above) installs: without them those checks are skipped and
say so, except in CI's full build, where they fail.

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
`battle-inspection.spec.js`, `battle-services.spec.js`, `battle-parity.spec.js`, `battle-reactions.spec.js`,
`battle-tutorial.spec.js` and `battle-release.spec.js`, which live under `studio/browser/` (battle/README.md, studio/README.md).

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

### rules/build.mjs

```bash
node scripts/rules/build.mjs            # build rules/ into table/rules.json and rules.fr.json, or list what it refuses and exit 1
node scripts/rules/build.mjs --check    # exit 1 when either file is not the build of rules/
```

The official rulebook (ADR 0188, `rules/README.md`). It draws the book's values from the hosted rule set, the
reference creature and the spells, refuses a duplicate or vanished id, an unresolved value, a retired word, a
missing source, a dead `/rules#<id>` link and an unknown card, and writes the book as data. It builds the French
edition, `rules/fr/`, into `table/rules.fr.json`, and refuses a French book whose files, ids, order, sources, cards
or changes differ from the English, or that uses an English word the French column replaced (ADR 0193).
`table/rules.test.js` fails when a committed file is not its build; `UPDATE_INDEXES=1` with that test rewrites it too. The table serves
the book at `/rules` (see [The lobby and the pages](#the-lobby-and-the-pages)).

### rules/reach.mjs

```bash
git diff origin/main... | node scripts/rules/reach.mjs   # the sections of the book a change reaches, and why
```

What `/rules` and the `rules-keeper` start from. A section is reached by an ADR or a `game-rules.md` heading its
`sources:` line cites, by a value it draws (a spell, a mutation, a field of the hosted rule set, a stat of the
reference creature), by a spell or a mutation it names, and by a change to its own lines. The French edition's
sections are read beside the English and marked `[fr]`. A path that calls for the
keeper and reaches no section (the domain, the words, the pages' rule text) is listed to be read by hand. A line
removed just above a heading counts for the section it ended. Input that is not a unified diff (nothing, or
`--name-only`) is refused, never read as nothing reached. `table/rules-reach.test.js` tests it, and runs it on an
excerpt of a past balance pass.

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
node showcase/build.mjs /tmp/showcase   # into another folder, which it replaces
GAME_ORIGIN=https://your-game-host.example node showcase/build.mjs
```

Builds the public showcase into `showcase/dist`, or into the folder you pass as its one argument. The build
replaces that folder, so it refuses one that holds anything the build does not write. The output is ten localized
pages, their assets and the `_headers` file Cloudflare Pages serves (showcase/README.md, ADR 0185). The two
rulebook pages are drawn from `table/rules.json` and `rules.fr.json`. `GAME_ORIGIN`
sets the origin of the Play links. It must be a bare HTTPS origin, with no path, query or credentials. The default,
and the game website's origin the table links to, come from `table/site-origins.js`. `node --test showcase/*.test.js`
builds the showcase into a temporary folder and checks the result, leaving `showcase/dist` as it was. CI runs the
same build on `main`, behind the
gate, and deploys it (`.github/workflows/showcase.yml`, infra/README.md).

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

Every route reads a body of at most 64 KB and answers a longer one 413 `Host.BodyTooLarge` (the studio's host
allows 4 MB). A route that throws answers 500 `Host.Unexpected`; the exception goes to the console, one line,
never to the answer. A failure a route expects answers a code or a fixed sentence, never the exception's
text (#786). A table the store cannot open or keep is 503 `Lobby.Unavailable`, and one whose content does not
load is 503 `Lobby.Unplayable`. A relay that is out is 503 `Relay.Unavailable` on `GET /api/lobby/negotiate`,
and a plain-text 503 on `GET /api/seat/<seat>/negotiate`. A push test the service refuses answers with the
service's status code. A session page whose files cannot be read is 404.

#### Seats, network and recording

- `--handover N` starts as bots and hands over at round N.
- `--bind <address>` serves a phone on the same network instead of this machine only (`--bind 0.0.0.0` every
  interface, what its container binds, ADR 0080; refused on Windows).
- The session is recorded into `runs/playtest/<id>/` (`--record` names another root, or a blob container by its
  URL for the hosted table (ADR 0080); `--who <initials>` stamps who played, `--no-record` writes nothing at
  all), and `/session/<id>` shows it in the viewer once the match is over. It is 409 when the run's record exists and cannot be parsed, and served when the run has no record (it predates ADR 0091).
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
  `GET /api/sessions/<id>/replay`). `underWay` says, from its record, that a table is still played: its row offers
  no cut as a scenario until it is over (#736).
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
  `POST /api/sessions/export {"ids":[...]}`). The zip, and its 404 and 409, are sent with
  `Cache-Control: no-store, private`, so no cache serves it to someone the panel's gate would refuse.

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
docs/tabletop/scenarios-plan.md). A session still being played cannot be cut, and its rounds are not listed: 409
`Scenario.UnderWay`. Its journal holds the other seat's face-down choices of the round (#736). A scenario cut before
that rule from a game still played is not listed under Practice while that game is played (409 `Scenario.UnderWay`,
or 409 `Scenario.SourceUnread` when the game's record exists and cannot be parsed, or 503 `Scenario.Unavailable`
when the store cannot say); taking one off is always allowed (#833).

The tab also lists every file of the repository's `scenarios/` directory, read when the host starts (`"source":
"repository"` and its `"file"` in the list, under the id `sc-repo-<file name>`): played from the tab, on the content
the file carries, neither removed nor listed there (409 `Scenario.FromRepository`), and opened by the host's
admin accounts alone when it names some (`--admins`). A tutorial among them (`scenarios/tutorials/`, ADR 0187)
says its place in the series (`"tutorial": 1`), and every signed-in account opens it. A bug scenario among them
(`scenarios/bugs/<issue>-<slug>.json`, under the id `sc-repo-bug-<file name>`, feedback plan, R6) says the issue it
reproduces (`"bug": 812`), and the tab marks it *Bug #812*; every other row says `"bug": null`.

Its link `/battle?scenario=<id>` a signed-in player opens as practice (`GET /api/scenarios/<id>`,
`GET`/`POST /api/scenarios/<id>/practice`: resumed, or started from the cut in place of the account's last
practice; `DELETE /api/scenarios/<id>/practice` lets go of it, 204 whether there was one or not; in this process's memory, never recorded, indexed or in the ladder, one an account, 200 at most, let
go of after an idle hour, gone with a restart), the battle view saying it is practice in a band with Briefing,
Restart the round and Leave, and opening a practice started from its cut on a briefing card (its title, goal,
seat and round). `GET /api/scenarios` lists the scenarios listed under Practice, which a signed-in player's home
page shows. A scenario cut before #736 from a game still played is neither opened nor resumed until that game is
over (`GET`/`POST .../practice` 409 `Scenario.UnderWay`, or 409 `Scenario.SourceUnread` when its record cannot be
parsed, or 503 `Scenario.Unavailable` when the store cannot say), and `GET /api/scenarios` leaves it off, as it
leaves off one whose game the store cannot read just then, said in one console line for the list (#833).

`GET /api/tutorials` lists the tutorials to any signed-in account (401 signed out), in the order of the series:
each one's `id`, `order`, `of`, `title`, `goal`, `round`, `seat`, `format` and `link`. A tutorial is opened as a
practice by the routes above, and is never listed under Practice. Once a tutorial is done the host answers nothing
more for the person and the page draws no question: its end card offers Play again and Leave. A done tutorial is
not resumed, and leaving one, done or not, lets go of its practice (`DELETE`), so it opens from its cut the next
time; a done one is kept done for the account first, whether or not the page's own `done` got there before.

Each tutorial also says its `state` for the account: `new`, `done` or `skipped`. `opens` is the link of the
tutorial that opens by itself: the first of the series while it is new to the account, null otherwise, and null
on a host that keeps no game settings. `POST /api/tutorials/<id>/skip` records that the account skipped a tutorial;
the battle view says it for **Skip the tutorial** and for **Leave** before the end. `POST /api/tutorials/<id>/done`
records that it finished one, and answers 409 `Tutorial.NotDone` unless the account's practice of it at this host
says `done`. A tutorial is done once its match ends, conceded included; a practice whose table was let go of or
could not go on is not done, is not resumed, and opens from its cut the next time. Both answer `{ id, state }`, 404 `Tutorial.NotFound` for an id that is no tutorial, and 404
`Account.NoPreferences` on a host that keeps no game settings. Skipping a tutorial the account has finished leaves it done.

While it plays, the host answers the person's assisted questions from the recorded answers, as the person's own
decisions. At the first recorded answer that does not fit, it stops and gives every question back to the person.

The seat's view says the lesson under `practice.tutorial`:

- `order`, `of`, `title`, and `teaches`, the question kinds the person answers.
- `assisted`: the question kinds the host still answers for the person, so a page draws no question of that
  kind; empty once the questions are handed back.
- `steps`: each `at` a kind, a round and a creature, with `says`, `concepts` and `pointsAt`.
- `shown`: how many steps have had their question asked.
- `done`: the last step's question was asked and its round ended, or the match ended.

#### The admin panel: Overview, Activity and Settings

- Its Overview, the tab it opens on, has the numbers at a glance (`GET /api/admin/overview`: players, games in
  progress and finished, open seats, invitations, games running out of time within a day, the last tending run,
  bot turns waiting and given up on, feedback waiting, registration), each card linking to its tab.
- Its Activity tab lists what operators did from the panel, twenty a page, newest first
  (`GET /api/admin/activity?page`: each table opened, closed, reinvited or voided, session deleted or exported,
  player signed out, renamed, deleted, suspended or restored, tending pass, requeue or drop, settings save,
  scenario cut, listed, taken off or removed, a table's hidden state revealed or its bot turn owed again, feedback
  marked, moved to the other kind or dismissed as spam, a sender stopped or let through, with
  who and when), the last 1,000 kept beside the recordings under `operator/` (or the operator container).
- Its Settings tab opens or closes registration and posts a notice
  (`GET`/`PUT /api/admin/settings {"registrationOpen","notice"}`, kept beside the accounts as
  `settings/host.json` and read by each process at most once a minute): closed, a Google sign-in with no account
  is answered 409 `Account.RegistrationClosed` instead of being asked for a name, invitation or code included,
  while every existing account signs in and a dev host is unaffected, and the notice, one line of at most 200
  characters, rides on `/api/me` and shows under the header of every page but the game itself (ADR 0177).

#### Feedback

- `POST /api/feedback` keeps a bug report or an idea (ADR 0189, docs/tabletop/feedback-plan.md). It comes from
  the seat whose key the request carries, or from a signed-in account. A request that is signed out and has no
  seat is answered 401 `Feedback.SignInRequired`; a seat that belongs to another account, 403
  `Feedback.NotYourSeat`.
- A bug report is `{"kind":"bug","category","happened","expected?","attach","page","errors"}`, its category one of
  `rules`, `looks`, `stuck` or `other`. An idea is `{"kind":"idea","where","problem","wish?","page"}`, its `where`
  one of `rules`, `battle`, `lobby` or `other`. The text holds 10 to 2,000 characters (400 `Feedback.Invalid`).
- The host adds what the page cannot be trusted to send: the table, the seat, the round, sub-phase and journal
  length, the content, rules and engine, and, when `attach` is true, the seat's own view. For a practice it adds the
  scenario's id and the practice's journal since its cut. The page is kept as its path alone, and its last ten
  errors without the queries of the addresses they quote. A store that fails answers 503 `Feedback.Unavailable`; on a host where nobody signs in,
  feedback without a seat is answered 403 `Feedback.SeatRequired`. The same words sent again within two minutes
  answer the id already kept.
- Spam guards, for both kinds:
  - Two sends less than two minutes apart: 429 `Feedback.TooSoon`, with `retryAfterSeconds` and `Retry-After`
    (each refusal below says how long to wait too).
  - More than five a day per sender, and for a guest more than five a day per network address (counted in memory
    only, the address Cloudflare saw only when the request came through Cloudflare): 429
    `Feedback.TooMany`.
  - A second bug report on the same table and round, or a second idea within the hour, is merged into the first:
    200 `{"id","merged":true,"code":"Feedback.Merged"}`.
  - More than 100 new a day for the host: 429 `Feedback.HostFull`.
  - A muted sender gets 403 `Feedback.Muted` once the body is checked, before the spam guards. The mute lasts 30
    days, or until the operator lifts it. The operator sets it, or a third spam dismissal within 30 days does.
- `GET /api/feedback` asks, before the player writes, whether they are muted (#717). A signed-in request is
  checked as its account, whatever key it carries; a signed-out one as the guest seat its key names. It answers
  403 `Feedback.Muted`, or 204 for anyone else, the signed-out and seatless included, never kept by a cache. It
  looks up no game table, so a host still starting answers it too. A store that fails answers 503
  `Feedback.Unavailable`. The page asks it once loaded, and the battle view again for each seat it opens or leaves,
  and hides every **Report or suggest** entry on 403.
- The operator's Feedback tab (docs/tabletop/feedback-plan.md, R3) lists it newest first, twenty a page
  (`GET /api/admin/feedback?kind&status&category&page`: `kind` is `bug` or `idea`, `status` a status or `open`, the
  tab's default, for what is not closed) and reads one (`GET /api/admin/feedback/<id>`: the words, what the page and
  the host added, the position, the sender's mute, `underWay` while its table is still being played, and `unread` when
  its table's record is missing or unparseable).
  A filter naming no kind, status or category is refused 400 `Feedback.Invalid`. The sheet lists the `actions` that
  apply to it, and the page shows only those.
  `PUT /api/admin/feedback/<id>/status {"status","reason?","plan?","issue?"}` marks it `seen` (which reopens it),
  `reproduced` (a bug report only) or `dismissed` with a reason of one line and at most 300 characters, `"reason":"spam"` being the
  spam mark (400 `Feedback.Invalid` otherwise). An idea is also marked `planned` with its plan's path
  (`docs/…-plan.md`, lowercase), `shipped` (which closes it) or `declined` with a reason of one line, which the players
  who asked it read (which closes it too). The plan stays through any other status until another plan replaces it.
  Closed again with the same status, it keeps the time it was first closed.
  `POST .../<id>/merge {"into"}` merges an open idea into another open idea (feedback plan, I1). The merged idea leaves
  the list but keeps its sender and words. Its group's sheet shows it under `asks`; its own sheet shows `mergedInto`.
  Each sender counts once towards `askedBy`. An idea merged since is followed to the one its group became. A merge into
  a bug report, into itself or into an idea already in its own group is 400 `Feedback.Invalid`; into a closed idea, 409
  `Feedback.Closed`. A merged idea takes no status but dismissed, which takes it out of its group, and no kind (409
  `Feedback.MergedAway`). An idea others joined stays an idea (409 `Feedback.Grouped`). The list filtered to ideas
  sorts by `askedBy`, then by the newest ask.
  `DELETE .../<id>/merge` puts a merged idea back in the list as an idea of its own (#725): it keeps its status, leaves
  its group's `asks` and no longer counts towards its `askedBy`. A merged idea's sheet offers it as `unmerge`. An idea
  not merged, or unmerged already, changes nothing and notes nothing; an unknown id is 404 `Feedback.NotFound`. The
  Activity line is `feedback.unmerge <id> from <group>`.
  The list of open feedback (`status=open` or an open status), the Overview's count and the spam count read the index
  of feedback (`index.json` beside the records, #866), and the list opens the page's rows alone. The index is written
  with each change, checked against a listing of the store at each read, and built from the records at the first
  read. A record kept while its index could not be written is still answered as kept; the console says so. Each
  tending pass (`table-tend`, or the Jobs tab's) builds it again from every record. Deleting `index.json` makes the
  next read build it.
  A filter that keeps closed feedback reads every record.
  `PUT .../<id>/kind {"kind"}` turns a bug report into an idea, or an idea into a bug report. It keeps the words,
  position and sender and sets the status to seen. A closed one gets 409 `Feedback.Closed`.
  `POST .../<id>/reproduce` cuts a bug report's round as a match scenario (feedback plan, R4). The cut is at the top of
  the reported round, in the reporter's seat, through the Scenarios tab's own cutting. It reads the table's recorded
  session. For a report from a practice, it reads the scenario the practice was opened from, then the entries the
  report copied. Its title is `Report <id>, round N` and its goal is empty. It answers the sheet, whose `reproduction`
  is `{"id","link"}`, the link being `/battle?scenario=<id>`. The status does not change. Asked again while that
  scenario still exists, it cuts nothing and answers the same sheet. An idea is 409 `Feedback.NotABug`, and a report
  sent outside a round 409 `Feedback.NoPosition`. Reproduce on a report from a table still being played is 409
  `Feedback.UnderWay`. Until the game is over, its sheet offers no `reproduce`, and `reproduceOnceOver` says the
  round will be cut then (#736). A round the journal does not offer is 409
  `Scenario.RoundUnavailable`, with `rounds` listing the rounds it does offer. The source answers as the Scenarios tab
  does: 404 `Scenario.NoSession`, 409 `Scenario.NoRules` or `Scenario.ContentGone`; for a practice, 409
  `Scenario.NotFound` when the scenario it was opened from is gone, 409 `Scenario.UnderWay` (or `Scenario.SourceUnread`)
  while the game that scenario was cut from is still played (#833), and 409 `Scenario.Invalid` when its copied entries
  cannot be read. A report sent without its seat is 409 `Feedback.NoPosition` too: it is never cut in the other one.
  The sheet links the scenario while it is kept, and only on a bug report. A host that keeps no scenarios has no such
  route (404).
  `POST .../<id>/scenario.zip` returns that scenario redacted (feedback plan, R5), as `report-<id>.zip` holding one
  `scenario.json` to attach to a public issue. It is a POST because it writes an Activity line, and every answer says
  `Cache-Control: no-store, private`, so no cache keeps it past the panel's gate.
  The file includes the scenario's content. Its source's session, run and match are the report's own id (the match a
  GUID drawn from it), every journal line's time is the epoch, and the source's entries after the cut stop at the
  journal's length when the player wrote (a practice's report keeps every entry it copied). The file is then checked.
  It is refused with 409 `Feedback.NotRedacted` if it still names the session, keeps a time, or holds as a word of its
  own the report's table, an account, a seat's key, code or invitation, the pilot's key or a player's name. The
  content it includes and the fields' names are not read, and a content id (`spell:pummel:v1`) is one word, never
  the name inside it. The error says what the file holds, never the name. A report is 409 `Feedback.UnderWay` when its table is
  still under way or its scenario was cut from one. It is 409 `Feedback.TableUnread` when its table's record is missing
  (the session was deleted) or unparseable, or when the record of the table its scenario was cut from is unparseable (a missing one passes: another host's run), because nothing then shows the game is over. Either way, nothing a seat holds
  face down reaches the issue. An idea is 409
  `Feedback.NotABug`, and a report not reproduced, or whose scenario was removed since, 409 `Feedback.NotReproduced`.
  Each download is an Activity line, `feedback.download`, with the scenario's id. The sheet offers `download` once the
  game is over.
  `PUT .../<id>/status {"status":"filed","issue":N}` keeps on an open bug report the number of the issue the operator
  filed it as (400 `Feedback.Invalid` without a number above 0); a second call with another number replaces it. The
  number stays through the statuses set after it, and `filed=true` lists only what names an issue. `{"status":"fixed"}` closes a report that names an issue (409
  `Feedback.NotFiled` for one never filed). The tab sends it when GitHub's public API says the issue was closed as
  completed. The list and the sheet carry `issue` and `repository` (`owner/name`, where the sheet's **File an issue**
  opens GitHub's form `bug_from_report.yml`). The Activity lines are `feedback.file` with `#N`, and `feedback.status`
  with `fixed: #N`.
  `POST`/`DELETE .../<id>/mute` stops its sender for 30 days and lifts it (409 `Feedback.NoSender` for a deleted
  account's, or an account's on a host that keeps no account activity). An account's mute is kept with its activity
  record and dropped at its first write after the mute ends; a guest's is kept in the process's memory. Unknown
  feedback is 404 `Feedback.NotFound`, and a store that fails 503 `Feedback.Unavailable`. Asked again, a change
  writes nothing and notes nothing. Each change adds an Activity line (`feedback.status`, `feedback.convert`,
  `feedback.spam`, `feedback.mute`, `feedback.unmute`, `feedback.merge`, `feedback.reproduce`). A merged idea is
  deleted with the idea its group became. The Overview's Feedback waiting card counts feedback
  nobody has seen yet, in the warning colour while a bug report is new or the host is full for the day.

  A new one is kept and answers 201 `{"id","merged":false}`.
- Each one is kept as one record beside the recordings, under `feedback/` (or the feedback container). A host that
  records nothing keeps none and answers 404. Deleting an account removes the account from its feedback and keeps
  the text.

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
  one page, `/offline.html`, shown when a page cannot be loaded at all (ADR 0166), and the rulebook (ADR 0191).
- `/rules` is the official rulebook (ADR 0188), open to anyone: `rules.html` reads `table/rules.json` and draws its
  contents, every part and section, the concept cards and What changed. `/rules#<id>` opens a section, a part, an
  id `rules/moved.json` redirects, or a concept card's section, at the top of the screen; the filter keeps the
  sections whose title or text hold every word typed. It reads in English or in French (`table/rules.fr.json`, ADR
  0193): the language the address asks for (`?lang=fr` or `?lang=en`, the public book's link, ADR 0195), else the one stored
  on the device, else the browser's, and two buttons switch; a switch, once its book is read, drops `lang` from the address. **Rules** is in the main menu. The service worker keeps the
  page and the book, asks the network first and gives its copy only when the host cannot be reached or, for the
  rulebook's own files, answers with a server error, as Cloudflare does for a host that is down (ADR 0191). A copy carries `X-Rulebook-Copy: kept`, and the page then
  says the book is the one this device last read.
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
only; a device holding both seats is told nothing, ADR 0120). After a last visit, the notice says how many spells
were cast since (`Round 4: choose a spell. 3 casts since your last visit.`).

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
  override them for that game only (ADR 0140). The `GET` also says the tutorials the account finished and
  skipped (`tutorials.done`, `tutorials.skipped`, ADR 0187), kept in a document of their own beside the settings
  (`preferences/<account>.tutorials.json`): skipping a tutorial saves no settings.

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
  own. A game under way that waits for the player at one seat carries `recap`: `casts` (the spells cast since the
  seat's last visit, counting only those it may see; null before a first visit, ADR 0194) and `lastMoveAt` (when the
  last move was). While the journal cannot be read, a card has no `answerBy` and no `lastMoveAt`, and the list says
  so in one console line, not one per card (#861).
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
otherwise, refused against a bot and with one account in both seats, 409 `Reaction.Gone` once the operator
deleted the table's run, 503 `Reaction.Busy` when it could not be kept), kept with the table's record
(`reactions.json`, the last eight), served in each seat's view as `reactions` (null when the store cannot be read), shown once to the other player
unless their settings hide them, and never notified, counted in the bell or recorded as an event of the match
(ADR 0173).

#### A seat's last visit

The battle view tells the host the last feed sequence each seat was shown (`PUT /api/seat/<seat>/seen
{"sequence":12}` under the seat's token). It sends at most once every five seconds, and once more when the page is
hidden; a replay sends nothing. The host keeps the larger sequence with the time it was told, beside the table's record
(`seen-player1.json`, `seen-player2.json`, written only over the version read), ignores a smaller one, and answers
`{"seen":{"sequence","at"}}` with the mark kept; anything but a sequence of 0 or more is `400 Seen.Invalid`, and
`503 Seen.Busy` when every retry lost to another process or the store failed. Once the operator has deleted the
table's run it answers `409 Seen.Gone`: the host writes no file, so the run is not brought back. If this process could
not write the table's record, it cannot tell a deleted run from a missing record. It answers `503 Seen.Busy` (a
reaction: `503 Reaction.Busy`), not `409`. The operator's delete waits for a mark or a reaction already on its way. A
seen mark moves nothing on the table and sends no notice. A mark is kept under a stamp of the seat's key, so a seat
offered again starts with none. Each seat's view carries its own mark as `seen` (null before the first) and when the
journal was last written as `lastMoveAt` (null for a table that keeps none). The page reads the larger of its browser's
mark and the host's (ADR 0194).

#### The ladder, formats and the time to answer

- `/leaderboard` (`GET /api/leaderboard`) ranks the accounts by Glicko-2 over games between two of them
  (ADR 0124), except those opened with `"ranked":false` on `POST /api/lobby/tables` for an open seat or a friend
  (ADR 0155).
- A ranked game is 3v3, another format refused with `Lobby.RankedFormat` and none named played 3v3, while an
  unranked game, a game against a bot and one on a shared device take any format (ADR 0171).
- A game between two players gives each the time to answer `"turnLimit"` says on `POST /api/lobby/tables` (24,
  72 or 168 hours, 72 by default; none against a bot), and a player who lets it run out loses, both drawing when
  both ran out, and is told once on their devices when a quarter of it is left; every table it lists and each
  seat's view say when it runs out (`answerBy`, null while the journal cannot be read), which the lobby's cards
  and the battle view count down (ADR 0169).
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
- every piece of feedback closed more than 180 days ago deleted (ADR 0189).

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
A session still being played is refused (exit code 1), as the Scenarios tab refuses it (#736, #833).

### scenario-check

```bash
dotnet run --project src/DownfallArena.Cli -- scenario-check --scenario bug.scenario.json [--trace]
```

Whether a scenario still rebuilds on this engine and stands on the boards recorded at its cut, and whether its
recorded answers after the cut still apply (exit 0 or 1); a file kept under `scenarios/` is checked by the tests, so
a repro becomes a regression test. For a file with a `tutorial` block (ADR 0187) it also says whether each lesson
step is reached, in the order written (`scenarios/README.md`). `--trace` is a flag here, not a trace file. It prints
each seat's creatures (health, energy, stun, conditions) at the cut and after each entry past it, with the entry, to
find the entry where a rule went wrong (feedback plan, R6).

### scenario-generate

```bash
dotnet run --project src/DownfallArena.Cli -- scenario-generate --p1 greedy --p2 greedy --seed 5 --format 1v1 --round 3
```

A match of two bots (`--p1`, `--p2`, greedy by default; `--seed`, 1 by default; `--rules`, `--format`,
`--declaration`) played on this host's content and cut at the top of a round into a scenario file that carries
that content (docs/tabletop/scenarios-plan.md, S7): without `--round` it lists the rounds the match offers;
`--seat`, `--title` and `--goal` as `scenario-cut`. It writes `scenarios/<p1>-v-<p2>-seed-<N>-round-<N>.json`
unless `--out` names another file, and never over one already there. A file under `scenarios/` is listed in the admin panel's Scenarios tab at the
next start of a recording host (`scenarios/README.md`).

### scenario-import

```bash
dotnet run --project src/DownfallArena.Cli -- scenario-import --zip report-<id>.zip --issue 812 [--out file]
```

A bug's redacted scenario, the zip the operator attached to its issue (docs/tabletop/feedback-plan.md, R5 and R6),
written as `scenarios/bugs/<issue>-<slug>.json`: the slug is the scenario's title, or `round-N` when the title is the
one Reproduce gives. The zip must hold one `scenario.json` and nothing else. The scenario must be redacted (its source
is the report's own, and no journal time is kept), carry its content, have no `tutorial` block, and rebuild on this
engine with its answers applying, as `scenario-check` requires. Otherwise it is refused and nothing is written.
Players' names were checked by the host when the zip was downloaded (R5); the import cannot check them again, so
import only a zip the operator's own account attached to the issue. The file is written as this engine writes a
scenario, so a field the format does not know is dropped. A file under `bugs/` must be named after `--issue`. It never
writes over an existing file unless `--out` names it, and it says when the content carried is not this repository's.
The bug's test reads the file with `BugScenario` (`scenarios/README.md`).

### The hosted table's image

```bash
docker build -t downfall-table . && docker run --rm -p 8080:8080 downfall-table
```

The hosted table's image, locally: an admin panel on 8080, the operator's token in the log (infra/README.md).

### ops/read.sh, ops/report.mjs

```bash
AZURE=1 RESOURCE_GROUP=downfall-table scripts/ops/read.sh <dir>   # the readings, one JSON file each; AZURE=0 reads the table alone
node scripts/ops/report.mjs <dir> [--now <ISO time>]              # the production report, one phone screen in French
```

What `ops-report.yml` runs (ADR 0186), every morning at 10:23 UTC and on demand (*Read the table in production*,
Run workflow), signed in as the ops identity, which reads the resource group and writes nothing. `read.sh` reads
`GET /api/lobby`, the active revisions, the restarts and 5xx answers of the last 24 hours, the tending job's runs,
the messages waiting in the queues, the console's error lines, the month's cost and the budget; a reading that
fails is written as its error and the rest are read. `report.mjs` prints the report to the log, which is public:
counts, and error lines without emails, ids, tokens or query strings. `/ops` starts the workflow and reads its
log. `studio/ops.test.js` tests both, `read.sh` with a fake `az` and `curl`. `rollout.yml` asks the deployed
table for `GET /api/lobby` the same way once a deployment is up, and is red when it does not answer.

### ensure-labels.sh

```bash
GH_TOKEN=... GH_REPO=<owner>/<repo> .github/scripts/ensure-labels.sh [<labels.json>]
```

What `labels.yml` runs when `.github/labels.json` changes on `main`, and on demand (*Create the missing labels*,
Run workflow): it creates on GitHub the labels the file declares and the repository does not have, each with its
description. It only adds: a label that exists keeps its colour and description, and one the file does not name
is left alone. `studio/labels.test.js` tests it with a fake `gh`.

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

### design-system/build.mjs

```bash
node scripts/design-system/build.mjs
```

Builds the Downfall Instinct design system's files under `artifacts/design-system/project/` (ignored, and the only
place it writes): `tokens.json` from `docs/art/branding.css` and `docs/art/design-system/tokens.notes.json`,
`components/bundle.css` from the frame and `table/identity.css`, the font, and the brand book and component cards
as written. It fails when a palette colour has no usage note, or a note has no colour. A session publishes the
folder to the design system's page (docs/art/branding.md#design-system) with `/design-system`; it prints the
pictures to upload.

### battle/phaser-symbols.mjs

```bash
node scripts/battle/phaser-symbols.mjs "Phaser.Cameras.Scene2D.Camera#shake" Phaser.Geom.Point
```

Says, for each name, `yes` or `no`: whether the Phaser the battle installs (`battle/node_modules/phaser/types`)
declares it. A name is written as Phaser's documentation writes it (`Phaser.Namespace.Class#member`); a member is
also found on what its class extends, and `#` names a member of a class or a type, never of a namespace. It
exits 1 when a name is missing, and 2 with no name or when `npm --prefix battle ci --ignore-scripts` has not been
run. `PHASER_TYPES_PATH` reads another declaration file. The craft book (docs/art/battle-craft.md) is held to it
by `studio/battle-craft.test.js`.

### battle/filmstrip.mjs

```bash
node scripts/battle/filmstrip.mjs spell:pummel:v1
node scripts/battle/filmstrip.mjs spell:frostbite:v1 --actor a3 --targets b1,b3 --speed 2 --out frost.png
node scripts/battle/filmstrip.mjs spell:pummel:v1 --fizzle
node scripts/battle/filmstrip.mjs spell:hateful_sacrifice:v1 --wound
node scripts/battle/filmstrip.mjs spell:pummel:v1 --damage 8 --defeat --motion
node scripts/battle/filmstrip.mjs --all --out artifacts/filmstrips
```

Films one effect on the effects bench (`/battle-lab?bench`) and writes its contact sheet: twelve frames at phone
size with the millisecond under each (battle/README.md, "The effects bench"). It needs the built bundle
(`npm --prefix battle run build`) and the browser suite's Playwright, and serves `table/` itself; `--url` films
a laboratory page served elsewhere. `--actor` and `--targets` name creatures of the fixture (`a1` to `a3`, `b1`
to `b3`), `--speed` sets the playback speed (0.5, 1 or 2). `--fizzle` films the spell fizzling, and `--wound` its
caster paying in blood; the two are refused together, and their sheets are named `pummel-fizzled.png` and
`pummel-wound.png`. `--damage` is the health each creature struck loses (the weight of the blow) and `--defeat`
fells the first one struck; neither goes with `--fizzle`. `--motion` writes `<sheet>.motion.jsonl` beside the
sheet: at each frame, how each figure stands and what the view does. `--frames`, `--interval` (a multiple of 20 ms) and `--columns`
set the sheet. `--scale` sets the device pixel ratio. `--out` is a `.png` for one effect, a folder otherwise
(`artifacts/filmstrips/` by default). It exits 1 with no effect named, an option it cannot film, or an effect
the bench does not list.

### recap-fixture.py

```bash
dotnet run --project src/DownfallArena.Cli -- play --seed 1 --trace artifacts/recap-match.trace.json
python3 scripts/recap-fixture.py
```

Rebuilds the recorded match the recap's tests read (`table/battle-recap.test.js`) from the trace in the ignored
`artifacts/`: seat player1's feed of a few rounds, frames trimmed to the stats the recap reads, and the board at a
few named moments. Its paths are fixed. Run it when an event's shape changes, and read the tests' numbers again.

### workflow-metrics.py

```bash
python3 scripts/workflow-metrics.py
```

The numbers of docs/engineering/ai-workflow-plan.md measured again: git readings first, `--github` for the rest.

| Flag | Default | What it does |
| --- | --- | --- |
| `--last <n>` | 300 | The last pull requests merged on `origin/main` that the git readings and `--github` read |
| `--github` | off | How long a pull request stays open, merged before green, CI on `main`. Needs `GITHUB_TOKEN` |
| `--minutes` | off | The Actions minutes a private repository would be billed. Each job counts from start to end, rounded up to the minute. Reports per workflow and per group of CI's jobs, the cancelled jobs' share and a month's estimate. Jobs on a runner of the project's own are counted apart. Needs `GITHUB_TOKEN`, a session's or a person's: a workflow's own allows 1,000 requests an hour, about one busy week |
| `--days <n>` | 7 | The window `--minutes` reads: whole UTC days ending yesterday |

`/retro` reports `--minutes` each week (docs/engineering/ci-minutes-plan.md, M1), so each later stage shows the
minutes it saved.
