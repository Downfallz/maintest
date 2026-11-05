# Phaser arena presentations

Two Phaser pages: a viewport-bound visual experiment at `/arena-lab`, isolated from the actual `/play` client,
and `/arena`, the opt-in complete game loop for one authenticated seat (ADRs 0125–0128).
Pinned Phaser 4.2.1 + TypeScript, bundled locally with esbuild. [ADR 0123](../docs/adr/0123-prototype-the-arena-as-an-isolated-presentation.md)
records the choice; the [delivery plan](../docs/tabletop/browser-arena-plan.md) covers live integration.

## Build and run

From the repository root (Node 22 and .NET 10):

```bash
npm --prefix arena ci --ignore-scripts
npm --prefix arena run build
dotnet run --project src/DownfallArena.Cli -- table --lobby
```

Open the printed host URL at `/arena-lab`. For a static preview after building:
`python3 -m http.server 8765`, then `/table/arena-lab.html` on that server.
Rebuild after changing TypeScript or the imported shared table modules; there is no watch server.

`build` type-checks and emits `table/arena-build/game.js` and `live.js`, legal comments and the Phaser MIT license.
Output is ignored, never committed. CI builds before route/browser tests. Docker uses a Node builder
stage and copies only the bundle/notices into the .NET runtime alongside the local WebP assets.
No CDN, remote fonts or browser package downloads are needed at runtime.

## Boundaries

- `src/main.ts`: accessible DOM controller, input, dialogs, lifecycle and optional synthesized sound.
- `src/scene.ts`: Phaser scene, local assets, creature figures, cancellable tweens and camera effects.
- `src/types.ts`: presentation types only.
- `src/spell-effects.ts`: bounded, cancellable vector motifs for confirmed spell outcomes.
- `data/presentation/spell-art.json`: each spell's visual family, motif and delivery, published by the builder
  and served on the card; `table/arena-playback.js` reads it off the card, with neutral and fizzle fallbacks.
- `table/arena-art.js`: audited sprite manifest, the body and accents a creature's acquired packages give it (read off the
  catalogue's packages, authored in `data/presentation/mutation-art.json`), and mirrored geometry.
- `table/arena-lab-state.js`: the deterministic sample state machine, which carries the one action it offers,
  and the cancellation generation.
- `table/arena-layout.js`: the portrait/landscape geometry, shared by the Phaser figures and the HTML hit
  targets so the two cannot drift.
- `table/arena-lab-fixture.js`: fictional before/after frames. Never import this into a live adapter.

The `table/` modules are plain JavaScript so `node --test table/*.test.js` covers them without a build:
the TypeScript imports them and owns Phaser and DOM integration.

No rules, legality, damage or random resolution are reimplemented here. `/arena-lab` keeps its fixture
and makes no API requests. `/arena` is the opt-in live entry point: choose an accepted seat, inspect its
board, and answer combat questions. Return to `/play` for planning and the rest of the game loop. It shares [`table/presentation.js`](../table/presentation.js)
with the table. [ADR 0125](../docs/adr/0125-read-one-authenticated-seat-in-the-live-arena.md) describes
seat discovery, explicit viewpoints, read-only transport, arrival behavior and recovery.

- `src/live.ts`: live DOM adapter and lifecycle; no lab imports.
- `table/arena-live.js`: snapshot adapter and fenced polling/SSE connection.
- `table/arena-playback.js`: bounded confirmed-action queue; pure historical frames, motifs and cancellation.
- `table/arena-history.js`: public recent-event journal, independent of playback; `src/history.ts` renders its accessible drawer.
- `table/arena-seats.js`: read-only capability discovery and return links.
- `table/arena-layout.js`: shared geometry for 1v1, 2v2 and 3v3.

