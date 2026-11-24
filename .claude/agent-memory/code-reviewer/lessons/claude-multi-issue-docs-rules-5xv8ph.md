# code-reviewer lessons (branch claude/multi-issue-docs-rules-5xv8ph)

- A craft book's or a plan's "Here: none exists" claim is grepped across every sheet the page loads (`table/table.css`, not only `table/identity.css` and `table/lobby.css`): UX1's walk said the pages had no motion tokens and nothing to reduce, and `table.css` held both (#928).
- A test that holds a lesson "to the paths where its mistake was made" must list every file the issue names (#979 named `scripts/ux/play.mjs`, which `pages.md` does not load on) and check each path exists (a made-up `Infrastructure/Content/SpellVisual.cs` passed) (cd5be92).
- A plan's proposal reworded after its stage is built must be read against the stage's ADR: "in place of the blanket rule" contradicted ADR 0197, which keeps the net (cd5be92).
