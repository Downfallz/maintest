# code-reviewer lessons (branch ccr-bb05b300-ug55nx)

- An absolutely positioned `<img>` given `left` and `right` with `width: auto` ignores `right`: a replaced element takes its width from its height and ratio, so a cover meant to fill the rest of the panel overflows it and `overflow: hidden` clips it silently (the duel at 1041 px lost ~40 % of its right side). Measure the img's box against its container at each breakpoint, the spec checked only the phone (scene paintings S2, table/lobby.css:241, 8116084).
