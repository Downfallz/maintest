# Evolution WebP library — staged intake

The owner-approved polished references are stored under `table/battle-art/evolutions/` as
WebP files. Most are 1536×1024; three Predator references have wider original canvases,
recorded explicitly in `table/battle-evolution-art.js` alongside stable tier IDs.
There is one registered image for each of the 21 stage-1-to-stage-3 tier IDs, plus one
unassigned Predator stage-2 visual study retained without guessing its package identity.

These are **staged assets, not yet eagerly loaded combat sprites**. The current
`table/battle-art.js` manifest and its four audited runtime sprite hashes, geometry,
fallbacks, and initial payload budgets are intentionally unchanged. Before activating
these images, verify edge transparency and silhouette anchors against the actual
battlefield. Loading every evolution texture at scene boot would exceed the initial
asset budget; a subsequent integration must fetch only active roster variants and
fall back to audited base art while an optional texture is unavailable.

## Adding visual hybridizations

- The dominant shape remains the Stage 3 tier's standalone image.
- Register only hand-approved variant pairs, e.g. `transcendent + necrotic`; do not
  generate the Cartesian product of all purchased tiers.
- A missing pair resolves to the primary Stage 3 art. Mutations can exist and
  function mechanically without a matching image.
- Derive the pair choice from historical acquired tier IDs in a deterministic,
  replay-safe way. Do not assume the acquired-tier array retains purchase order.
- Keep hybrid selection separate from the current ART-5 body precedence and
  bounded mutation cue overlays until its gameplay UX is reviewed.

## Quality and provenance

The WebPs are quality-88 lossy RGB with alpha-quality 100 and method 6. Original
polished PNGs are retained outside the repo in the art deliverable; no sources or
existing sprites are overwritten. These are polished illustrations, not rigged
animation frames. Their canvas dimensions do not imply identical silhouette
bounds or contact anchors.

Run `node --test table/battle-evolution-art.test.js` to verify every file and the
stable tier catalogue mapping.
