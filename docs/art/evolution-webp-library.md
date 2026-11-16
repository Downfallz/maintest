# Evolution WebP library

The owner-approved polished references are stored under `table/battle-art/evolutions/` as
WebP files. Most are 1536×1024; three Predator references have wider original canvases,
recorded explicitly in `table/battle-evolution-art.js` under stable illustration ids.
There is one registered image for each of the 21 Stage 1 to Stage 3 packages, plus
the ancestral Stage 0 creature (`base-creature.webp`), which no purchase draws.

They are live in the battle view since ADR 0151, as **lazily loaded illustrations**. The
four audited sprites of `table/battle-art.js` are still loaded eagerly and stay the fallback: a creature shows
its sprite until its illustration has loaded, and for good if it never does. The scene fetches only the
illustrations that creatures on the field resolve to.

Which package draws which illustration is authored beside the content, in `data/presentation/mutation-art.json`
(`artwork`, and `effect` for a capstone). It is not in the page, which names no package. Each registry entry in
`table/battle-evolution-art.js` records its canvas, its silhouette bounds (alpha >= 128, measured from the file)
and its ground anchor (the bottom centre of those bounds), so every stage is drawn to the same box.

## Adding visual hybridizations

- The dominant shape remains the Stage 3 tier's standalone image.
- Register only hand-approved variant pairs, e.g. `transcendent + necrotic`: one entry in `hybrids`, naming the
  two illustrations it combines (`dominant`, `hybrid`), its file under `evolutions/hybrids/`, its canvas and its
  measured bounds. Do not generate the Cartesian product of all purchased tiers. Serve the new file in
  `TableFiles.cs`.
- A missing pair resolves to the next eligible hybrid, then to the primary Stage 3 art. Mutations exist and
  function mechanically without a matching image.
- Which hybrid shows, and in which order, is decided by `table/battle-evolution.js` from the purchase log, never
  from the acquired-tier set's order (ADR 0151).

## Quality and provenance

The WebPs are quality-88 lossy RGB with alpha-quality 100 and method 6. Original
polished PNGs are retained outside the repo in the art deliverable; no sources or
existing sprites are overwritten. These are polished illustrations, not rigged
animation frames. Their canvas dimensions do not imply identical silhouette
bounds or contact anchors.

Run `node --test table/battle-evolution-art.test.js` to verify every file and the
stable tier catalogue mapping.
