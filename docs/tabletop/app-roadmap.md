# Roadmap: build the playtest app

Status: **Plan** (2026-09-17). Phase 6 of [plan.md](plan.md): "build it". The *what* is
[playtest-app.md](playtest-app.md) — the rules it plays, the server-side hidden-information boundary, the
client's shape, hotseat on one screen, what a session records, and what is not in v1. This document is the
*how*: the stages, in order, each one or two pull requests that leave `main` green.

Nothing here restates the specification. Where the two disagree, the specification wins on *what* and this
document wins on *when*.

## Ground rules

- **An instrument, not a product.** This app exists to playtest the board game, not to distribute it. Nothing
  is published, nobody signs up, and the only two people who will ever use it are in the same room. So it
  spends on power where a product would spend on safety: no accounts, no onboarding, no hardening past the
  seat token on a LAN-bound host. That budget goes into the piloting surface instead.
- **Piloting is expressed in decisions the engine accepts, never in state the engine did not produce.**
  Seating a bot, swapping a seat, fast-forwarding, replaying from a seed: all legitimate, because each is a
  decision the aggregate would have accepted from a player. Setting a Creature's Health to 3 is not. See
  "Where the line is" in stage 6.
- **The engine decides, the client draws.** Every stage below adds rendering, transport, recording or
  control. No stage adds a rule. A rule that feels wrong at the table becomes an engine ADR (plan.md:224-231).
- **No framework, no package, no build step.** `table/` is `index.html`, one stylesheet and ES modules with an
  injected transport, tested with `node --test` (ADR 0024). A stage that reaches for a UI toolkit, a NuGet
  package or a second process stops and writes its own ADR first, with a reason no existing piece answers.
  Neither mobile-first nor the pilot panel is such a reason: both are layout, not tooling.
- **Mobile first, reachable from desktop.** The phone is the width every player-facing screen is designed at
  and the width every stage must be usable at. The desktop is the same page widening — never a second layout,
  never the one designed first and shrunk. A stage that only works at desktop width is not done. The one
  exception, argued in stage 6, is the pilot page, which no player sees.
- **Architecture tests stay green at every stage.** Cli composes, Infrastructure adapts, Application
  orchestrates, Domain decides. `tests/DownfallArena.Architecture.Tests/LayerDependencyTests.cs:51-60` and
  `:62-71` are the tests that say so.
- **No new port.** `IMatchRepository` (`src/DownfallArena.Application/Matches/Ports/IMatchRepository.cs:11,13`),
  `IArtifactWriter` (`src/DownfallArena.Application/Learning/Ports/IArtifactWriter.cs:10,13,16`),
  `IRandomSource` (`src/DownfallArena.SharedKernel/Randomness/IRandomSource.cs:11,16`) and `TimeProvider`
  (registered at `src/DownfallArena.Application/ApplicationServiceCollectionExtensions.cs:29`) answer
  everything this app needs.
- **The content moves under the app, deliberately and often.** The maintainer is tuning toward 10 to 15 Rounds
  (plan.md:110-118). Nothing in `table/` may carry a Spell, a number or a card face. This is checked by a test
  from stage 3 on, not by review alone.

## The shape it is built into

| Piece | Where it comes from |
| --- | --- |
| The host | The studio's: a fixed static route table (`src/DownfallArena.Cli/Studio/StudioFiles.cs:7-33`), an `HttpListener` loop (`src/DownfallArena.Cli/Studio/StudioServer.cs:37-56`), the same-origin fence (`StudioServer.cs:141-156`) |
| The seat | `IPlayerAgent` — "a bot, a scripted test, a UI adapter" (`src/DownfallArena.Application/Agents/IPlayerAgent.cs:8`); `ConsoleAgent` is already a blocking human (`src/DownfallArena.Cli/ConsoleAgent.cs:13`), wired at `src/DownfallArena.Cli/GameSession.cs:94` |
| The opponent | `AgentSpec.Parse` (`src/DownfallArena.Application/Agents/AgentSpec.cs:16-32`) and `IAgentFactory.Create` (`src/DownfallArena.Application/Agents/AgentFactory.cs:29-44`), already seated per slot at `GameSession.cs:108-109` |
| The sequencing | `MatchDriver.PlayAsync` (`src/DownfallArena.Application/Matches/Driving/MatchDriver.cs:18`), including the per-Creature board re-read of ADR 0039 (`MatchDriver.cs:71-87`) and the driverless resolution step (`MatchDriver.cs:91-93`) |
| The dataset and the trace | `RunRecorder` (`src/DownfallArena.Application/Learning/Recording/RunRecorder.cs:24-27,51-96`) and `MatchTraceRecorder` (`src/DownfallArena.Application/Learning/Tracing/MatchTraceRecorder.cs:40-49,53,56`) |
| The result page | `ViewerPage.Render` (`src/DownfallArena.Cli/Studio/ViewerPage.cs:19-27`), served as the studio serves a run (`src/DownfallArena.Cli/Studio/StudioApi.cs:112-126`) |
| The host's tests | `tests/DownfallArena.Cli.Tests/`, which already tests a host of this exact shape (`Studio/StudioFilesTests.cs`, `Studio/StudioApiTests.cs`, `Studio/StudioPageContractTests.cs`) |

The page files are copied next to the tests by a wildcard, not a list
(`tests/DownfallArena.Cli.Tests/DownfallArena.Cli.Tests.csproj:18`), and `StudioFilesTests.cs:44-50` asserts
the served set against whatever landed there. `table/` gets the same two lines, so a module the page imports
and the route table does not serve fails the build instead of blanking the screen.

---

## Stages

### Stage 1. A walking skeleton: `table`, two seats, one screen, one match

**The smallest thing that is a real playtest**: two people at one phone play a full Match, through the engine,
to its outcome, with the rules they are testing — even though the screen is a list of lines and a Spell is an
id. Everything after this stage makes a session *better to read* or *easier to run*. Nothing after it makes a
session *possible*.

