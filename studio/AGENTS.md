# studio/ — the content studio, and the browser suite

The static content studio: browse, edit, version and try the game content (`README.md`). It is served by
`studio` (`src/DownfallArena.Cli/Studio`, ADR 0015) and published as files (ADR 0023). `browser/` holds the
Playwright suite for the studio, the table and the battle view.

## Commands

```bash
node --test studio/*.test.js table/*.test.js                            # the pages' own tests; no install
dotnet run --project src/DownfallArena.Cli -- studio                    # http://127.0.0.1:5099
dotnet run --project src/DownfallArena.Cli -- studio --export site/data # what the published studio reads
npm --prefix studio/browser ci --ignore-scripts && npm --prefix studio/browser test -- --trace off   # the browser suite
```

## A change here runs

- `node --test studio/*.test.js table/*.test.js`, and the gate.
- A change a person sees: the studio's browser specs (`studio/browser`), phone (320 px and 390 px) and desktop.
- A change to `browser/` itself: the specs it touches, and say which were run.

## Traps

- No runtime dependency or build step (ADR 0024); only the browser tests use pinned Playwright.
- The published studio is served under a GitHub Pages subpath: no absolute URL.
- `studio` refuses `--declaration`; the studio plays the engine's default.
- In a cloud session Chromium is preinstalled: do not run `playwright install` again, and pass `--trace off`.
