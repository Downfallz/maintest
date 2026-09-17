# 0055. An Android head that embeds the engine

Date: 2026-09-17
Status: Accepted

## Context

[ADR 0054](0054-a-playtest-app-on-the-same-engine.md) gave the table a host and a page, and rejected "a native
or cross-platform app" as "a framework, a toolchain, a store and a build step, to show a JSON document on a
phone screen". That rejection weighed a **thin client** against a server it talks to, and against that it was
right. It did not weigh the case the maintainer actually has: **no laptop at all**. `table` is a live engine —
`MatchDriver` advancing a `Match` with a seat blocked on a tap (`src/DownfallArena.Cli/Table/TableHost.cs`,
`HumanSeat`) — so it cannot be published as a static page with files the way the studio is
([ADR 0023](0023-a-hosted-studio-with-github-as-its-backend.md)): no file set answers `/api/seat/{slot}`.
Playing the tabletop rule set on a phone therefore requires a machine on the same network, an address typed
with `--bind`, a URL and a seat code. With no machine, there is no session. That is the gap, and nothing in
the tree closes it. The second force is the one 0054 leaned on hardest: an embedded app traps a run directory
in Android's app-private storage. The maintainer removes it by removing the reason — **this head records
nothing**. It is for playing the rules on the phone, not for collecting a playtest.

## Decision

We will build an **Android head that embeds the engine**: one `Activity` holding one `WebView` that shows the
existing `table/` page against an **in-process** match, hotseat only — both seats on one phone, passed between
two people, which is what a phone at a table is anyway (`docs/tabletop/plan.md`, decision D). It is a project
of its own, `android/`, targeting `net10.0-android`, **outside `DownfallArena.slnx`**: a project with that
target framework in the solution would break `dotnet build` for anyone without the android workload, the agent
included. It references `src/DownfallArena.Cli` and Cli grants it `InternalsVisibleTo`, so the seat API, the
route table and the token boundary are the *same code* rather than a copy — a second `TableApi` would be a
second implementation of the hidden-information boundary, which is the one thing this effort forbids.

**Content comes from assets through a file.** `AddGameResources(services, schemaPath)`
(`src/DownfallArena.Infrastructure/InfrastructureServiceCollectionExtensions.cs`) calls
`GameSchemaBuilder.Load(schemaPath)`, which takes a **path**; an Android asset is not a file. So the head
copies its embedded `game.schema.json` and its rule set JSON into the app's `FilesDir` on first launch and
passes that path. **No change to Infrastructure and none to Application.**

**Transport: the existing host in process first.** The head starts `TableServer` on the loopback address and
navigates the `WebView` to `http://127.0.0.1:<port>/?player1=…&player2=…`, which is the link `TableHost`
already prints for two people sharing one browser. That reuses the page, `TableApi`, the route table and the
seat tokens unchanged and costs almost no new code. `System.Net.HttpListener`
(`src/DownfallArena.Cli/Hosting/HttpHost.cs`) **is** available there, which is what makes this the cheap
option rather than the hopeful one: in the `net10.0` Android runtime pack, `System.Net.HttpListener.dll`
carries the same managed implementation as the Linux build — the IL of `.ctor`, `Start` and `GetContext` is
byte for byte identical, and `get_IsSupported` compiles to `ldc.i4.1; ret`, a hardcoded true. That was read
off the runtime pack, not run on a device, which is as far as this environment reaches; it is enough to choose
a transport and not enough to call the head working. The fallback stays on record for the case the device
contradicts the reading: **a bridged transport**, a second factory beside `httpTransport` in
`table/transport.js`, answered in C#. The seam is ready —
`transport.js` is one factory exposing exactly `seat(since)`, `session()`, `catalogue()` and
`decide(decision)`, its own comment says "nothing else in the page knows that fetch exists", and `table.js`
builds one per seat on one line. **A bridge, if built, must be asynchronous**: a request id plus a callback
through `EvaluateJavascript`, never a synchronous `AddJavascriptInterface` call. A synchronous call blocks the
WebView's JS thread, and a seat's `decide` blocks until the engine asks that seat again; in hotseat the other
seat needs that same thread in order to act, so the two would wait on each other. That is a deadlock, not a
slow screen, and it is the non-obvious part of this record.