Opening combat controls acknowledges the visible asking. Explicit confirmation sends Action, Intent or
Target through the existing transport; inspection alone remains read-only. `table/arena-combat.js`
fences drafts and pending commands, while `src/combat.ts` draws their accessible controls.
[ADR 0126](../docs/adr/0126-answer-combat-questions-in-the-live-arena.md) records this extension.
Invitation acceptance stays at `/play`.
401/403 clears private content; a transient failure labels the last snapshot stale. A hidden page
stops and clears its display, then reconnects from a new snapshot. The host's existing reads mark an
accepted seat as arrived, which may start a waiting match.
The compiled bundle is about 1.2 MB uncompressed. Four creature WebPs total 1,212,726 bytes,
plus the eclipse background; the old three-frame sheet is no longer loaded. Device performance and transfer compression remain to be measured before release.

## Verify

```bash
node --test studio/*.test.js table/*.test.js
npm --prefix studio/browser ci --ignore-scripts
npm --prefix studio/browser exec -- playwright install chromium
npm --prefix studio/browser test -- arena-lab.spec.js arena-live.spec.js arena-combat.spec.js arena-playback.spec.js arena-loop.spec.js arena-history.spec.js arena-guidance.spec.js arena-upkeep.spec.js arena-mutations.spec.js arena-tempo.spec.js
dotnet build
dotnet test --no-build
dotnet format --verify-no-changes
```

The browser suite checks actual Phaser startup, both orientations, 320px width, 44px target bounds,
no document scrolling, rotation, all three outcomes, cancellation/reset, keyboard/skip, reduced motion,
2× settings, missing-art fallback and no API requests. Route tests verify WebP binary handling and the
packaged bundle. Screenshots in the delivery plan come from the actual .NET host.

The lab remains one authored action. The live scene plays the host's confirmed before/after frames in
sequence, including damage, recovery, energy, conditions, fizzles and defeat. Skip, opening combat controls,
resize and connection interruption return immediately to the current snapshot. First loads and hidden-tab
returns never replay history. Legacy events, and one read bringing more than twelve actions, use confirmed
text on the latest board. Result details remain available in an accessible overlay. Both pages use four audited forms with uniform scaling and explicit ground anchors ([ART-3](../docs/art/runtime-sprites.md)). Real-device Safari,
Android GPU/performance, screen readers and high zoom remain future acceptance.

A7 completes planning and the match result in the scene shell ([ADR 0128](../docs/adr/0128-complete-the-game-loop-in-the-arena.md)).
`table/arena-planning.js` fences mutation, tempo and tie drafts; `src/planning.ts` renders offered mutation
cards with explicit confirmation and a separate reference tree (AU4). AU5 adds editable team tempo tabs and one
confirmation over the host's sequential questions, with simultaneous reveal still owned by the host (ADR 0135).
`table/arena-match.js` and `src/match.ts` provide two-round upkeep/aftermath review,
concession, the host outcome and a manual whole-match replay. Replay steps distinguish recorded action frames,
labelled reference frames and missing battlefield snapshots. `/arena?replay=<session>&seat=player2` opens a
finished recording using the same operator authorization as `/play?replay=...`.

`node node_modules/playwright/cli.js test --config table.config.js arena.loop.js` (from `studio/browser`)
plays complete 3v3 games in both declarations, reads recorded sessions, and checks two independent seats'
simultaneous choices and concession against the real .NET engine.

## AU1: recent match history

The **History** button previews the latest public event and opens a chronological journal of the current
and previous round (up to 200 events). Expand an attack to read its caster, spell, targets, critical/fizzle,
applied effects, dropped targets and recorded before/after values. Upkeep ticks, public mutation reveals,
intent reveals, the turn order with each slot's tempo, skipped turns, aftermath and the outcome share the
same list; a round or phase beginning is a marker read in place. Creatures are named the way the scene names
them (the id only tells namesakes apart) and the outcome is read for this seat, although the feed names slots
the way the engine does. Unknown event payloads and unrevealed choices are never rendered; a generic notice
identifies unsupported event types. Missing action frames are labelled rather than inferred from the current board.

