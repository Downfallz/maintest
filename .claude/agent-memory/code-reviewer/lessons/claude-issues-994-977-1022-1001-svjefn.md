# code-reviewer lessons (branch claude/issues-994-977-1022-1001-svjefn)

- A "names of several words keep their capitals" scanner built from `termRows` (tables headed `Internal` only) checks exactly one name (Mutation Tree), and `sources()` skips showcase/: `showcase/lore.mjs:17` "Explore the mutation tree" passed. Print the name list and the folders before calling such a test a guard (#1022, terminology.test.js miscased, 1252b5a3).
- A word-budget test whose comment derives the limit (60 at 320 px minus a 10-word frame = 50) but asserts the old 60 for every other card leaves Health and Defense (60 own words, about 70 at 320) over the brief it quotes: read the asserted number against the comment's own arithmetic (#977, concepts.test.js:546-552, 1252b5a3).