| | |
| --- | --- |
| Layer | Application (one new check), Cli (the host), `table/` (the page) |
| Ships | `dotnet run --project src/DownfallArena.Cli -- table --port 5100 --rules <file> --p2 greedy` |

**Application.** `PlayerDecision` — a closed record of the things a seat can decide (unlock, pass, speed,
intent, targets) — and `PlayerDecisionCheck.Validate(PlayerOptions, PlayerDecision) -> Result`. This is the
409 pre-check. It exists because `MatchDriver` throws on a refused decision, deliberately: "an agent's
decision was refused" is a bug in a bot (`MatchDriver.cs:122-128`). A human gets a refusal for a reason. The
host validates against the seat's own pending `PlayerOptions` **before** handing the decision to the blocked
seat, so the driver's invariant holds and a stale tap is a `409` with the `DomainError` code instead of a dead
session. It is a pure function over what `PlayerOptionsProjection.Build` already returned
(`src/DownfallArena.Application/Matches/Projections/PlayerOptionsProjection.cs:17,33-41`) — no port, no
aggregate, no rule of its own.

**Cli.** `TableHost` (the command, in the shape of `src/DownfallArena.Cli/Studio/StudioHost.cs:19-79`),
`TableServer` (the listener and the same-origin fence), `TableFiles` (the fixed route table), `TableSession`
(create, join twice, run `MatchDriver.PlayAsync` on a background task, hold the two seats), and `SeatAgent`.

**`SeatAgent` is built as a delegating agent from the first commit.** It holds a current `IPlayerAgent` and
forwards the four `Decide*` calls to it; the human delegate blocks on the HTTP host, a bot delegate answers
at once. This costs nothing now and it is the mechanism behind every piloting capability in stage 6. Building
it any other way is the one decision in this roadmap that would be expensive to undo.

Routes: `/api/session`, `/api/seat/{slot}`, `POST /api/seat/{slot}/decision`. Two opaque seat tokens, printed
at start-up, one per seat, required on every API request.

**`table/`.** `index.html`, `table.css`, `table.js`, `transport.js`. One column at 360 px. A bottom sheet
sized in `dvh` holding the one decision. A full-screen **pass the device** screen between seats — a
first-class screen from the first commit, not a modal added later: on one phone it is the entire fence between
the two seats, and it is the same fence the cardboard has (playtest-app.md §4).

**Piloting this stage earns**, both nearly free:

- **Choose the opponent, either seat.** `--p1` and `--p2` take the specs the CLI already parses —
  `random`, `greedy`, `heuristic:<weights.json>`, `lookahead`, `minimax`, `policy:<policy.json>`, `explore:<rate>`
  (`AgentSpec.cs:16-32`) — and `IAgentFactory.Create(spec, rules, random)` builds any of them
  (`AgentFactory.cs:29-44`). A seat is an `IPlayerAgent`, so this is one option and not a subsystem. It is
  also how one person tests the app between playtests.
- **Fast-forward at start-up.** `--handover <round>`: both seats start as bots and flip to their human
  delegate at the top of round N. This is the single highest-value piloting item and it is three lines on top
  of `SeatAgent`, because the tenth Round is the one nobody reaches by hand and it is exactly the one that
  needs reading. It records nothing yet — stage 5 is where mixing bot and human decisions in one dataset
  starts to matter, and stage 6 is where it is made honest.

**Left out of stage 1, on purpose.** Card faces (a Spell prints as its id, exactly as `ConsoleAgent.cs:37`
does today), the event feed, the condition dock, the talent mat, the run directory, notes, the pilot page, the
die. Each is a better reading of a session that already happens.

**Not left out, and it must not be.** The per-seat API and the server-side boundary. A skeleton that serves
one payload with both boards produces a playtest that is silently worthless (playtest-app.md §2.4), and
retrofitting the boundary means rewriting every route. Likewise the 409 pre-check: without it the first
mis-tap ends the session with an exception.

**Tests.**

- `tests/DownfallArena.Application.Tests` — `PlayerDecisionCheckTests`: every decision the options offer is
  accepted; a decision naming the other seat's Creature is refused; a decision for a sub-phase that is not
  pending is refused; a target set of zero on a Spell with no legal target is **accepted**, because that is
  what the engine does (`src/DownfallArena.Application/Matches/Projections/TargetOptions.cs:6-9`,
  `src/DownfallArena.Domain/Matches/Rules/Combat/LegalTargets.cs:10`).
- `tests/DownfallArena.Cli.Tests/Table` — `TableFilesTests` (the module sweep of `StudioFilesTests.cs:44-50`),
  `TableSessionTests` (two scripted seats drive a Match to an outcome without HTTP; a seat whose delegate is
  swapped between rounds keeps the Match running), `TableApiTests` (the other seat's GET answers `Waiting`; a
  wrong token is `403`; a stale decision is `409` and the session survives it).
- `node --test table/*.test.js` — `transport.test.js`, against a stub transport in the shape of
  `studio/backend.test.js:10-25`.

**What a reviewer checks.**

1. Grep `table/*.js` for a rule. No filtering of a Spell list, no target counting, no initiative ordering.
2. The player 2 payload, **as serialized bytes**, contains no intent and no Speed choice of player 1. Assert on
   the JSON string, not on the DTO: a DTO test passes while a serializer writes a field.
3. `MatchDriver.cs` is untouched.
4. `SeatAgent` delegates; it does not decide.
5. The page is opened at 360 px before it is opened at 1280 px.

**Unblocks.** Everything.

---

### Stage 2. The LAN bind, and a phone in hand

Small, early, and it is what makes "mobile first" checkable from here on. Half a day of work that every later
stage's review depends on.

