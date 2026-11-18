---
paths:
  - "scripts/**"
---

# Script rules

- A script whose empty output reads as "nothing to do" (no reviewer, no section reached, nothing to report)
  refuses empty, unparsed or unexpected input with a message on stderr and exit 1, and a test feeds it that
  input (#716). A failed `git diff` writes nothing on stdout: that is not an empty diff.
- A scanner of the player's words reads docs/domain/player-terminology.md through `scripts/terminology.mjs`,
  and refuses every sentence of its Before column, whatever the case of its letters (#675). Exempting an
  everyday word such as "speed" because it is lowercase would let "Choose speed" through.
- A script a skill runs is tested from `studio/`, `table/` or `showcase/` (CI's `node --test` reads only those
  folders), exit codes included.
- A new name the rulebook's build draws (`lookup` in `scripts/rules/build.mjs`) ships with what reaches it in
  `scripts/rules/reach.mjs`, and a case in `table/rules-reach.test.js` (#687, #805): a value source `reach.mjs`
  was never taught reaches no section, and `/rules` says the book is untouched.
