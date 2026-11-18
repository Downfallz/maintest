# code-reviewer lessons (branch claude/issue-736-scenario-cut-spb4x9)

- A host refusal that removes an action from the sheet must also be grepped in the browser specs' hand-written host mocks ("as FeedbackAdminApi decides it", admin.ux.spec.js:327 still offers `reproduce` while `underWay`): the spec stays green and documents the old host (#736, bbf7189).
- A new refusal on a source ("not while under way") must be grepped against the plan's `**decided**` lines on that source ("from any recorded session", scenarios-plan.md:30), not only the route reference (#736, bbf7189).
- A page note promising "X once the game is over" keyed only on `underWay` must carry every other condition the host checks for X (Cuttable: round > 0 and a seat), or it promises what never comes (#736, admin-feedback.js:395).
