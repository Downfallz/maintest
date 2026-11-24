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

- No runtime dependency or build step (ADR 0024); only the browser tests use pinned Playwright, and axe-core for the journeys' measures.
- `battle-craft.test.js` holds the craft book to the Phaser types `npm --prefix battle ci --ignore-scripts`
  installs. Without them its engine checks are skipped and say so. The gate and CI's full build install them
  first, and in CI's full build missing types fail the test.
- `ux-craft.test.js` holds the pages' craft book (`docs/art/ux-craft.md`): an entry's six lines, the paths it
  names, its links to a file or a heading, and its tokens against `docs/art/branding.css`, `table/table.css` and
  `table/identity.css`. It cannot open a link to the web.
- `ux-journey.test.js` holds the journey film (`scripts/ux/`) without a browser: its arguments, its journeys'
  names, its measures and the baseline's page. The walk is played by `browser/table.journey.js`, twice, on the real
  engine: a measure that reads a clock or an id breaks it. A new screen of a journey also needs its line in the baseline.
  A walk's setup `request.post` (an account, a lesson skipped) checks `ok()` and throws: a refusal otherwise
  goes unseen: the walk measures the wrong screen, or fails later somewhere unrelated (#1093).
- `journey-briefs.test.js` holds each brief of `docs/art/journeys/` to its nine parts, to the screens its journey's
  walk names (`scripts/ux/journeys.mjs`), to the craft book's headings and to the declared tokens: a screen or a
  heading renamed there renames it in the brief, in the same change.
- `rules-reach.test.js` checks that each lesson it lists sits in a rule file of `.claude/rules/` that loads on the
  paths where its mistake was made: a rule file loads only on the paths its front matter names (#979, #991,
  #1017). When you move or reword such a lesson, or rename a path it lists, update the test too.
- `storyboards.test.js` holds each beat of `docs/art/storyboards/` to the windows of `table/battle-timing.js`: a
  change to those timings re-times the storyboards and the figures in their `README.md`, in the same change.
- The published studio is served under a GitHub Pages subpath: no absolute URL.
- `studio` refuses `--declaration`; the studio plays the engine's default.
- A spec, a fixture or a job that starts to read another path says so in `browser/plan.js` and
  `browser-suite.test.js`: CI plays only what the plan names, and the nightly matrix is what finds a gap.
- CI retries a journey once and `browser/flakes.js` names what passed only then; locally nothing retries.
- `battle-journeys.test.js` holds the battle specs (`battle-*`, not the real-engine `battle.*.js`) to three rules for a
  loaded runner. Where a test only needs the playback's stages, jump between its timers with `fastForward`; a test
  that runs the fake clock 1 s or more frame by frame gets `test.setTimeout(60000)` (#784). To know the first board
  arrived, wait for `data-connection` to be `live`, never for the hidden `#loading` card (#785).
- A spec waits on what the page shows or on the fake clock, never on a real timer's length or a fixed poll budget:
  a slow shard turns a 2.6 s toast or a 30 s film into a flake (#961, #976, #1135).
- A battle spec reads its mock route's `shown` or `writes` through `expect.poll`. The request reaches the route some
  time after the click, and a change the page draws before sending (a menu closed, a bubble drawn) does not mean the
  route has it yet (#816, #831, #845). A plain read is fine in two cases, with no click, key press or awaited
  helper between: the test has waited for something only the host's answer produces (an error text, a URL
  change), or an `expect.poll` on the same field (`writes.length`) has passed. A negative read (`toEqual([])`) stays plain.
  `battle-journeys.test.js` checks this by reading the lines before each plain read. A wait it cannot see (a sheet
  closed after its batch) goes in its `READ_AFTER_HOST` list, by test and read, with the reason (#857).
- In a cloud session Chromium is preinstalled: do not run `playwright install` again, and pass `--trace off`.
- Each checkout serves the pages on a port derived from its path (`browser/origin.js`). A running server is
  reused only when its `/__root` answer names this checkout (#711). `STUDIO_BROWSER_PORT` sets the port.
- The real-engine journeys start the host with `browser/dotnet.js`. It uses the `dotnet` under `DOTNET_ROOT`
  when set, else the one on `PATH` (#707).
- A team's tempos are one asking per creature, sent one after another from one sheet: a real-engine journey's step
  that answers them ends once the round leaves its tempos (`browser/battle-step.js`), not when the asking changes (#787).
  `waitAnswered` waits for any step. Its window (10 s by default) restarts each time the seat's view moves, and the
  wait ends at three windows at most (#858).
- A real-engine journey collects the host's seat bodies in the page (`browser/seat-views.js`, or an `addInitScript`
  wrapping `fetch`, attached before `goto`). It never parses them in `page.on('response', async ...)`: Playwright does
  not await those handlers, so two reads can finish out of order and the spec can read a view older than the page
  drew (#903, #921). `seat-views.test.js` refuses such a handler in `browser/`.
