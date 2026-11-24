# scripts/

The scripts the gate, the learning loop and the reports run. Each command, with its flags, is described once in
[docs/reference/commands.md](../docs/reference/commands.md); this page says what lives where.

| Path | What it is |
| --- | --- |
| `verify.sh`, `gate/` | The local gate (`/verify`): `verify.sh`, the storage emulator it starts, and the record of the last green gate. |
| `iterate.sh` | The learning loop, one turn at a time (docs/learning/). |
| `declaration-reading.py`, `slot-choices.py`, `sweep-weight.py` | The learning loop's readers and one-off measurements. |
| `build-tiers.py` | The one-time tier migration from the talent tree, kept reproducible. |
| `review/` | The reviewers a diff calls for (`/reviewers`). |
| `rules/` | The rulebook's build and the sections a diff reaches (`/rules`, [rules/README.md](../rules/README.md)). |
| `digest/` | The morning digest (`/digest`). |
| `ops/` | The production report (`/ops`, ADR 0186). |
| `spells-report/` | The spells the bots cast and leave (`/spells-report`). |
| `design-system/` | The design system rebuilt from the repository (`/design-system`). |
| `battle/` | The names the installed Phaser declares, for the craft book (docs/art/battle-craft.md) and its test; the film strip of one effect, filmed on the bench (`battle/README.md`); and the frame time of each staging under the browser's CPU throttle (`frametime.mjs`, docs/art/battle-acceptance.md). |
| `ux/` | The journey film (docs/art/ux-craft-plan.md, UX2): the five journeys and the match's windows (UX12) walked on the built showcase and a real host, a contact sheet and a file of measures each, one interaction across time, the first paints held to `docs/art/page-budget.json`, and the baseline's page. `ux/pictures.mjs` is the picture kit's script (UX5, ADR 0198): it derives a page's pictures at the size they are drawn from `docs/art/page-pictures.json`, holds them against the declaration and the budget, and lists what the briefs ask of the owner; `ux/encode.py` is its encoder (Pillow). `ux/round.mjs` makes the showcase's round seen played from the real battle view (UX6). `ux/access.mjs` and `ux/flash.mjs` are the checks of accessibility (UX13) that `studio/browser/table.access.js` runs on every screen; `ux/access-known.json` lists what they found and left to a later stage. |
| `promo/` | The promotion's shots taken from the real game (docs/art/promotion-plan.md, ADR 0205): `promo/shoot.mjs` frames a shot's scenario at each ratio or films it, `promo/cut.mjs` joins films with the brand's cards and captions ([promo/README.md](../promo/README.md)). |
| `markdown-anchors.mjs` | The anchors GitHub gives a markdown file's headings, for the tests that follow a link to one (`studio/prompts.test.js`, `studio/storyboards.test.js`, `studio/journey-briefs.test.js`). |
| `module-imports.mjs` | The modules a page script reaches through its static imports, for the tests that check a hand-written list of a page's scripts against what the scripts really import (`table/sw.test.js`, `studio/picture-kit.test.js`, #1136). |
| `terminology.mjs` | The one reader of docs/domain/player-terminology.md's tables, for every scanner of the player's words. |
| `sync-branding.mjs`, `matte-opaque-art.py` | The brand tokens inlined into the pages, and a portrait given its alpha. |
| `workflow-metrics.py` | The numbers of docs/engineering/ai-workflow-plan.md (`/retro`). |
