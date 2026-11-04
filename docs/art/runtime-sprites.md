# ART-3 — Arena sprite integration

The approved base Creature, Brute, Predator and Warped now replace the prototype winged/robed sheet in
both arena scenes. See [ADR 0130](../adr/0130-map-arena-creatures-to-audited-sprites.md) for the selection
and fallback rules and [`table/arena-art.js`](../../table/arena-art.js) for the authoritative manifest.

## Assets and geometry

The four files in `table/arena-art/` are byte-identical copies of the [ART-2 references](asset-intake.md).
The audit's `referenceWebp.sha256` values remain their source hashes; a Node test checks every runtime copy.
All canvases remain 1536×1024. No original PNG, regenerated artwork, crop, alpha threshold or new RGB encode
is introduced in ART-3. A lower alpha maximum of 254 and faint edge traces remain recorded source limitations.

Ground points are derived from the alpha-128 bounds: the horizontal centre and the bottom row of each
principal silhouette, which is where every one of these figures stands.
The renderer scales width and height together, fits the alpha-128 silhouette into its accessible target,
and mirrors the anchor with the enemy image. These silhouettes are wider than tall and the targets are
nearly square, so the width decides the scale: a figure stands 39% (Predator on a desktop) to 81% (Warped on a phone)
of its slot height depending on the viewport, which a Node test keeps from shrinking further. Shadow/ring locations use the same placement as the HTML button.
Faint fringe pixels can extend past the principal bounds; they do not define a new clickable silhouette.

## Form selection

| Displayed acquired roots | Form |
| --- | --- |
| None, missing legacy data, or no recognized root | Base Creature |
| Brute root only | Brute |
| Predator root only (`tier:prowler:v1`) | Predator |
| Warped root only (`tier:occultist:v1`) | Warped |
| More than one recognized family | Base Creature pending ART-5 |

Later stages within one family keep its form. Changes appear after the authoritative snapshot arrives.
Historical action frames carry their own forms; old recordings without mutation fields use the base.
The neutral mixed-family fallback is provisional, not a visual claim that the creature lost its mutations.
Inspect the Mutation Tree for its actual build until ART-5 defines compositing/precedence.

## Contrast and acceptance

The eclipse backdrop is dimmed with a neutral tint; creature pixels retain their original palette on both
teams. Bone masks, limbs and silhouettes read against the ground, while gold rings and explicit tags show
acting/selected/target status independently. No team-wide red tint obscures the source colors.

Review six-creature captures at 1440×1000, 390×844 and 844×390 (also test 320px width), mirrored enemies,
active/target indicators and portrait/landscape rotation. Fine Warped tendrils and Predator claws naturally
lose detail on phones; names, stats and the full rectangular targets remain legible and usable. These are
static illustrations with transform animations, not skeletal rigs or final commissioned assets.

The documented ART-2 verifier checks the reference images. `node --test table/arena-art.test.js` additionally
checks runtime byte identity, mapping, replay and geometry. The normal arena browser suite covers rendering,
fallbacks and controls; the host's WebP tests check explicit binary routes. PNG source upload is independent
of this integration. The intake's `runtimeReady` flags are now true: these are the bytes the arena runs.
