# Arena artwork

Current runtime art: [ART-3 integration](../docs/art/runtime-sprites.md) uses the four audited ART-2
references via `table/arena-art.js`, copied byte-for-byte under `table/arena-art/`. The scene loads
`base-creature.webp`, `brute.webp`, `predator.webp` and `warped.webp`, plus the eclipse background.
The original three-frame `creatures.webp` is retained below as historical provenance only.

## Historical prototype art

**Historical prototype only:** the current [creature art direction bible](../docs/art/creature-art-direction.md) supersedes the winged predator and robed wraith concepts below. Keep these asset briefs as provenance, not current approval.

Generated on 2026-11-03 with the built-in ImageGen tool for this isolated visual proof of concept.
The assets are provisional direction studies, not final catalogue creature identities or commissioned art.
Original PNGs were converted to WebP (quality 90, alpha quality 100); no other raster editing was applied.

| Asset | Use | Generation brief |
| --- | --- | --- |
| `table/arena-art/eclipse-arena.webp` | Cover-scaled battlefield | Wide 16:9 painted dark-fantasy ritual arena under a golden eclipse; ruined basalt architecture, teal mist, cracked stone foreground, low horizon, open floor for creatures; no characters, UI, lettering or logos. |
| `table/arena-art/creatures.webp` | Three equal-column transparent sprite frames | Three isolated full-body creatures facing slightly right: massive horned armored brute, skeletal winged predator, robed antler wraith; matching dark-fantasy painted treatment, equal columns and aligned feet; transparent background, no floor, shadows, UI, lettering or logos. |

The briefs above record art direction, rather than a guarantee of future reproducibility. The scene names
the frames `horns`, `wings` and `veil`; it mirrors and tints the enemy team. Transparent padding is retained
inside each equal frame. These are illustrations animated with transforms, not a skeletal animation rig.

The original prototype follow-up was to introduce a catalogue-to-presentation asset manifest with explicit anchors,
aspect ratios and fallbacks, then commission or approve distinct creature coverage. Keep game numbers,
labels and target affordances outside the art. Missing artwork must never block a legal action.
