# code-reviewer lessons (branch claude/lobby-issues-multiples-5f37x8)

- A `sizes` test whose rows land on the same file under the old and the new value proves nothing: pick a viewport and a density where the two pick different files (#1070: 400 by 900 at 1x, where `100vw` asked for 475 and the cover needs 506; lobby-home.spec.js, 387675f).
