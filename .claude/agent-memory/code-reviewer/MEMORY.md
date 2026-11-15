# code-reviewer memory

- A path router keyed on file extension misses the JS registries that hold the look (sprite bounds and anchors in `table/battle-art.js`, hybrids in `table/battle-evolution-art.js`, hex colours in `table/table.js`): check the agent's own checklist against the router (design-reviewer PR, 89e9dcd). The router now also reads the added lines of a page script (issue #606).
- Adding a reviewer: the checklist is in the header of `scripts/review/reviewers.mjs` (issue #606). Also update the W5 "Built" list in `docs/engineering/ai-workflow-plan.md` and narrow the sibling reviewer whose scope it takes over (design-reviewer PR, 89e9dcd).
