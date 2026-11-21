# code-reviewer lessons (branch ccr-715dd578-1xhbgw)

- A grid layout switched on by `:has(> .picture)` still matches when the picture fails (`.missing { display: none }` keeps the node): the next two children auto-place side by side in the empty grid. Probe the 404 on every container the `:has` reshapes, not only the one the spec covers (UX12 P5, battle.css:1149 `.atlas-card-head`, 6a507acc).
- A 404 probe on a picture the page already showed (the pick's form, then the tree's detail of the same mutation) passes through the memory cache and never errors: route the failure before the first draw, or probe a picture drawn only on the screen under test (UX12 P5, 6a507acc).
- Sixth time (#812, UX10, #1004, #1010, UX12 P3, then UX12 P5): the page newly reads `/api/catalogue` packages' `visual.artwork`, PackageTagsTests asserts family/motif/portrait only, the spec writes `artwork` by hand. Still not in `.claude/rules/tests.md` (6a507acc).
