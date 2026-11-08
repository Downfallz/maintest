# Warped hybrid AAA WebP intake

Twelve owner-supplied Warped hybrid portraits are staged under
`table/battle-art/evolutions/hybrids/`, encoded as WebP at quality 88,
method 6 and alpha quality 100. No source PNGs are included in this change.

| Primary Stage 3 form | Portraits | Files |
| --- | ---: | --- |
| Revenant | 4 | `warped-revenant-01.webp` through `-04.webp` |
| Transcendent | 4 | `warped-transcendent-05.webp` through `-08.webp` |
| Cataclysm | 4 | `warped-cataclysm-09.webp` through `-12.webp` |

The `table/battle-warped-hybrid-art.js` intake catalog records the images' stable
paths, primary Stage 3 page illustration ids and original canvas sizes. The
numbers follow the owner's displayed batch order, not a finalized mutation
influence order. Revenant `03` uses its original 1536 × 784 canvas; the other
eleven are 1536 × 1024. These sources have opaque black backgrounds, not
transparent cutout alpha; alpha cleanup and silhouette/anchor QA remain to do
before the portraits can be enabled as field sprites.

## Source correspondence

| Portrait | Original uploaded PNG |
| --- | --- |
| Revenant 01–04 | `1000011942.png`, `1000011943.png`, `1000011946.png`, `1000011947.png` |
| Transcendent 05–08 | `1000011948.png`, `1000011949.png`, `1000011954.png`, `1000011955.png` |
| Cataclysm 09–12 | `1000011956.png`, `1000011957.png`, `1000011922.png`, `1000011923.png` |

## Activation deliberately deferred

The Predator precedent separated asset intake (#495) from the later approved
influence mapping and display behavior (#497). Likewise, this change does not
modify `battle-evolution-art.js`, mutation resolution, combat loading or the
host's module routing. In particular, which three influences and Ultimate are
represented within each Warped line has not been confirmed by the owner. Do
not infer it merely from numbering or visible colors.

Validation: `node --test table/battle-warped-hybrid-art.test.js`.