A loopback host is only enough when both players use the browser on the machine running the engine
(playtest-app.md §4). Hotseat **on a phone** — two people passing one phone across a table — is the normal
case, and it needs the host to leave `127.0.0.1`. The studio's server binds the loopback prefix explicitly
(`StudioServer.cs:31-32`) and ADR 0023 refused to unbind it, for reasons that are about the studio's API: it
writes content files, deletes them, and takes a path from a request. The table host writes nothing but its own
session directory, deletes nothing, and takes no path from a request. That difference is the whole argument,
and it is the seat token that carries it: **every route requires one, in both the loopback and the LAN case.**

The desktop case is then the easy one — the same host, the same URL, the same page, no desktop-only build
step and no second layout. Nothing is added for it.

**Ships.** `table --bind <address>`, defaulting to loopback; a start-up line printing the URL and a short code;
the seat token kept in `localStorage`.

**Tests.** `Cli.Tests` — every route refuses a request with no seat token or the wrong one; the same-origin
fence of `StudioServer.cs:141-156` is carried over and tested at the same boundary.

**Check before the first session on a phone**: a non-loopback `HttpListener` prefix needs a URL reservation on
Windows. Binding an explicit interface address rather than `+` avoids it (playtest-app.md, Open questions 1).

**What a reviewer checks.** Open the page **on a phone**, over the LAN, and play three Rounds of the text
skeleton with someone. Not a narrow browser window: a phone, in daylight, held in one hand.

**Unblocks.** The reviewer check of every stage after it.

---

### Stage 3. The catalogue projection: nothing in the client carries content

The load-bearing stage for the constraint that the content keeps changing. After it, a tuning pass is a
rebuild and a restart, never an app change.

| | |
| --- | --- |
| Layer | Application (the projection), Cli (one route), `table/` (the card) |
| Ships | `GET /api/catalogue`, and a hand that prints real cards |

**Application.** `CardFace` and `CatalogueView`, built by `CatalogueProjection.Build(IGameResources, RuleSet)`:
one card per Spell with its name, class, type, energy cost, Initiative, targeting, effects with their
durations and its Critical chance, plus the enabled Talent trees as bands, plus the content hash
(`IGameResources.Version`, `src/DownfallArena.Domain/Resources/IGameResources.cs:15`) and the `RuleSetStamp`.

It reads the **built** catalogue through `IGameResources`, never the authored files. The studio's
`/api/catalogue` answers `ContentStore.Read()` (`src/DownfallArena.Cli/Studio/StudioApi.cs:197`), which is the
authored side and includes documents a build prunes
(`src/DownfallArena.Infrastructure/Resources/DisabledContent.cs:24-25`). The app must print the catalogue the
Match is playing, which is why this is a new projection and not a reuse of that route.

**The d20 threshold.** [d20-criticals.md](d20-criticals.md) is landing now. A card prints `d20: 14+` rather
than `35%`. The threshold is computed **server-side**, in the projection: it is `21 - 20 x chance`, and the
chance is the one the engine rolls against
(`src/DownfallArena.Domain/Matches/Rules/Combat/ResolutionRules.cs:56`). Until the rule lands, the projection
prints the chance as a percentage and omits the threshold line — the same fallback components.md:778 gives
the print generator.

One thing to write down while writing it, because it is the app's only dependency on the d20 rule: the printed
chance is the whole chance **only because a Creature carries none**. The engine still sums the Creature's
chance with the Spell's (`ResolutionRules.cs:56`), and `CreatureSnapshot` still carries one
(`src/DownfallArena.Domain/Matches/Creatures/CreatureSnapshot.cs:35`). If a Creature ever carries a chance
again, the threshold stops being a property of a card and becomes a property of a caster, and it moves out of
this seat-independent, cacheable route into the per-seat payload. Say so in the projection's doc comment.

**Cli.** `GET /api/catalogue`, read-only, seat-independent, answered with the content hash as an ETag so the
client fetches it once.

**Tests.**

- `Application.Tests` — `CatalogueProjectionTests`: every Spell in `IGameResources.Spells` has exactly one
  card; a card carries cost, Initiative, targeting and every effect with its duration; the view carries
  `IGameResources.Version`; a threshold is a whole number from 1 to 20 for every Spell whose chance is not
  zero, and absent for every Spell whose chance is zero. The range includes 1: a chance of 1.00 is a legal
  twentieth and `21 - 20 x 1.00` is `d20: 1+`, a Spell that always crits. Nothing authors one today, which is
  exactly why a test that stopped at 2 would have gone unnoticed until one did.
- `Cli.Tests` — **the content-freedom test**: `table/index.html` and `table/*.js` contain no `spell:` id, no
  class name, no Spell name and no stat number. A regex sweep over the shipped files, in the spirit of
  `tests/DownfallArena.Cli.Tests/Studio/StudioPageContractTests.cs:19-28`. This is the test that makes "a
  tuning pass never requires an app change" true rather than aspirational.
- `node --test` — the card renderer draws every field from its argument and has no default.

**Piloting this stage earns.** Nothing on its own, but the pilot's spell names and thresholds come from here
rather than from a second list.

**What a reviewer checks.** Rebuild `data/dst` with a changed Spell, restart the host, reload the page, and
see the new number with no diff in `table/`. That is this stage's acceptance criterion, performed, not argued.

**Unblocks.** The hand, the talent mat (stage 4), and the session copy of the catalogue (stage 5).

---

### Stage 4. The board, at phone width

The components of components.md, one for one, legible in one hand.

| | |
| --- | --- |
| Layer | Application (one projection), Cli (one route), `table/` (most of the work) |
| Ships | The initiative track, the creature boards, the condition dock, face-down intents, the talent mat tab |

