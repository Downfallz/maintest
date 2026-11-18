# studio/ — the content studio, and the browser suite

The static content studio: browse, edit, version and try the game content (`README.md`). It is served by
`studio` (`src/DownfallArena.Cli/Studio`, ADR 0015) and published as files (ADR 0023). `browser/` holds the
Playwright suite for the studio, the table and the battle view.

## Commands

```bash
node --test studio/*.test.js table/*.test.js showcase/*.test.js             # the pages' own tests; no install
dotnet run --project src/DownfallArena.Cli -- studio                    # http://127.0.0.1:5099
dotnet run --project src/DownfallArena.Cli -- studio --export site/data # what the published studio reads
npm --prefix studio/browser ci --ignore-scripts && npm --prefix studio/browser test -- --trace off   # the browser suite
```

## A change here runs

- `node --test studio/*.test.js table/*.test.js showcase/*.test.js`, and the gate.
- A change a person sees: the studio's browser specs (`studio/browser`), phone (320 px and 390 px) and desktop.
- A change to `browser/` itself: the specs it touches, and say which were run.
- `git diff --name-only origin/main... | node studio/browser/plan.js --pull-request` says what CI will play for a
  pull request (ADR 0182, 0190): the specs, the real-engine job, the release profiles, the image.

## Traps

- No runtime dependency or build step (ADR 0024); only the browser tests use pinned Playwright.
- The published studio is served under a GitHub Pages subpath: no absolute URL.
- `studio` refuses `--declaration`; the studio plays the engine's default.
- A spec, a fixture or a job that starts to read another path says so in `browser/plan.js` and
  `browser-suite.test.js`: CI plays only what the plan names, and the nightly matrix is what finds a gap.
- CI retries a journey once and `browser/flakes.js` names what passed only then; locally nothing retries.
- In a cloud session Chromium is preinstalled: do not run `playwright install` again, and pass `--trace off`.
- Each checkout serves the pages on a port derived from its path (`browser/origin.js`). A running server is
  reused only when its `/__root` answer names this checkout (#711). `STUDIO_BROWSER_PORT` sets the port.
- The real-engine journeys start the host with `browser/dotnet.js`. It uses the `dotnet` under `DOTNET_ROOT`
  when set, else the one on `PATH` (#707).
- A team's tempos are one asking per creature, sent one after another from one sheet: a real-engine journey's step
  that answers them ends once the round leaves its tempos (`browser/battle-step.js`), not when the asking changes (#787).
