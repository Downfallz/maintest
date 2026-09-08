---
paths:
  - "docs/**"
  - "AGENTS.md"
  - "CLAUDE.md"
---

# Documentation rules

- ADRs follow `docs/adr/0000-template.md`. Numbered, immutable once accepted; superseding means a new ADR
  that links back. Keep them under a page.
- The glossary is the ubiquitous language. One term per entry, a one-sentence definition, and its status
  (`decided`, `inherited from legacy`, `open`).
- Game rules in `docs/domain/game-rules.md` describe what the engine does today or is decided to do next.
  Ideas go in the "Open questions" section, not in the rules.
- `AGENTS.md` is the contract with AI agents. Change it when a convention changes; do not let it drift.
- Write in English. Short sentences. No marketing.