**Application.** `SeatVisibility.CanSee(IMatchEvent, PlayerSlot)` — a **default-deny** closed switch over the
fifteen events in `src/DownfallArena.Domain/Matches/Events/`. The raw stream is not safe to serve:
`MatchTraceRecorder` keeps every event with **both** boards, on purpose (`MatchTraceRecorder.cs:40-49`;
`docs/learning/artifacts.md:152-154`). Two events are private — `IntentSubmitted`
(`src/DownfallArena.Domain/Matches/Events/IntentSubmitted.cs:7-10`) and `SpeedChoiceSubmitted`
(`.../SpeedChoiceSubmitted.cs:10`) — and they are the two `PlayerBoardStateProjection` already filters
(`src/DownfallArena.Application/Matches/Projections/PlayerBoardStateProjection.cs:37-38`).

Note while classifying, because it is not obvious: `EvolutionChoiceSubmitted` is *public*, even though the
board filters it too (`PlayerBoardStateProjection.cs:35`). Both teams' `CreatureSnapshot`s are served whole
(`PlayerBoardStateProjection.cs:19-20`) and a snapshot carries `KnownSpells` (`CreatureSnapshot.cs:39`), so an
unlock is already visible to the opponent. The event says nothing the board does not. Write that reason in the
switch arm; a default-deny table is only worth its test if every arm has a stated reason.

**Cli.** `GET /api/seat/{slot}?since=N` gains the event feed: `MatchTraceRecorder.EntriesOf`
(`MatchTraceRecorder.cs:53`) hands back the entries recorded so far, the host drops the two boards each entry
carries and filters the events through `SeatVisibility`. The trace itself is never served during a session.

**`table/`.** The components, from the projections that already carry them, and which phone rule each owes:

| Component | Source | Phone rule it owes |
| --- | --- | --- |
| Initiative track | `PlayerBoardState.Timeline`, `ActivationSlot` | A horizontal strip, Quick band then Standard, scrolling sideways at 360 px |
| Creature board | `CreatureSnapshot` (`CreatureSnapshot.cs:21-41`) | **Numbers, not rails**: `14/30` and a bar, never a 31-cell track |
| Condition dock | `ConditionSnapshot(Effect, RemainingRounds, Source)`, carried at `CreatureSnapshot.cs:41` | Chips grouped by remaining Rounds; permanent in their own group |
| Hand and face-down intent | `CreatureSnapshot.KnownSpells` for the hand, `IntentOption.CastableSpells` for what is enabled, `PlayerBoardState.Intents` for the seat's own backs (`PlayerBoardState.cs:40-41`) | The opponent's back is a count and carries no data at all |
| Talent mat | The bands of stage 3, `KnownSpells` for the pips | **A tab**, not a panel: it is only touched during Evolution |
| Round track | `PlayerBoardState.RoundNumber`, `Phase`, `SubPhase`, the cap from the session stamp | One header line, the Round's shape as a collapsible strip |
| The decision | `PlayerOptions`, one filled section at a time | **The bottom sheet**, sized in `dvh`, board scrolling behind it; two taps to commit an Intent or a target set |

There is no undo (playtest-app.md §7) and a mis-tap on a phone is the misplay this app will produce most.

**The critical, on screen.** The card shows `d20: 14+` (stage 3) and the resolution line says whether it
landed, from `CombatResolution.IsCritical`
(`src/DownfallArena.Domain/Matches/Rules/Combat/CombatResolution.cs:43`). It does **not** show a face. The
Domain reports whether the cast crit and not what was drawn, and a client that invented a face would be
drawing a number the engine did not draw. See "The die" below.

**Tests.**

- `Application.Tests` — `SeatVisibilityTests`: reflection over the Domain assembly enumerates every type
  implementing `IMatchEvent` and fails when one is unclassified. There are fifteen today; the test exists so
  the sixteenth cannot be served by accident.
- `Cli.Tests` — the feed is monotonic in `since`, never carries an entry's boards, and never carries the other
  seat's `IntentSubmitted`. Asserted on the serialized bytes.
- `node --test` — the initiative strip renders the order it is given and never sorts; the condition dock groups
  by `remainingRounds` and puts `null` in the permanent group; the decision sheet enables `done` at
  `MinTargets` and disables past `MaxTargets`, which is the same walk `ConsoleAgent.cs:52-66` does.

**Piloting this stage earns.** The board and the options a seat holds are now addressable, which is what
"what would the bot do" needs in stage 6.

**What a reviewer checks.** The page on a phone, over the LAN of stage 2, with six Creatures, a full condition
dock and a full timeline, scrolled top to bottom with one thumb. Then the same page at 1280 px: it must be the
same page, wider.

**Unblocks.** A session worth watching.

---

### Stage 5. The recorded session

| | |
| --- | --- |
| Layer | Application (one record), Cli (the wiring), docs |
| Ships | `runs/playtest/<session-id>/`, `/api/notes`, `/session/{id}` |

**Application.** `PlaytestNote` and `NoteKind` (`Decision`, `Refused`, `Lookup`, `Misplay`, `Comment`), with
the fields of playtest-app.md §5.3, written through `IArtifactWriter.AppendJsonLinesAsync`
(`IArtifactWriter.cs:16`) and timed with `TimeProvider`. Same JSON conventions as every other artifact
(`docs/learning/artifacts.md:31-43`).

**Cli.** `RunRecorder` around both seats. Nothing new is written to produce the first four files:
`StartAsync` opens them and writes a zero-count manifest so an interrupted session still says what it was
(`RunRecorder.cs:51-56`), `Wrap` returns a `RecordingAgent` (`RunRecorder.cs:58-69`), `MatchPlayedAsync`
closes the episodes and the trace (`RunRecorder.cs:71-93`), `FinishAsync` rewrites the counts
(`RunRecorder.cs:96`). The stamp says `human` or `human:<initials>` where `GameSession.cs:94` says `"Human"`,
so `compare-stamps` reports the agents axis (`src/DownfallArena.Application/Learning/RunStamp.cs:64`).

