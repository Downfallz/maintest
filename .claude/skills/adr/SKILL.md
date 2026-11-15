---
name: adr
description: Create a new Architecture Decision Record in docs/adr from the template, numbered after fetching main so the number is not already taken. Use when a change introduces a dependency, changes a convention or layering rule, or settles a game-design question that is hard to reverse.
---

# Create an ADR

1. Determine the next number, on both sides (ADR 0183). Another branch may have merged an ADR since this one
   was cut, so the working tree alone is not enough:

   ```bash
   git fetch origin main
   { ls docs/adr; git ls-tree --name-only origin/main docs/adr/ | sed 's|.*/||'; } \
     | grep -oE '^[0-9]{4}' | sort -n | tail -1
   ```

   Take that number plus one. If the fetch fails, say so: the number is then a guess.
2. Slug the title: lower-case, hyphens, no stop words (`0006-use-wolverine-for-messaging.md`).
3. Copy `docs/adr/0000-template.md` to the new file and fill every section:
   - The first line is `# NNNN. Title`, and `Status:` is a line of its own above `## Context`: together they
     are the index, and `AdrFileTests` checks both.
   - Context: the forces, in two to five sentences. What makes this a decision at all.
   - Decision: one paragraph, active voice ("We will ...").
   - Consequences: good, bad, and neutral. Be honest about the bad ones.
   - Alternatives considered: one line each with why it lost.
4. Status starts as `Proposed` unless the user says it is already agreed, then `Accepted`.
5. There is no index to edit. If the ADR supersedes or amends another, say so on both `Status:` lines.
6. If the decision changes a rule that code enforces (architecture tests, analyzers, `AGENTS.md`), list those
   files under "Follow-up" in the ADR and, if asked, make the changes in the same commit.
7. If `main` gained the same number before the pull request merged, `AdrFileTests` fails once `main` is merged
   in: rename the file to the next number, and change its first line and whatever cites it.

Keep the ADR under one page. Link related ADRs by number.
