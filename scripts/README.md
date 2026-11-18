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
| `battle/` | The names the installed Phaser declares, for the craft book (docs/art/battle-craft.md) and its test. |
| `terminology.mjs` | The one reader of docs/domain/player-terminology.md's tables, for every scanner of the player's words. |
| `sync-branding.mjs`, `matte-opaque-art.py` | The brand tokens inlined into the pages, and a portrait given its alpha. |
| `workflow-metrics.py` | The numbers of docs/engineering/ai-workflow-plan.md (`/retro`). |
