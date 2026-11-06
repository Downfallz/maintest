# 0123. Prototype the arena as an isolated presentation

Date: 2026-11-03
Status: Accepted for the isolated prototype; live adoption is a later decision

## Context

The table is a functional tactical client. The first DOM study validated selection and fixed action
frames but remained a scrolling page. The maintainer requested an immersive, viewport-bound game and
a Phaser proof of concept. The .NET engine already supplies public before/after action frames (ADR 0075).

## Decision

Use pinned Phaser 3.90.0 and TypeScript in `arena/` for the isolated `/arena-lab` scene. Bundle with
esbuild into ignored `table/arena-build/`; npm's lockfile pins the build. CI and a dedicated Node stage
in Docker build the bundle before .NET route tests or packaging. The runtime serves JavaScript, WebP
art and license notices locally through explicit `TableFiles` routes; no CDN is required.

Phaser owns the environment, creature illustrations, ambient effects, attack/recoil tweens and camera.
HTML owns keyboard/touch targets, the HUD, live status and native inspection/settings dialogs. A
ResizeObserver sizes the canvas to its actual stage, including mobile rotation and browser chrome.
Portrait uses two rows; landscape uses opposing formations. Primary controls stay in the viewport.
Fullscreen is optional. Sound starts only after opt-in; reduced motion and skip reach the final frame.

Keep the authored fixture and cancellable presentation state machine outside Phaser. They describe
three fixed outcomes, not game rules. Reset, interruption and resizing cancel pending playback. The lab
reads no live seat, creates no account/session and posts no commands. `/play` remains unchanged.
Use provisional generated illustrations with a selectable vector fallback for missing artwork.

Later live integration must keep the authoritative seat view, displayed historical frame and pending
local choice separate. The engine owns legality, resolution and progress. Animation completion never
becomes a game command. The fixture must never be imported by a live adapter. See the
[delivery plan](../tabletop/browser-battle-plan.md) and [build instructions](../../battle/README.md).

## Consequences

- This establishes a concrete visual direction without migrating the rules or existing player flow.
- The experiment adds a frontend build to CI/container packaging; ordinary `/play` remains plain modules.
- The bundle is about 1.2 MB uncompressed and the initial art about 0.8 MB; device profiling is still needed.
- Accessible DOM controls duplicate scene positions, so resize and hit-target checks are required.
- Illustrations are three reusable static forms with transforms, not six finished animated rigs.
- WebGL uses Phaser's canvas fallback where available. Missing art remains playable; renderer failure
  offers reload. Real-device Safari/Android, screen-reader and GPU-loss testing remain release work.
- Accepting Phaser for this experiment does not approve replacing `/play`. Live integration is A3–A8.

## Alternatives considered

- Keep the DOM study and animate it with CSS: it stayed a scrolling page, and a battlefield of large figures
  with depth, particles and a camera is what the DOM does worst. It lost on the feel the prototype exists to test.
- PixiJS: a renderer alone, with scenes, scaling, tweens, cameras and a loader to write or assemble. Phaser
  ships all of them, and the prototype is a spike, not a framework; the heavier bundle is the price.
- A CDN build of Phaser: no local build, but a runtime dependency on a third party for a page the hosted
  table serves, and nothing to pin. Bundling locally keeps the lockfile as the one version.
- A Phaser-only page, with the targets drawn on the canvas: no duplicated positions, but no keyboard, no
  screen reader and no native dialogs. Accessible DOM targets over the canvas cost a shared layout instead.

## Follow-up

- `TableFiles` routes the bundle and the art; `TableFilesTests` holds them, and says to build when it is absent.
- `AGENTS.md`, the `/verify` skill, CI, the Docker image and Dependabot know the `arena/` package.
- The live adapter (A3–A4) replaces the fixture with the seat projection; this ADR's fixture rule holds until then.
