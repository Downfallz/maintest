# code-reviewer lessons (branch claude/issues-675-716-fw7ni2)

- A "one reader of X" test keyed on one literal of the old copy (`=== 'Before'`, five folders, not recursive) only catches that exact copy: a reader written with `startsWith('| Before')` or of the Internal table passes. Key it on what every reader must do (reads the file by name) and require the import (#675, terminology.test.js, ede430f).
- A refusal rule worded "empty, unparsed or unexpected input" but implemented as "no path" still reads `fatal: ...` (git diff 2>&1) or `git show` output as a path list and exits 0 silent: probe the guard with non-empty garbage, not just blanks (#716, reviewers.mjs, ede430f).
- Second round, same trap: the "one reader" guard went repo-wide but stayed keyed on parse literals (`split('|')`, `startsWith('|`); `split(/\|/)` with `trim()[0] === '|'` passes (probed, 810e41a). Probe such guards with a rogue file written differently, not the old copy.
