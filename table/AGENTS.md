# table/ — the pages people play on

Static HTML, CSS and JavaScript, served by the table host (`src/DownfallArena.Cli/Table`, see its `AGENTS.md`).
What the client may never decide is in docs/tabletop/playtest-app.md; the routes are in `README.md#routes`.

## What is here

- The lobby and home (`/`, `/lobby`, ADR 0115), the admin panel (`/admin`), the pilot page, the leaderboard
  and the statistics, the rulebook (`/rules`, ADR 0188), the offline page and the service worker (ADR 0166, 0191).
- `presentation.js`: the pure seat-to-presentation boundary shared with the live battle work.
- `identity.css`, loaded last by every page, owns the shared bone-and-soot parts: buttons, cards, windows,
  headers (ADR 0158, docs/art/branding.md). `fonts/` holds the display face the host serves.
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

## Traps

- No dependency, no build step, no CDN or remote font (ADR 0024). Tests use Node's own runner. One generated file:
  `rules.json`, the rulebook built from `rules/` by `node scripts/rules/build.mjs` (ADR 0188), never edited by hand.
- A new file is served only once it joins the host's file table (`src/DownfallArena.Cli/Table/TableFiles.cs`)
  and its test (`TableFilesTests`); a missing one is a 404 in production, not a failing page test.
- `identity.css` stays last in every page's `<head>`.
- The host serves a page with its stylesheets and `battle-build/` bundles named `?v=<tag>` (`TableFiles`), so a cache
  cannot pair a page with another deploy's bundle. A module another module imports keeps its bare name.
- 320 px wide first, 44 px touch targets, no document scrolling in the battle view.
- The words a player reads come from docs/domain/player-terminology.md, not from the domain's own.
  `terminology.test.js` holds them to its EN column and refuses the legacy words and sentences it
  replaced. A one-word string in a script is read only where it is assigned to `textContent`, `innerText`,
  `title`, `ariaLabel`, `placeholder` or `alt`; any other one-word label comes from `terms.js`.
- A page reads what changed from the host's streams or the relay, never on a clock (ADR 0164, ADR 0165).
- The host decides; the page shows. A legal option, a card's text or a rule computed here is a bug.