History is available from the header, planning/combat choices and action result. Opening it settles
playback without sending a decision or acknowledging a question. Command drafts and focus return on close;
expanded entries and reading position survive polling. New events follow the bottom only when the reader
is already there; otherwise **Latest** announces their count. First loads populate history without replaying
animations. Switching viewpoint, hidden tabs, lost access and renderer failure clear every journal surface.

This implements AU1 of the [UX parity plan](../docs/tabletop/arena-ux-parity-plan.md).
Automatic upkeep is presented by AU3b below; older-round loading, filtering and
links into replay belong to AU6. The existing upkeep review remains available. This slice adds no domain
rules, transport contract or storage. Browser tests cover 320px portrait and short landscape, focus/drafts,
read-only access, deduplication, scroll retention and clearing on access loss. The real-host complete-match
journeys compare journal action identities against actual seat responses in both declaration modes.

AU1 captures from the real .NET host (seed 7, 3v3, at-activation; provisional artwork):
[desktop history](../docs/tabletop/images/arena-history/desktop.jpg) ·
[phone history](../docs/tabletop/images/arena-history/phone.jpg).

[ART-4](../docs/art/spell-vfx.md) supplies 44 spell profiles for confirmed-action playback, with distinct
physical, biological, elemental, necromantic and astral shapes. It preserves queue timing and accessible
outcomes; reduced motion bypasses all VFX. The browser playback suite also boots the production scene
in an isolated test harness to verify actual vector objects and their cleanup.

## AU2: objectives and contextual help

The command bar names the current objective: shared mutation picks, the creature choosing its tempo,
a tied group, an on-turn spell, a hidden intent, or targets for an already revealed spell. Waiting rooms,
opponent turns, sent choices, lost connections and recorded playback have separate instructions.
The **Help** button opens the current explanation and a four-phase round guide. The next mutation
opportunity comes from `board.nextEvolutionRound`; the guide follows the catalogue's declaration mode, where
a stamp that names none plays hidden intents (ADR 0110), and names creatures the way the scene does.

Each instructional subject opens automatically once, after playback and other dialogs have settled.
**Do not show phase tips again** mutes future automatic tips; Help remains available from the timeline,
planning/combat choices and settings. Generic seen-subject/mute preferences use
`downfall.arena.help.v1`; no seat, token, match, question or choice is persisted. Denied storage falls
back to the current page's memory. Opening Help preserves drafts, restores focus on close and never
acknowledges a question or sends a command. Access loss clears its text.

`table/arena-guidance.js` derives this read-only model; `src/guidance.ts` owns the native dialog.
The UI's pass label now says **Pass remaining picks**, matching the engine's existing whole-round pass.
The AU3b receipt precedes first-use tips; mutation cards are covered by AU4 below and team tempos by AU5.

`arena-guidance.spec.js` covers default-on first-use behavior, persistence, mute/reopen, pending commands,
waiting/access loss, playback boundaries, both declaration modes, short orientations and enlarged text.
`arena.loop.js` completes real-host matches in both modes with tips enabled. Other focused arena journeys
set an explicit returning-player preference so they continue exercising their original controls.

Real-host captures: [desktop help](../docs/tabletop/images/arena-guidance/desktop.jpg) ·
[phone help](../docs/tabletop/images/arena-guidance/phone.jpg).

ART-5 adds [mutation cues](../docs/art/mutation-cues.md): one dominant body selected from the displayed
acquired tiers, two branch slots and an owner-only capstone accent. `mutation-cues.ts` follows the figure
transform, retains static cues under reduced motion, and suppresses them if the requested sprite is missing.
The live suite checks all nine branch pairs and lifecycle behavior with the production scene.

ART-6 adds [playback polish](../docs/art/arena-polish.md): shared timings, live 1×/1.5×/2× playback and
opt-in synthesized sound. `audio.ts` owns one short voice, cancels pending resumes, and is shared with the
lab. Reduced motion keeps a 650 ms readable result at every speed. The playback suite exercises the
production audio owner and instrumented browser audio alongside the renderer.

## AU3a: recorded ordinary energy

