# code-reviewer lessons (branch ccr-6c38e4f7-2vflcz)
- Fetch `origin/main` and `git merge-tree --write-tree origin/main HEAD` before judging an ADR's number: promo P1 took 0204 while #1166 merged its own 0204 on main after the branch point, and the generated index then conflicts (promo P1, 6888c9b).
- A real-engine spec that opens a repository scenario through the host reads that scenario file: check `plan.js` plays it when the file changes (top-level `scenarios/*.json` matched no rule, promo P1 table.promo.js).
