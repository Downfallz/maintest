# code-reviewer lessons (branch claude/ux12-pictures-plan-eb5u4p)

- A size change of a drawn picture (journal 22 -> 24 px, UX12 P4) leaves the old number in the docs that describe the drawing, not only in the plan's step: grep `docs/art` for the old `NN px` (ux12-pictures-plan.md:19 table, spell-vfx.md:11 still said 22) (f37a332).
- A source-scanning width check written as `spellPortrait\([^,]+,\s*(\d+)\)` is blind to a call that takes the default (40): count every `spellPortrait(` and require each to carry the width (table/battle-spell-art.test.js, f37a332).
