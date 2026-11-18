---
paths:
  - "scripts/**"
---

# Script rules

- A script whose empty output reads as "nothing to do" (no reviewer, no section reached, nothing to report)
  refuses empty, unparsed or unexpected input with a message on stderr and exit 1, and a test feeds it that
  input (#716). A failed `git diff` writes nothing on stdout: that is not an empty diff.
- A scanner of the player's words reads docs/domain/player-terminology.md through `scripts/terminology.mjs`,
  and refuses every sentence of its Before column, in any case (#675). An everyday word exempted as lowercase
  ("speed") lets "Choose speed" through otherwise.
- A script that a page test or a skill imports is tested from `studio/` or `table/` (`node --test`), its exit
  codes included.
