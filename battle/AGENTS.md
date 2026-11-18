# battle/ — the Phaser battle view

Isolated Phaser/TypeScript presentation, built into the ignored `table/battle-build/` (ADR 0123). It is the game
every seat opens in (ADR 0157). `README.md` describes each slice (AU1–AU14, A7, A8) and its boundaries.

## Commands

```bash
npm --prefix battle ci --ignore-scripts
npm --prefix battle run build          # type-checks and emits table/battle-build/game.js, live.js and fx.js
dotnet run --project src/DownfallArena.Cli -- table --lobby   # then /battle-lab, /battle-lab?bench, or a seat's /battle
node scripts/battle/filmstrip.mjs spell:pummel:v1    # one effect's film strip, from the built bundle
```

There is no watch server: rebuild after changing TypeScript or the shared table modules it imports.

## A change here, or to table/battle-*, runs

- `npm --prefix battle run build`, then the gate (`dotnet test` reads the bundle in `TableFilesTests`).
- `node --test studio/*.test.js table/*.test.js showcase/*.test.js`.
- The browser suite's battle specs, from `studio/browser`:

```bash
npm --prefix studio/browser ci --ignore-scripts
npm --prefix studio/browser exec -- playwright install chromium   # skip in a cloud session: Chromium is installed
npm --prefix studio/browser test -- --trace off battle-lab.spec.js battle-bench.spec.js battle-fx.spec.js battle-live.spec.js battle-combat.spec.js battle-playback.spec.js battle-loop.spec.js battle-history.spec.js battle-guidance.spec.js battle-upkeep.spec.js battle-mutations.spec.js battle-evolution.spec.js battle-tempo.spec.js battle-spellbook.spec.js battle-pacing.spec.js battle-growth.spec.js battle-order.spec.js battle-targets.spec.js battle-inspection.spec.js battle-services.spec.js battle-parity.spec.js battle-reactions.spec.js battle-recap.spec.js battle-tutorial.spec.js battle-release.spec.js
```

- Complete games against the real engine: `node node_modules/playwright/cli.js test --config table.config.js battle.loop.js`
  from `studio/browser`.

## Traps

- The build output is never committed, and CI builds it before the route and browser tests.
- No CDN, remote font or package download at runtime; Docker copies the bundle and its notices only.
- The view plays the host's confirmed frames. It never decides an outcome, and never holds the player's own
  cast (ADR 0141).
- Reduced motion and replay navigation show the settled view; first loads and hidden-tab returns replay nothing.
- Look at an effect before calling it done. The bench (`/battle-lab?bench`) plays any entry of the spell art.
  Its film strip puts twelve frames of the play on one sheet (`README.md`, "The effects bench").
- The effects kit (`src/fx/`, ADR 0196) is a bundle of its own with no engine in it. In `src/fx/`, import
  Phaser as a type only. Elsewhere, import the kit as a type only. `build.mjs` refuses both.
- A path `build.mjs` reads outside `battle/` needs its `COPY` in the Dockerfile's `battle-build` stage, or the
  image does not build while the local gate stays green.
- An effect is staged before it is built: its storyboard is in `docs/art/storyboards/`, and `/battle-pass` runs
  the loop of storyboard, build, film and critique with the `battle-designer` agent.
- A still field is drawn one frame in four (`src/rest.ts`, README "The resting loop"). New motion goes through a tween
  or the effects kit, or is counted in the scene's `busy()`: otherwise it plays at the resting rate.
- The words a player reads come from docs/domain/player-terminology.md.
- Every new item in the header's `.tools` is measured at 320 px with every button it can show: `battle-reactions.spec.js`
  checks the header with the reactions on (#767). Under 360 px, 💬 lives in the opponent's card.
- The suite takes 30 to 45 minutes in a session: run the specs the change touches first, then the list above.