**The first build is a probe, not a head.** Does the engine start on the device, does the content load from
the copied assets, does the page render one Round. It exists to close the two risks that cannot be tested
before an APK exists.

**It records nothing.** No `runs/playtest/<id>/`, no `notes.jsonl`, no dataset, no trace file, nothing to
bring back, nothing leaving the phone. The recorded session ADR 0054 decided stays on the page host, where a
directory can reach `viewer/` and the learning pipeline. The in-memory `MatchTraceRecorder` still runs, because
`SeatFeedProjection` builds a seat's feed from its entries; it writes no file.

**Architecture rule 4 is amended in the same PR, and the test is not.** Rule 4 in `AGENTS.md` names Cli as the
only place concrete adapters are wired, because Cli was the only head; this head calls `AddApplication`,
`AddInfrastructure` and `AddGameResources` itself, since the schema path is the phone's `FilesDir` and the
`Activity` lifecycle is the host, not `Program`. So the rule is reworded to bind *heads* rather than to name
one, and `docs/architecture/overview.md` already anticipates it ("Hosts: DownfallArena.Cli (later: Api, Ui)").
`tests/DownfallArena.Architecture.Tests/LayerDependencyTests.cs` needs no change: it enforces the *direction*
of dependencies between five assemblies it loads by reference, and says nothing about where adapters are
wired. It also cannot load an assembly outside the solution — which is part of the answer rather than a gap in
it: the head's layering is held by review, and the rule it is held against has to say so out loud.

## Consequences

- Good: it answers the case nothing else answers. No laptop, no server to start, no LAN address, no URL and no
  seat code typed on a phone keyboard. An icon and a tap.
- Good: there is still one implementation of every rule. The same engine assemblies, the same `table/` files,
  the same seat API and the same token boundary — a phone build cannot disagree with the engine about a rule
  because it contains no rule.
- Good: nothing is on a network. In the in-process case the host binds the loopback address of the phone
  itself, so no seat token crosses a link and the trade ADR 0054 accepted when it left loopback is not taken
  again here.
- Good: hotseat needs no new screen. The pass-the-device screen is already first class on the page, and it is
  the same fence as the cardboard's.
- Bad: **the agent can neither build nor run it.** `dl.google.com` answers `403 Forbidden` through the egress
  proxy, so there is no Android SDK in this environment: no local build, no emulator, no verification of any
  kind. GitHub Actions' `ubuntu-latest` has the SDK, so the only feedback loop is: CI builds an APK, the
  maintainer installs it, the maintainer reports. In a repository whose convention is to verify before
  declaring anything done, this head is the one piece that ships unverified by whoever wrote it, and saying so
  every time is the price.
- Bad: out of the solution is out of every gate. `dotnet build`, `dotnet test`, the format check and the
  architecture tests never see `android/`. It has its own project file and its own workflow, and nothing else
  covers it.
- Bad: a new toolchain, which is exactly what 0054 refused. A workload, an SDK, a manifest, a signing config,
  and a WebView whose behaviour depends on the device's WebView version rather than on anything in this tree.
- Bad: one unknown stays open until an APK exists: whether the device's WebView loads the page's ES modules
  over `http://127.0.0.1` the way a desktop browser does. The probe exists for it.
- Bad: if the bridge is needed there are two transports to keep working. `node --test` can hold the new factory
  against a stub exactly as `transport.test.js` holds `httpTransport`, and the C# half is testable on a phone
  only.
- Bad: sideloading. A debug-signed APK means allowing unknown sources for the installer, no store review, no
  update channel, a download and a tap for every reinstall, and no upgrade path to a release key later.