Two host behaviours only a human session needs:

- **Checkpoint the trace after every accepted decision.** `MatchTraceRecorder.EntriesOf` hands back the
  entries without forgetting the match (`MatchTraceRecorder.cs:53`). A session abandoned at Round 9 leaves a
  readable partial trace. A bot batch never needed this because a bot batch is never interrupted by dinner.
- **Write `catalogue.json` once, at `StartAsync`.** The card faces of stage 3, into the session directory. See
  "A recorded session whose content no longer exists" below. It is one more `WriteJsonAsync` on the existing
  port, and the viewer ignores it: `classifyDocument` answers `unknown` for a document that is not a report, a
  trace, a manifest or an evaluation (`viewer/index.html:66-72`), exactly as `classifyLines` already ignores
  `notes.jsonl` (`viewer/index.html:58-64`).

Three one-tap buttons produce the last three note kinds — **"I had to look this up"**, **"that was a
misplay"**, and a comment box on the end screen. `Decision` notes are automatic and nothing about the timing
is asked of the player. `Refused` notes are written by the 409 path of stage 1.

**Docs in the same PR.** `docs/learning/artifacts.md` gains `notes.jsonl`, `catalogue.json` and
`runs/playtest/<id>/` as a run directory. `AGENTS.md` and `.claude/skills/verify/SKILL.md` gain
`node --test table/*.test.js` beside the studio's.

**Tests.**

- `Application.Tests` — a note serializes in the artifact conventions; `elapsedMs` comes from the injected
  `TimeProvider` and never from the wall clock.
- `Cli.Tests` — a scripted session leaves all six files; every step's `action` is in its `candidates`, which
  `RecordingAgent.cs:64-69` already enforces and the 409 pre-check of stage 1 keeps from being provoked; an
  abandoned session leaves the zero-count manifest and a partial trace.

**Piloting this stage earns.** The decision log the pilot page shows is this, read back.

**What a reviewer checks.** Drop `runs/playtest/<id>/` on `viewer/index.html`. It must open as a Batch and its
trace as a Match, with no change to the viewer (viewer/README.md:12-13).

**Unblocks.** Stage 6's honesty about who decided what, and the learning pipeline: `steps.jsonl` from a human
is the input `train-clone` already reads.

---

### Stage 6. The pilot: the instrument's control surface

The stage where the app stops being a game client and becomes an instrument. Its users are the two people
running the test, and its whole job is to make a session smooth and productive.

#### Where the line is

**Piloting is expressed in decisions the engine accepts, never in state the engine did not produce.**

Everything the pilot does goes through the same four `Decide*` calls and the same commands. Nothing writes to
a Creature or a Round. The Domain already makes the wrong side unreachable — `Round` and `Creature` mutators
are `internal` and only `Match` and the rules it runs call them (AGENTS.md, Domain conventions;
docs/roadmap.md:157), and the only public entry points are the commands of `MatchCommandHandlers`. The rule is
written down here so that nobody widens that later "just for the pilot".

Why it matters concretely: set a Creature's Health to 3, and every trace entry, every step and every note
after it describes a game no Match could have reached. The trace still reads as a sequence of legal moves.
That is the same failure as the leaked hidden information of playtest-app.md §2.4 — a crash you notice, this
you do not.

#### What it ships, and what each costs

| Capability | What the engine already gives | Verdict |
| --- | --- | --- |
| **Choose the opponent** | `AgentSpec.Parse` (`AgentSpec.cs:16-32`) and `IAgentFactory.Create(spec, rules, random)` (`AgentFactory.cs:29-44`); a seat is an `IPlayerAgent`. One option, not a subsystem. | Shipped in **stage 1** |
| **Fast-forward at start-up** | `SeatAgent` delegates; its delegate is a bot until round N. | Shipped in **stage 1** |
| **Hot-swap a seat mid-match** | Swap the delegate. Mechanically free. What it costs is the truth of the record — see below. | **This stage** |
| **Fast-forward at will** | The same swap, at a round boundary, from the pilot page. **Refused mid-Round**: the driver asks one seat for several decisions inside one sub-phase (`MatchDriver.cs:65-68,76-85`), and a seat that changes hands between two Creatures of the same team makes that Round unreadable. | **This stage** |
| **What would the bot do** | `GreedyAgent` is stateless and takes no random source (`src/DownfallArena.Application/Agents/GreedyAgent.cs:13-15`). The pilot calls the matching `Decide*` with the board and options the seat already holds, and shows the answer without submitting it. No mutation, no draw from the Match's source, no step recorded. Since ADR 0051 a recorded step also carries the scorer's terms of **every** candidate (`StepRecord.CandidateTerms`), so the pilot can show *why* the bot prefers its choice, per term, rather than only what it would pick — the same numbers a policy now learns from. | **This stage**, cheap, and a reading no cardboard session can produce |
| **The decision log** | The session already writes it (stage 5). The pilot reads it back. | **This stage**, free |
| **Simulation from here** | There is **no way to clone a `Match`**: `InMemoryMatchRepository` holds the aggregate itself (`src/DownfallArena.Infrastructure/Matches/InMemoryMatchRepository.cs:13,17-25`) and the aggregate has no copy. A rollout is therefore a **replay** — a fresh Match on the same seed and content, the session's recorded decisions submitted in order, then bots play on. That is a real piece of work. | **After the first session** |
| **Replay and branch** | The same replay, stopped one decision early and given a different one. | **After the first session** |
| **The content A/B** | The same replay against another content hash. `AddGameResources` registers `IGameResources` as a singleton from one schema path (`src/DownfallArena.Infrastructure/InfrastructureServiceCollectionExtensions.cs:38-46`), so two hashes in one process needs a second resolution path. The cheap answer needs none: two hosts on two ports, one per hash, the same decision list replayed into each, and the two run directories compared in the viewer — `RunStamp.DifferencesFrom` reports `content` as its own axis (`RunStamp.cs:60`) and the viewer's Compare does the rest (viewer/README.md). | **After the first session**, and probably with no new C# at all |

