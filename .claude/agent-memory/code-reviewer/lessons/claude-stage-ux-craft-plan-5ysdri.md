# code-reviewer lessons (branch claude/stage-ux-craft-plan-5ysdri)

- A skill step that grows a list a test pins ("a (new) screen joins the walk in scripts/ux/journeys.mjs") must carry every file that test couples to it: ux-journey.test.js:121 also wants the screen's line in baseline.md, said in studio/AGENTS.md:35 but not in /ux-pass step 4. Grep the area's AGENTS.md for "also needs" against the skill's build step (UX3, a8348c2).
- Second time (after storyboards.test.js's empty cells): a validator's escape hatch (`*UX4*` "and what the kit must declare") checked with startsWith accepts the bare marker; probe each hatch with nothing after it (UX3, journey-briefs.test.js:153, a8348c2).
