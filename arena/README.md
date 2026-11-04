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
- `table/arena-art.js`: audited sprite manifest, root-mutation mapping, neutral multiclass fallback and mirrored geometry.
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
npm --prefix studio/browser test -- arena-lab.spec.js arena-live.spec.js arena-combat.spec.js arena-playback.spec.js arena-loop.spec.js
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
`table/arena-planning.js` fences mutation, tempo and tie drafts; `src/planning.ts` renders the catalogue's
Mutation Tree with explicit confirmation. Tempo confirms one creature at a time, with simultaneous reveal
still owned by the host. `table/arena-match.js` and `src/match.ts` provide two-round upkeep/aftermath review,
concession, the host outcome and a manual whole-match replay. Replay steps distinguish recorded action frames,
labelled reference frames and missing battlefield snapshots. `/arena?replay=<session>&seat=player2` opens a
finished recording using the same operator authorization as `/play?replay=...`.

`node node_modules/playwright/cli.js test --config table.config.js arena.loop.js` (from `studio/browser`)
plays complete 3v3 games in both declarations, reads recorded sessions, and checks two independent seats'
simultaneous choices and concession against the real .NET engine.
