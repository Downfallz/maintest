---
name: adr
description: Create a new Architecture Decision Record in docs/adr from the template, numbered correctly and linked from the index. Use when a change introduces a dependency, changes a convention or layering rule, or settles a game-design question that is hard to reverse.
---

# Create an ADR

1. Determine the next number: list `docs/adr/*.md`, take the highest four-digit prefix, add one.
2. Slug the title: lower-case, hyphens, no stop words (`0006-use-wolverine-for-messaging.md`).
3. Copy `docs/adr/0000-template.md` to the new file and fill every section:
   - Context: the forces, in two to five sentences. What makes this a decision at all.
   - Decision: one paragraph, active voice ("We will ...").
   - Consequences: good, bad, and neutral. Be honest about the bad ones.
   - Alternatives considered: one line each with why it lost.
4. Status starts as `Proposed` unless the user says it is already agreed, then `Accepted`.
5. Add a row to the table in `docs/adr/README.md`.
6. If the decision changes a rule that code enforces (architecture tests, analyzers, `AGENTS.md`), list those
   files under "Follow-up" in the ADR and, if asked, make the changes in the same commit.

Keep the ADR under one page. Link related ADRs by number.
