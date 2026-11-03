# Phaser arena laboratory

A viewport-bound visual experiment at `/arena-lab`, isolated from the actual `/play` client.
Pinned Phaser 3.90.0 + TypeScript, bundled locally with esbuild. [ADR 0123](../docs/adr/0123-prototype-the-arena-as-an-isolated-presentation.md)
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

`build` type-checks and emits `table/arena-build/game.js`, legal comments and the Phaser MIT license.
Output is ignored, never committed. CI builds before route/browser tests. Docker uses a Node builder
stage and copies only the bundle/notices into the .NET runtime alongside the local WebP assets.
No CDN, remote fonts or browser package downloads are needed at runtime.

## Boundaries

- `src/main.ts`: accessible DOM controller, input, dialogs, lifecycle and optional synthesized sound.
- `src/scene.ts`: Phaser scene, local assets, creature figures, cancellable tweens and camera effects.
- `src/types.ts`: presentation types only.
- `table/arena-lab-state.js`: the deterministic sample state machine, which carries the one action it offers,
  and the cancellation generation.
- `table/arena-lab-layout.js`: the portrait/landscape geometry, shared by the Phaser figures and the HTML hit
  targets so the two cannot drift.
- `table/arena-lab-fixture.js`: fictional before/after frames. Never import this into a live adapter.

The three `table/` modules are plain JavaScript so `node --test table/*.test.js` covers them without a build:
the TypeScript imports them, and holds nothing that is not Phaser or the DOM.

No rules, legality, damage or random resolution are reimplemented here. There is no API request, saved
seat, live stream or service worker. A3 extracts the shared read-only boundary in
[`table/presentation.js`](../table/presentation.js), already used by `/play`. A4 will connect seat-filtered
views through that boundary in a separate live adapter; the lab keeps its fixture.
The compiled bundle is about 1.2 MB uncompressed; the two art files total
about 0.8 MB. Device performance and transfer compression remain to be measured before release.

## Verify

```bash
node --test studio/*.test.js table/*.test.js
npm --prefix studio/browser ci --ignore-scripts
npm --prefix studio/browser exec -- playwright install chromium
npm --prefix studio/browser test -- arena-lab.spec.js
dotnet build
dotnet test --no-build
dotnet format --verify-no-changes
```

The browser suite checks actual Phaser startup, both orientations, 320px width, 44px target bounds,
no document scrolling, rotation, all three outcomes, cancellation/reset, keyboard/skip, reduced motion,
2× settings, missing-art fallback and no API requests. Route tests verify WebP binary handling and the
packaged bundle. Screenshots in the delivery plan come from the actual .NET host.

This is one authored action, three reused static forms and illustrative UI text. Real-device Safari,
Android GPU/performance, screen readers, high zoom and a complete real match remain future acceptance.
