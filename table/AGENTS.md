# table/ — the pages people play on

Static HTML, CSS and JavaScript, served by the table host (`src/DownfallArena.Cli/Table`, see its `AGENTS.md`).
What the client may never decide is in docs/tabletop/playtest-app.md; the routes are in `README.md#routes`.

## What is here

- The lobby and home (`/`, `/lobby`, ADR 0115), the admin panel (`/admin`), the pilot page, the leaderboard
  and the statistics, the rulebook (`/rules`, ADR 0188), the offline page and the service worker (ADR 0166, 0191).
- `presentation.js`: the pure seat-to-presentation boundary shared with the live battle work.
- `identity.css`, loaded last by every page, owns the shared bone-and-soot parts: buttons, cards, windows,
  headers (ADR 0158, docs/art/branding.md). `fonts/` holds the display face the host serves.
- The motion kit (ADR 0197, `docs/art/branding.md#motion`): its forms at the foot of `identity.css`, and
  `motion.js`, which every page that loads `identity.css` also loads: it handles less motion and ambience.
- The picture kit (ADR 0198, docs/art/page-pictures.md): `picture.js` writes a painting's `<img>` and a strip of
  frames from `page-art.js`, which `scripts/ux/pictures.mjs` derives with `page-art/`; the battle bundle takes only
  the form, `picture-form.js`, and its own pictures, `battle-page-art.js`, which is also derived; `icons.js` is the
  icon set.
- `battle-*.js`: the shared modules the battle view imports. `battle-build/` is the bundle `battle/` builds:
  ignored, never committed, never edited by hand.
- `/battle-lab` is an isolated visual study on fictional sample frames, not a live seat
  (docs/tabletop/browser-battle-plan.md, ADR 0123).
- `/battle` is the Phaser complete game loop for one authenticated seat (ADRs 0125–0128), the game every seat
  opens in since ADR 0157. `/play` keeps the invitation doorway, replays, practice, and the archived classic
  table for a browser storing `downfall.presentation = classic`.

## Commands

```bash
node --test studio/*.test.js table/*.test.js showcase/*.test.js        # the pages' own tests; no install
dotnet run --project src/DownfallArena.Cli -- table --lobby         # serve the pages locally
dotnet run --project src/DownfallArena.Cli -- table --practice      # the practice scenarios
```

Every flag of `table` is in docs/reference/commands.md.

## A change here runs

- `node --test studio/*.test.js table/*.test.js showcase/*.test.js`.
- The gate's `dotnet test`: `TableFilesTests` and `TablePageContentTests` in `tests/DownfallArena.Cli.Tests/Table`
  read these files.
- A change to `battle-*` files: rebuild the bundle and run the battle specs (`battle/AGENTS.md`).
- A change a person sees: the browser suite's table projects (`studio/browser`, `table.config.js`).
- A change to how a journey looks, reads or moves is staged first: its brief is in `docs/art/journeys/`, and
  `/ux-pass` runs the loop of brief, build, film and critique with the `ux-designer` agent.

## Traps

- No dependency, no build step, no CDN or remote font (ADR 0024). Tests use Node's own runner. Two generated files:
  `rules.json` and `rules.fr.json`, the rulebook built from `rules/` by `node scripts/rules/build.mjs` (ADR 0188,
  0193), never edited by hand.
- A new file is served only once it joins the host's file table (`src/DownfallArena.Cli/Table/TableFiles.cs`)
  and its test (`TableFilesTests`); a missing one is a 404 in production, not a failing page test. A module
  only the battle bundle imports joins it too: `Every_module_beside_the_page_is_served` asks it of every script here.
- `identity.css` stays last in every page's `<head>`.
- A painting on a page comes from `picture.js`, declared for that page in `docs/art/page-pictures.json` and under
  its budget (`docs/art/page-budget.json`); never a file of `battle-art/` sent whole, never a derived file's name
  written in a page. `page-art.js`, `battle-page-art.js` and `page-art/` are generated: derive them again, do not
  edit them. An icon comes from `icons.js`, inside a control that has a name; a new emoji or glyph as an icon is refused in review.
- A new module the picture kit imports joins, in the same change, `TableFiles.cs`, `OFFLINE_SCRIPTS` of `sw.js`, `KIT`
  of `studio/browser/picture-kit.spec.js` and `SHOWCASE_FILES` of `studio/browser/plan.js`: each lists them by name.
- Motion comes from the kit: a token of `docs/art/branding.css`, never a number; `transform` and `opacity` only;
  travel under full motion alone. A `<dialog>` stays displayed while it fades after closing: test `open`, not `display`. The
  match's windows are in the kit, its docked sheets (`.docked-sheet`) fade in and leave at once, and
  `battle/src/windows.ts` holds their focus (UX12): a new window gets its frame control in the map `live.ts` gives it.
- The host serves a page with its stylesheets and `battle-build/` bundles named `?v=<tag>` (`TableFiles`), so a cache
  cannot pair a page with another deploy's bundle. A module another module imports keeps its bare name.
- 320 px wide first, 44 px touch targets, no document scrolling in the battle view.
- The words a player reads come from docs/domain/player-terminology.md, not from the domain's own.
  `terminology.test.js` holds them to its EN column and refuses the legacy words and sentences they
  replaced, whatever their case. It reads the page through `scripts/terminology.mjs`, as the rulebook's
  build does. A one-word string in a script is read only where it is assigned to `textContent`,
  `innerText`, `title`, `ariaLabel`, `placeholder` or `alt`; any other one-word label comes from `terms.js`.
- A page reads what changed from the host's streams or the relay, never on a clock (ADR 0164, ADR 0165).
- The host decides; the page shows. A legal option, a card's text or a rule computed here is a bug.
