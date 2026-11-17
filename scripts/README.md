# scripts/

The scripts the gate, the learning loop and the reports run. Each command, with its flags, is described once in
[docs/reference/commands.md](../docs/reference/commands.md); this page says what lives where.

| Path | What it is |
| --- | --- |
| `verify.sh`, `gate/` | The local gate (`/verify`), the storage emulator it starts, and the record of the last green gate. |
| `iterate.sh` | The learning loop, one turn at a time (docs/learning/). |
| `build-tiers.py`, `declaration-reading.py`, `slot-choices.py`, `sweep-weight.py` | The learning loop's readers and one-off measurements. |
| `review/` | The reviewers a diff calls for (`/reviewers`). |
| `rules/` | The rulebook's build and the sections a diff reaches (`/rules`, `rules/README.md`). |
| `digest/` | The morning digest (`/digest`). |
| `ops/` | The production report (`/ops`, ADR 0186). |
| `spells-report/` | The spells the bots cast and leave (`/spells-report`). |
| `design-system/` | The design system rebuilt from the repository (`/design-system`). |
| `sync-branding.mjs`, `matte-opaque-art.py` | The brand tokens inlined into the pages, and a portrait given its alpha. |
| `workflow-metrics.py` | The numbers of docs/engineering/ai-workflow-plan.md (`/retro`). |
