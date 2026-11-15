# Window

A centred window (`dialog`): cut top right by `cut-window` (16px), grain and soot inside, a bone-tinted edge.

- The head (`.dialog-heading`) is a gradient from `soot` to `paper` with grain: an `eyebrow` in `ember`, then the title in `window-title`, `bone`. Under it runs the rune rule: the rune, then a bone hairline fading out.
- The consumer provides the body and at most one `.primary`, placed last among the actions.
- The backdrop is `soot` at 82%.
- A sheet docked to an edge of the screen (the match's planning, combat and upkeep sheets, any window under 620px wide) sets `--cut: 0px` and stays square.
