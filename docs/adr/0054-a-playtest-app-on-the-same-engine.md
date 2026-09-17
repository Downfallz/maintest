# 0054. A playtest app: the tabletop rule set on a screen, through the same engine

Date: 2026-09-17
Status: Accepted

## Context

The tabletop translation needs playtests, and a playtest needs a table. Phases 1 to 4 produced an audit, a
component manifest and a rulebook, all of which describe the engine's own rules; what is missing is a way for
two people to play them and be measured doing it. The Application layer already exposes exactly what a player
sees and may decide — `PlayerBoardStateProjection` builds a board per slot and `PlayerOptionsProjection`
returns the one legal decision of the current sub-phase, built from the domain's own gates. The Cli already
hosts HTTP for the content studio ([ADR 0015](0015-content-studio.md)) and already runs a human at the console
through `IPlayerAgent` (the `human` command). The engine already writes a trace and a dataset that the viewer
and the Python side read ([ADR 0013](0013-learning-stack.md)). The temptation is to write a game client; the
risk is that a game client implements a rule.

## Decision

We will build a **playtest app** as a new Cli command, `table`: an HTTP host, bound to the loopback address by
default, serving a static page in `table/`, in the shape of the studio and the viewer — no framework, no build
step, an injected transport, tested with `node --test`
([ADR 0024](0024-test-the-studio-page-with-nodes-own-runner.md)). The host plays the match through
`MatchDriver`, with each seat as an `IPlayerAgent` that blocks until a player taps, so the driver, the gates
and the aggregate are the only things that decide anything; the client renders `PlayerOptions` and submits one
decision at a time. **The hidden-information boundary is the server's**: a request carries one seat token and
is answered with that seat's projection only, the event feed is filtered by a default-deny projection over the
closed set of match events, and the trace — which carries both boards on purpose — is served only after the
outcome. **Hotseat first**, on a per-seat API so two devices is a later transport change. A session is
recorded by the existing `RunRecorder` into `runs/playtest/<id>/`, which is the bot run's directory exactly,
plus one new file, `notes.jsonl`, holding what no artifact carries today: how long a decision took, a refusal
a human provoked, a misplay, and a rule someone had to look up. No new port is declared: `IMatchRepository`,
`IArtifactWriter`, `IRandomSource` and `TimeProvider` answer everything. Three things are added to Application
— a card projection over the built catalogue, a seat's view of the event stream, and the note record — and
none of them is a rule.

## Consequences

- Good: there is one implementation of every rule. A client that only renders what the Application computed
  cannot disagree with the engine, and a rule that feels wrong at the table becomes an engine ADR, which is
  what the plan's decision C requires.
- Good: a human playtest lands in `viewer/` beside the bot runs and in the learning pipeline beside the
  recorded datasets, with no new reader and no change to the viewer. Human decisions become a cloning dataset
  and a measuring stick for the heuristic weights, on boards a bot never chose.
- Good: the hidden-information boundary is enforced where it can be enforced. A client-side hide would have
  let a player read the opponent's six face-down intents and six Speed tokens — the only two hidden decisions
  in the game — and would have left a trace full of legal moves, so the contaminated playtest would be
  undetectable afterwards.
- Good: reuse is high and new code is low. The static-page-plus-host shape, the route table, the same-origin
  fence, the trace format, the dataset writer, the viewer page and the stamp all exist.
- Bad: a seat blocks a thread while a human thinks. This is what `ConsoleAgent` already does, and one session
  has two seats, but it is sync-over-async and it is the reason this host is one session per process.
- Bad: a fifth host surface to keep working. `table/`'s modules join the studio's under `node --test`, and its
  coverage does not reach SonarCloud either ([ADR 0024](0024-test-the-studio-page-with-nodes-own-runner.md)).
- Bad: playing on a phone means the host leaves the loopback address. This host may do what
  [ADR 0023](0023-a-hosted-studio-with-github-as-its-backend.md) refused for the studio only because its API
  writes no content, deletes nothing and takes no path from a request; seat tokens are required on every route
  and the trade is accepted rather than argued away.
- Bad: hotseat's hidden information rests on two people passing a device. The server never sends both seats at
  once, but both tokens live in one browser. That is the cardboard's own fence, and it is weaker than the
  token boundary two devices would give.
- Neutral: a session does not survive a host restart; the checkpointed trace is what remains.
- Neutral: a rule set is still not content. The app stamps the content hash and the rule set on every artifact
  and prints both on its setup screen, which makes a session reproducible against the pair without deciding
  where a rule set lives.

## Alternatives considered

- **A client that drives the command handlers directly, with no driver and no agent.** Naturally
  request/response, and it loses the dataset: `steps.jsonl` is written by `RecordingAgent`, which wraps an
  agent. It would also reimplement the driver's sequencing, including the board re-read between two intents
  that [ADR 0039](0039-the-bot-binds-its-targets-on-a-board-that-has-not-happened-yet.md) had to fix once
  already.
- **A panel in the content studio.** One less host, and it mounts a player-facing page on an API that writes
  and deletes content files and reads a path from the request — the API
  [ADR 0023](0023-a-hosted-studio-with-github-as-its-backend.md) deliberately kept on the loopback address. It
  is the one combination that must not exist.
- **A native or cross-platform app.** A framework, a toolchain, a store and a build step, to show a JSON
  document on a phone screen. The viewer and the studio already proved a static page is the right weight.
- **Server-sent events or WebSockets.** Nothing to push in hotseat, a one-second poll is enough for two
  devices, and each is one more thing to keep alive across a screen lock.
- **Reusing the studio's `/api/catalogue` for the card faces.** It answers the *authored* content, including
  items a build prunes. The app must show the catalogue the match is playing.
- **Recording nothing, and taking notes on paper.** Honest, and it throws away the reason the engine writes
  artifacts at all: the traces line up, the datasets train, and paper does neither.

## Follow-up

- Application: the card projection, the seat's event-visibility projection with a test over every
  `IMatchEvent` in the Domain assembly, and the note record.
- Cli: the `table` command, its host and its route table; the seat agent; the decision pre-check that turns a
  refusal into a `409` and a note instead of an exception.
- `table/`: the page, its modules and `table/*.test.js`.
- `AGENTS.md`: the command, and `node --test table/*.test.js` in the gate beside the studio's.
- `.claude/skills/verify/SKILL.md`: the same.
- `docs/learning/artifacts.md`: `notes.jsonl`, and `runs/playtest/<id>/` as a run directory.
- `docs/tabletop/playtest-app.md`: the specification this record was written in, kept as the long form.
- `docs/README.md` and the repository map: `table/`.
- Architecture tests are unchanged: nothing new crosses a layer.

## Related

- [ADR 0015](0015-content-studio.md) and [ADR 0023](0023-a-hosted-studio-with-github-as-its-backend.md): the
  other host of this CLI, and the reasoning this one is held against.
- [ADR 0024](0024-test-the-studio-page-with-nodes-own-runner.md): how a static page of this repository is
  tested.
- `docs/tabletop/playtest-app.md`: the specification, which this record summarizes.
- `docs/tabletop/app-roadmap.md`: the order it is built in.