- Neutral: distribution is not decided. A CI artifact is a login-gated ZIP, which is awkward to fetch on a
  phone; a GitHub Release asset is a direct link. Publishing a release has not been approved, so the artifact
  is what exists and the question is left open.
- Neutral: iOS is out of reach, and not for a reason this repository can fix: building and signing it needs a
  Mac, and installing it on a device needs a paid Apple developer account. Android is the maintainer's phone
  and the only one where a build nobody published installs.
- Neutral: this is a parallel branch, not a roadmap stage. `docs/tabletop/app-roadmap.md` from stage 5 onward
  is the *recorded* session on the hosted page; this head records nothing, so the roadmap continues on the page
  and this head inherits whatever the page gains.
- Neutral: the head owns no rule set. It embeds the same JSON file `table --rules` reads and prints the same
  setup line, so `components.md` question 6 is as open as it was.

## Alternatives considered

- **A thin native client against a LAN host.** What ADR 0054 rejected, and the rejection stands: it needs the
  laptop, which is the thing that is missing.
- **Publishing `table/` as a static page, the way ADR 0023 publishes the studio.** There is no file set that
  answers a seat route. The page talks to a match that is being played, not to a document.
- **Opening the page from the phone's own storage.** A `file://` origin refuses ES modules, and there would be
  no engine behind it in any case.
- **The engine in the page — a JavaScript port, or Blazor WebAssembly.** A port is a second implementation of
  every rule, the one thing forbidden; WebAssembly is a second framework and a build step, and it still needs
  something to serve the page to the phone.
- **A cross-platform UI toolkit drawing the screens natively (MAUI, Avalonia).** A second client for a page
  that exists and is already designed at phone width, and a second place a rule could leak into.
- **Keeping the head inside `DownfallArena.slnx`.** One fewer project file, and `dotnet build` then needs the
  android workload for everyone, the agent included. The head's isolation is what keeps the gate runnable.
- **Reimplementing the seat API in the head instead of reaching Cli's internals.** No `InternalsVisibleTo`, and
  a second copy of the default-deny visibility filter, in the one place nobody can test.
- **A synchronous JS bridge.** Fewer moving parts and it deadlocks hotseat, as above.
- **Recording into app-private storage and exporting through the share sheet.** It brings back ADR 0054's
  artifact story on a head that exists for fun, and it puts a second copy of the artifact conventions where no
  test reaches them.

## Follow-up

- `android/`: the project file, the `Activity`, the asset copy into `FilesDir`, the `WebView`, and the probe.
- `src/DownfallArena.Cli/DownfallArena.Cli.csproj`: `InternalsVisibleTo` for the head.
- `table/transport.js`: the second factory and its `node --test` — **not expected**, since the runtime pack
  says `HttpListener` is there; only if a device contradicts that reading.
- `AGENTS.md`: rule 4 bound to heads rather than to Cli, `android/` in the repository map, and the plain
  statement that it is outside the solution and outside the gate.
- `docs/architecture/overview.md`: the head beside Cli in the Hosts row.
- `.github/workflows/`: a workflow that builds the APK on `ubuntu-latest`, not part of the pull request gate.
- `docs/tabletop/app-roadmap.md`: one line placing this head as a parallel branch that records nothing.
- `tests/DownfallArena.Architecture.Tests`: unchanged, for the reason given in the decision.

## Related

- [ADR 0054](0054-a-playtest-app-on-the-same-engine.md): the app this is a second head of, and the record whose
  rejection of a native app this one narrows.
- [ADR 0023](0023-a-hosted-studio-with-github-as-its-backend.md): why a live engine cannot be published as
  files the way the studio can.
- [ADR 0024](0024-test-the-studio-page-with-nodes-own-runner.md): how the page and a new transport factory are
  tested.
- [ADR 0009](0009-game-data-pipeline.md): the consolidated schema file the head ships as an asset.
