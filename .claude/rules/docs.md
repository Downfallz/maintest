---
paths:
  - "docs/**"
  - "AGENTS.md"
  - "CLAUDE.md"
  - "**/AGENTS.md"
---

# Documentation rules

- ADRs follow `docs/adr/0000-template.md`. Numbered, immutable once accepted; superseding means a new ADR
  that links back. Keep them under a page. The first line is `# NNNN. Title`, the status is on a
  `Status:` line, and a number is taken after fetching `main` (`/adr`, ADR 0183).
- The ADR table and the journal's list are generated, never edited (ADR 0184). After adding an ADR or a journal
  entry, and to settle a conflict in either:
  `UPDATE_INDEXES=1 dotnet test --project tests/DownfallArena.Architecture.Tests`.
- The glossary is the ubiquitous language. One term per entry, a one-sentence definition, and its status
  (`decided`, `inherited from legacy`, `open`).
- Game rules in `docs/domain/game-rules.md` describe what the engine does today or is decided to do next.
  Ideas go in the "Open questions" section, not in the rules.
- `AGENTS.md` is the contract with AI agents: an index under a budget (ADR 0181). Change it when a convention
  changes; do not let it drift. A command, a flag or a route goes in `docs/reference/commands.md` (and a route in
  `table/README.md#routes`); an area's commands and traps in that area's `AGENTS.md`.
- A new plan (`docs/**/*plan*.md`) gets a row in `docs/README.md`; a test checks it.
- Write in English. Short sentences. No marketing.