Why the last three wait: each is worth its cost only once a first real session has said which decisions
players actually find hard. The `Decision` notes with a long `elapsedMs` are that list, and building a
second-opinion tool before it exists is guessing at what the second opinion is for.

**A limitation of a replayed rollout, to state before building it.** Holding the Match seed keeps the replayed
prefix exact, which means the critical draws up to the branch point are the ones that happened; the spread
across M rollouts then comes from the agents' own random sources only, which are seeded apart from the Match
(`GameSession.cs:108-109`). Varying the Match seed instead would change the prefix, and the position would no
longer be the one the player is looking at. Decide which of the two the reading wants first.

#### The record must not lie about who decided

A seat that changed hands mid-match makes the run's own stamp false. Three things fix it, and the third is a
format change:

- **The stamp says so.** `RunStamp.Player1Agent` is one string for the whole run (`RunStamp.cs:20-22`). A
  swapped seat stamps the composite — `greedy>human@r10` — so `compare-stamps` sees the agents axis differ
  (`RunStamp.cs:64`) and nobody folds the session into a training set by name.
- **A `Seat` note per swap**, in `notes.jsonl`, with the round and the direction.
- **`StepRecord` gains `decidedBy`.** It has no such field today (`docs/learning/artifacts.md:56-68`), though
  ADR 0051 added `CandidateTerms` to the same record, so growing it is a move the dataset has just made. A
  fast-forwarded session's `steps.jsonl` cannot be separated into bot and human decisions — and a cloning run
  would learn the bot's opening as human play. Adding it is backward compatible on both readers: the viewer
  classifies a step by `'observation' in first` (`viewer/index.html:58-64`), and the Python `Step.from_json`
  reads named keys and ignores anything else (`learning/src/downfall_learning/artifacts.py:79-106`). It is
  also **useless until the Python side filters on it**, because `Step` has no such field
  (`learning/src/downfall_learning/artifacts.py:66-77`). So the field and the filter land together, with a
  pytest, or the run stays out on its stamp alone. This is the one stage that touches `learning/`.

#### The pilot page, and why it is the one desktop-shaped surface

`/pilot`, with its own token, never a seat token. Same host, same route table, same `node --test`, no
framework. Its content is a table — the seats and their agents, whose decision is pending and of what kind,
the accepted decision log, the round clock — and a table is what a wide screen is for. No player ever sees it,
and it is opened on the machine running the host, which is where the operator already is. So: **the players'
page is mobile-first and widens; the pilot page is desktop-first and must still open in one column on a
phone**, because a hotseat-on-a-phone session may have only one device in the room. Designed at desktop,
usable on a phone, is the reverse of every other screen in this app, and it is honest for exactly this one.

**The pilot page is not a god view, and this is the trap.** In a hotseat session the operator is usually also
one of the two players. A pilot page showing both boards would hand that person the opponent's six face-down
intents — the leak of playtest-app.md §2.4, through the back door, on the operator's own screen. So the pilot
page:

- shows the session state and the seats, never a board and never a hand;
- shows the decision log filtered by the same `SeatVisibility` of stage 4 — a declared Intent appears when it
  is revealed, not when it is submitted;
- shows "what would the bot do" **only for the pending decision of the seat whose turn it is**, which is a
  decision that player is already entitled to see.

**Tests.**

- `Cli.Tests` — a swap at a round boundary is accepted and the Match continues; a swap mid-Round is refused
  with a code; the pilot payload, **as serialized bytes**, carries no `CreatureSnapshot` and no unrevealed
  intent; "what would the bot do" for a seat that is not pending is refused.
- `Application.Tests` — a step carries `decidedBy`; a session with a swap stamps the composite agent string.
- `learning/` — pytest: a run with mixed `decidedBy` selects only the human steps, and a run without the field
  reads as it always did.
- `node --test` — the pilot module's transport, and the one-column fallback's breakpoint.

**What a reviewer checks.** A recorded session that was fast-forwarded and swapped must say so in three
places: the stamp, a `Seat` note, and every step's `decidedBy`. A trace whose seat changed hands and does not
say so is the one output this stage must not produce.

---

### Stage 7. The first session is worth reading

A stage that adds no feature. Its whole output is a session whose notes and trace are worth reading a week
later, and a written reading of it.

**What the first session must produce**, as a checklist run before and after:

| Before | Why |
| --- | --- |
| The rule set is the tabletop one, passed with `--rules`, and printed on the setup screen | The default is `RuleSet.Default` with a 30-Round cap (`src/DownfallArena.Domain/Matches/RuleSet.cs:20`), which is the simulator's number and not the table's |
| The setup line — engine, content hash, rule set — matches the line the print sheets carry (components.md:819-828) | A printed deck and an app that disagree is a playtest of neither |
| One dry run with `--p2 greedy` | It tests the app, not the game, and it costs one option |
| A fast-forwarded second run to round 10 | The tenth Round is the one that needs reading and the one nobody reaches by hand |
| Two people who have not read the rulebook, and a facilitator who does not answer a rules question before the player has looked it up | The rulebook of phase 4 is graded by the `Lookup` notes and nothing else |

| After | Why |
| --- | --- |
| A Match played to an outcome, not abandoned | An abandoned Match leaves a partial trace and no episodes |
| `steps.jsonl` with every seat's decisions and `decidedBy`, and `episodes.jsonl` with two lines | It is a cloning dataset only if it is complete and separable |
| A `Decision` note with `elapsedMs` for every step, and the wall clock of the whole Match | The engine measures Rounds and cannot measure minutes; phase 2's target is 15 to 30 minutes (plan.md:110-118) |
| Either at least one `Refused` note, or a sentence saying none happened | A refusal is *what the player expected the rules to be*. It is the most useful signal a playtest produces and the one no existing artifact had a field for |
| Every `Lookup` note, with the rule it was about | |
| The trace open in the viewer, read end to end by someone who was not at the table | If an absent person cannot read it, it is not a record |
| A written reading: how many Rounds, how many minutes, which Round stopped making sense, which three rules were looked up, and which decisions took longest | The four things the tabletop rule set is being tuned against, plus the list that justifies the rollout work |