New matches include **Upkeep · round energy** in History, with each recipient's actual gain, before/after
energy and base/passive request amounts. Zero gains are visible. These facts are captured before ongoing
energy, healing and bleed; a creature killed afterward retains its earlier receipt. Journal reads remain
read-only and deduplicated. Old recordings and resumed legacy matches have no ordinary-energy receipt;
the UI does not invent one from the default allowance. See [ADR 0132](../docs/adr/0132-record-ordinary-round-energy-without-renumbering-old-tables.md).

## AU3b: automatic upkeep receipts

Once human arrivals clear, the arena presents one grouped **Round 1 · Upkeep** receipt. Each observed
round transition gets the same receipt, after confirmed-action playback finishes and before a phase tip.
An open reference or command draft delays it; Continue and Escape only dismiss presentation. Reading it
never acknowledges an asking or submits a choice. A first visit to an advanced match does not replay old
round announcements; the next observed round does. Catch-up keeps the latest round, not a queue of old popups.

`table/arena-upkeep.js` combines only public ordinary-energy and ongoing-effect events by their own
round IDs. Ordinary gains show before/after energy and base/passive requests, including zero. Ongoing
energy, healing (including capped zero healing), then damage follow the engine's order. The receipt waits
for the ongoing event so it does not announce half an upkeep. Missing ordinary energy in a legacy feed
is explicitly unknown. Neither catalogue defaults nor current boards supply historical totals. No-tick
rounds still show their ordinary grants. Expired conditions are not reconstructed as current effects.

Creature links open the existing **current** inspection, including present condition durations; this is
labelled separately from the historical receipt. History and round review retain the facts after dismissal,
and replay uses the same tick extraction and wording, preserving zero results and legacy missing-data text.
AU7 still owns paced aftermath/upkeep choreography.

`src/upkeep.ts` renders a native modal with a scrollable body and reachable Continue button. Polls preserve
nodes and focus. Viewpoint changes, hidden tabs, access loss and renderer failure erase the receipt and
inspection. `sessionStorage` key `downfall.arena.upkeep.seen.v1` keeps at most 32 table/seat round high-water
marks, never tokens, questions, choices or event facts. These prevent duplicate announcements on reconnect
and reload in the same tab; blocked storage falls back to memory. A new browser tab can announce its first
round once. Marks record presentation, not a host acknowledgement.

`arena-upkeep.spec.js` covers arrivals, recorded amounts, zero healing, inspection, History, deferred drafts,
closing-action boundaries, legacy data, reconnect/reload, access loss, renderer interruption, 320px portrait,
short landscape and 200% text. Real-host journeys in both declaration modes read every new-round receipt
before answering and still verify that presentation sends no command or acknowledgement.

Captures: [real-host opening receipt, desktop](../docs/tabletop/images/arena-upkeep/desktop.jpg) ·
[phone receipt with non-default/passive/zero fixture values](../docs/tabletop/images/arena-upkeep/phone-fixture.jpg).

## AU4: compare offered mutations

`Choose mutation` opens the host's available cards for the selected creature, with three columns on
desktop and stacked cards on phones. Cards show family colour and name, stage, authored tags,
initiative gain and spell names/previews. Each separate Details disclosure includes spell cost,
targeting, effects and caster/critical text. Passive-only and shared capstones retain the catalogue's
passive lines without invented tags or spells. Later offers may contain any number of upgrades and
multiclass roots; missing catalogue records remain selectable by host id with explicit unknown details.

Creature tabs and the confirmation footer stay visible while cards scroll. The budget is capped by
the number of creatures with host offers; defeated and already-picked creatures explain their state.
Selecting a card changes only the local draft. Acquire names its recipient and mutation and spends
one team pick; changing creature clears that draft. Pass explicitly gives up all remaining team picks.
The full Mutation Tree is a separate, read-only disclosure during Evolution; its persistent access
outside this phase remains AU8. Same-question polls preserve disclosures, focus and scroll.

