# Agent memory

What `code-reviewer` and `host-reviewer` keep between sessions (`memory: project` in their front matter): one
folder each, `<agent>/MEMORY.md` first, written by the agent and committed with the change that taught it.

A session writes a new lesson only in `<agent>/lessons/<branch>.md`, one file per branch (its name with `/`
turned into `-`). Two pull requests in flight then never touch the same file, so the memory no longer conflicts
on every merge. `/retro` folds the week's `lessons/` files into `<agent>/lessons.md` (or into a rule) and deletes
them; `MEMORY.md` and `lessons.md` change only there. `.gitattributes` merges this folder with `merge=union`, as
a safety net for a local merge.

This is a trial (stage W5 of `docs/engineering/ai-workflow-plan.md`): whether a cloud session's reviewer writes
here, and whether the next session's reviewer reads it. A lesson that holds is moved into `.claude/rules/` or an
area's `AGENTS.md`, where every session reads it, and removed from here.