**What makes a session unreadable, and is therefore checked for.** A stamp saying `RuleSet.Default`; a
`manifest.json` with zero counts because `FinishAsync` never ran; notes with no `elapsedMs`; a run directory
with no `catalogue.json`; a swapped seat that no artifact mentions; and a session recorded across a content
rebuild.

**Unblocks.** The answer to "is this teachable, is this 15 to 30 minutes, does the tenth Round still make
sense" — the reason the app exists — and the evidence that says whether the rollout and branch work of
stage 6's bottom three rows is worth doing.

---

### After the first session

Not stages. Each is a decision of its own, listed so the stages above do not quietly grow into them: the
replay engine and what it buys (simulation from here, replay and branch, the content A/B), two devices, undo
by replay, a session that survives a host restart, a push transport, richer resolution narration, rendering
`notes.jsonl` in the viewer, and the typed die below. playtest-app.md §7 gives the reason and the cost for the
ones it already named.

---

## Decisions this roadmap makes

### The die: the engine rolls, through `IRandomSource`

**v1 rolls in the engine.** The app prints the threshold (`d20: 14+`, stage 3) and whether it landed
(`CombatResolution.IsCritical`, stage 4). It does not ask a player to roll a physical d20 and type the face.

The case for the typed face is real and it is the strongest argument in this document's way: **the die is part
of what is being playtested.** A player reading `d20: 14+` off a card and watching a real die stop on 13 is
the experience the tabletop rule set is being tuned for, and a number the server chose is not that experience.

It still loses, for three reasons that are checkable in the tree:

1. **A typed roll is a session that no longer replays from its seed.** Every artifact rests on that:
   `RunStamp.BaseSeed` (`RunStamp.cs:24`), the benchmark digest, and `evaluate` replaying a seed set. A
   recorded session whose outcomes came from a person's hand is a dataset with an unrecorded input. It can be
   made reproducible again — record every face and replay from the face list instead of the seed — but that
   is a second reproducibility story, and it is not v1's. It is also the story the rollout work of stage 6
   would have to share.
2. **A typed roll is a number a player can mistype**, and there is no undo. A 4 typed as 14 is a critical that
   did not happen, applied to the board, recorded in the trace as a legal outcome, and invisible afterwards.
3. **The engine draws once per resolved cast, whatever the chance.** `ResolutionRules.cs:56` calls
   `NextDouble()` before comparing, and the fizzle paths return before it (`ResolutionRules.cs:36-54`).
   Sixteen of the 36 Spells never roll (d20-criticals.md), so a naive typed-die adapter would ask for a face
   on a cast where the die cannot change anything. The host would have to suppress those prompts itself —
   which is the host deciding when a roll matters, which is a rule, in the host.

**What it would cost when it is taken**, so the door stays open and the shape is known: an Infrastructure
adapter on the **existing** `IRandomSource` port, not a new port. `NextDouble()` blocks until the host hands
it a face `f` in 1..20 and returns `(20 - f) / 20`. That mapping reproduces the engine's comparison exactly:
with a chance `c` that is a whole number of twentieths, `(20 - f) / 20 < c` holds precisely when
`f >= 21 - 20c`, which is the threshold printed on the card. It is opt-in (`--die typed`), never the default,
and it lands with a `Roll` note kind so the session replays from its faces. `Match` holds one `IRandomSource`
(`src/DownfallArena.Domain/Matches/Match.cs:26,32`) and uses it in exactly one place (`Match.cs:275` into
`ResolutionRules.cs:56`), so the adapter intercepts the critical roll and nothing else — which makes it a
small change rather than a risky one.

**What the app does not do in either design: show the drawn face.** `CombatResolution` carries `IsCritical`
and not the value behind it (`CombatResolution.cs:43`). Showing a face would mean a Domain change with its own
ADR, or a client that draws its own number — the one thing this app exists to prevent.

### The content hash moving under an open session

`IGameResources` is resolved once, as a singleton, from `data/dst/game.schema.json`
(`src/DownfallArena.Infrastructure/InfrastructureServiceCollectionExtensions.cs:38-46`), and the loader
verifies the file's hash against its own content
(`src/DownfallArena.Infrastructure/Resources/GameSchemaBuilder.cs:84-87`). So:

- **A rebuild while a session is open changes nothing about that session.** The host keeps the catalogue it
  loaded. The Match keeps the hash it was created with (`src/DownfallArena.Domain/Matches/Match.cs:38,46`),
  and that hash is on every board the client fetches
  (`src/DownfallArena.Application/Matches/Projections/PlayerBoardState.cs:20`).
- **The host must not reload.** A Match whose rules changed mid-Round is a playtest of neither rule set.
- **The host says so.** On every `/api/session` answer it compares `IGameResources.Version` with
  `data/dst/game.schema.sha256` on disk
  (`src/DownfallArena.Infrastructure/Resources/GameSchemaJson.cs:13`) and, when they differ, prints one line:
  *this session is playing content X; the files on disk are now Y; restart to play Y.* A banner, not a
  refusal — the session in progress is still valid and still worth finishing.
- **A new session on a changed catalogue needs no app change.** That is stage 3's whole point, and stage 3's
  content-freedom test is what keeps it true.

### A recorded session whose content no longer exists

A trace and a dataset carry Spell **ids**, not card faces. Read a year later against a catalogue tuned twenty
times since, `spell:pummel:v1` is a string, and its cost, its effects and its Critical chance are gone — or
worse, silently different.