`table/arena-mutations.js` projects this model using the existing card and mutation helpers;
`src/mutations.ts` renders it. The existing planning controller still owns acknowledgement,
host legality checks and duplicate/stale-command protection. `arena-mutations.spec.js` covers
comparison, late offers, recipient changes, passive capstones, missing content, effective budgets,
poll stability, pending acknowledgement and small/landscape layouts. `arena.loop.js` exercises the
chooser in complete real-host matches, including two-seat private choices.

Real-host opening cards: [desktop](../docs/tabletop/images/arena-mutations/desktop.jpg) ·
[phone](../docs/tabletop/images/arena-mutations/phone.jpg).

## AU5: team tempo drafts

**Team tempos** opens a tab for every remaining eligible creature. Choose Instinct or Precision for each,
return to edit any tab, then **Confirm tempos** once. The compact spell reference shows cost and Precision
critical chance; current-creature guidance and catalogue-only references are explicitly distinguished.
The footer stays reachable while the choices and references scroll.

`table/arena-tempo.js` owns the draft and frozen batch; `src/tempo.ts` owns the tabs and references. The first
acknowledged creature's timing includes the team's draft time. Each later asking is acknowledged before its
write. A refusal, uncertainty or interruption stops automatic progress; accepted choices remain visible and
continuation requires another explicit confirmation after a fresh seat read. Hidden pages, seat changes and
reloads discard local drafts. No opponent choices or public history are altered before the host reveal.

`arena-tempo.test.js` exercises protocol interleavings; `arena-tempo.spec.js` covers actual controls,
references, errors, keyboard navigation, mobile layout and screenshots. `arena.loop.js` verifies full games
in both declarations and opponent privacy across a partial batch, reload and completion on the real host.
See [ADR 0135](../docs/adr/0135-draft-team-tempos-before-sequential-confirmation.md).


## AU11: turn order and recorded dice

**Order** opens a read-only reference from the compact activation strip; **Inspect turn order** is also
available above a tie draft. It shows every host position, tempo band, initiative and recorded d20 chain,
with explicit team labels and Completed / Now / Upcoming states. Defeated and stunned creatures stay in
their slots with their status. Creature links open the existing inspection. The strip and reference use
the presented board, so recorded replay retains its own round, order, rolls and cursor. Opening the
reference during live action playback explicitly settles that queue, like History. Missing initiatives
or rolls remain unrecorded; no local sort, simulated die or current-stat fallback supplies them.

A newly observed public TimelineBuilt event queues one reveal summary. It waits for both teams' tie
ordering, action playback and open dialogs/drafts to finish, then appears before the next first-use tip.
Reading Order manually consumes that pending reveal. Initial load, reload/reconnect and recorded sessions
are baselines, not a new announcement; duplicate events do not repeat it. Advancing beyond the round or
losing the seat clears it. No reveal preference or private data is persisted. The History entries for
TimelineBuilt and TiesOrdered retain their respective exact positions, initiatives and rolls, so the
initial order cannot be mistaken for the final same-team choices.

Tie groups explain their fixed team positions, the chosen occupant of each, and intervening opponent
positions. Only offered group members can move; the existing planning controller still owns confirmation,
acknowledgement and write fencing. A same-team tie with no recorded dice does not claim to have rolled.
Open references retain the draft and focus and never acknowledge a covered question.

`table/arena-order.js` holds the pure projections and reveal watermark; `src/order.ts` renders stable
native controls. `arena-order.spec.js` covers private/unrevealed choices, interleaved and multiple ties,
repeat reveals, cursor/condition updates, focus/scroll, lost access and small/landscape/enlarged text.
Full real-host journeys check order/rolls against seat responses in both declarations and inspect a past
round during match replay. Captures in `docs/tabletop/images/arena-order/` identify their data source.

Captures from the recorded-roll browser fixture: [desktop](../docs/tabletop/images/arena-order/desktop-fixture.jpg) · [phone](../docs/tabletop/images/arena-order/phone-fixture.jpg).
