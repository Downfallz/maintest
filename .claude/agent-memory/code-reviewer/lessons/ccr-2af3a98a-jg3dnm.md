# code-reviewer lessons (branch ccr-2af3a98a-jg3dnm)

- A keep-list of `srcset` sizes chosen as "the width and twice it" misses every phone over DPR 2: Chromium at 390 px x3 and 412 px x2.625 picks the 1280 file over 708. Probe `currentSrc` with `deviceScaleFactor` 3 before trusting "a phone's sizes" (#1060, table/sw.js KEPT_PICTURES, d99aacb).
- A state fix-up run after one caller of a shared mutator (`reachScrolls` after `applyFilter` in show/input) misses the other callers (`land` clears the filter too): put the fix-up inside the mutator, or grep every caller (#1059, table/rules.js, d99aacb).
- A hit area grown by padding plus a cancelling negative margin on an inline link also grows its focus outline: the ring covers the neighbouring letters and lines; screenshot a focused link (#1059, table/rules.css:53, d99aacb).