**The session directory carries its own catalogue.** `catalogue.json`, the card faces of stage 3, written once
at `StartAsync` (stage 5). One extra `WriteJsonAsync` on a port that exists, and the run directory becomes
self-contained: the hash says *which* content, the file says *what it was*. The viewer ignores it today
(`viewer/index.html:66-72`) and can read it later without the file having to be invented then.

Nothing is done for bot runs. They are regenerated from a seed and a hash; a human session cannot be.

---

## Where the architecture rules bite

| Stage | The temptation | The rule, and where it is enforced |
| --- | --- | --- |
| 1 | Put the 409 pre-check in the Cli, beside the route | It is knowledge about what the options offered, so it is Application's. The Cli calls it. `LayerDependencyTests.cs:51-60` does not catch this one — the reviewer does |
| 1 | Give `SeatAgent` its own copy of the decision sequencing | `MatchDriver` owns it, including ADR 0039's re-read (`MatchDriver.cs:71-87`). A seat implements four `Decide*` methods and nothing else |
| 2 | Bind `+` and be done | ADR 0023 kept the studio on loopback because its API writes content. This host may leave loopback only because it writes nothing but its own session directory, and only with a token on every route |
| 3 | Reuse the studio's `/api/catalogue` | It answers the authored content (`StudioApi.cs:197`), including what a build prunes (`DisabledContent.cs:24-25`). A new projection over `IGameResources` is the answer |
| 3 | Compute the threshold in `table.js` | The chance is the one the engine rolls against (`ResolutionRules.cs:56`). The projection computes it |
| 4 | Serve `MatchTraceRecorder`'s entries as they are | They carry both boards on purpose (`MatchTraceRecorder.cs:40-49`). The host filters, default-deny, with a test over every `IMatchEvent` |
| 5 | Write a session file with `File.WriteAllText` | `IArtifactWriter` exists and Infrastructure implements it |
| 6 | Widen an `internal` mutator so the pilot can fix a board | The line above. The pilot submits decisions; it does not write state |
| 6 | Let the pilot page hold both seats' boards "because the operator needs to see" | The operator is usually a player. `SeatVisibility` binds the pilot too |
| any | Add a package to make the page nicer | ADR 0024. `node --test`, no build step, injected transport |

No architecture test changes at any stage. Nothing new crosses a layer: the additions are two projections, one
record, one pure check, one field, one host and one static directory.

## What can be built in parallel, and what cannot

**Hard dependency:** stage 1 before every other stage. Nothing can be rendered, filtered, recorded or piloted
before there is a session to render, filter, record or pilot.

**Hard dependency:** stage 5 before stage 6's swap. Hot-swapping a seat before the record knows how to say so
produces a dataset that lies, which is worse than not having the capability.

**Parallel:** the Application halves of stages 3, 4 and 5 have no dependency on stage 1 at all. The catalogue
projection, `SeatVisibility` and `PlaytestNote` are pure Application types with Application tests, and all
three can be written while stage 1's host is being built. Their wiring and their client cannot.

**Parallel:** stage 2 is independent of stages 3 to 5 and can land at any point after stage 1. It is placed
second because every later reviewer check wants a real phone.

**Serial:** the client work of stages 1, 3, 4 and 6 is serial on one page shell — one module graph, one
stylesheet. Two people editing `table.js` in the same week is the one merge conflict this plan can foresee.
The pilot page of stage 6 is the exception: it is its own module and its own route.

## The open question that blocks a stage

**Where the rule set comes from** (components.md:907-913, question 6). `RuleSet.Default` is a static in the
Domain (`src/DownfallArena.Domain/Matches/RuleSet.cs:20`), pinned for every CLI command by another static
(`src/DownfallArena.Cli/GameSession.cs:30-31,37`). The content hash does not cover it. A session is
reproducible against a hash **and** a rule set, or it is not reproducible — and a printed deck and an app that
disagree about the Round cap is a playtest of neither.

**The stage that needs it answered is stage 7**, the first session played against printed components. Stages 1
to 6 do not need it.

**What the app does until then**, and none of it answers the question:

- `table --rules <file>` takes the five numbers and builds the rule set through `RuleSet.Create`
  (`RuleSet.cs:35-47`). A stopgap: whatever the answer turns out to be — a document under `data/`, a
  `studio --export` output, or five flags — replaces it.
- With no `--rules`, the setup screen says **in words** that it is playing `RuleSet.Default` with a 30-Round
  cap. It never defaults silently.
- Every artifact stamps both. `RunStamp` already carries `ContentHash` and a `RuleSetStamp`
  (`RunStamp.cs:14-16`), and the viewer's comparison already reports `rules` as its own axis
  (`RunStamp.cs:62`). Nothing is added; it only has to not be skipped.
- The setup screen prints the same one line the print sheets carry (components.md:819-828), in the same order,
  so checking that a deck and an app are the same game is one glance by a human — the only check available
  until the question is answered.

This document does not answer it. It belongs wherever components.md:907-913 is answered, with the generator,
because what both need is the same answer and not two.

## Working agreement per stage

1. Open the stage with the docs it makes stale in the same PR: `AGENTS.md`, `docs/learning/artifacts.md`,
   `docs/README.md` and the repository map for `table/`.
2. Write the tests from the specification, then the code. Red, green, refactor.
3. Run `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes`, and `node --test table/*.test.js`.
   When `learning/` changes (stage 6 only), also run its ruff check, ruff format check and pytest.
4. Open the players' page on a phone before opening it on a desktop.
5. Ask `code-reviewer` for a pass on the diff, and `domain-reviewer` on anything that touches Domain — which
   should be nothing.
6. One PR per stage, two for stages 1, 4 and 6. The PR body lists what was added, what was reused, and what
   was deliberately left for a later stage.
