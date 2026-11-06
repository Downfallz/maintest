# ART-3 — Battle sprite integration

The approved base Creature, Brute, Predator and Warped now replace the prototype winged/robed sheet in
both battle scenes. See [ADR 0130](../adr/0130-map-arena-creatures-to-audited-sprites.md) for the selection
and fallback rules and [`table/battle-art.js`](../../table/battle-art.js) for the authoritative manifest.

## Assets and geometry

The four files in `table/battle-art/` are byte-identical copies of the [ART-2 references](asset-intake.md).
The audit's `referenceWebp.sha256` values remain their source hashes; a Node test checks every runtime copy.
All canvases remain 1536×1024. No original PNG, regenerated artwork, crop, alpha threshold or new RGB encode
is introduced in ART-3. A lower alpha maximum of 254 and faint edge traces remain recorded source limitations.

Ground points centre each principal silhouette horizontally and meet its lowest substantial contact pixel.
The renderer scales width and height together, fits the alpha-128 silhouette into its accessible target,
and mirrors the anchor with the enemy image. Shadow/ring locations use the same placement as the HTML button.
Faint fringe pixels can extend past the principal bounds; they do not define a new clickable silhouette.

## Form selection

| Displayed acquired roots | Form |
| --- | --- |
| None, missing legacy data, or no recognized root | Base Creature |
| Brute root only | Brute |
| Predator root only (`tier:prowler:v1`) | Predator |
| Warped root only (`tier:occultist:v1`) | Warped |
| More than one recognized family | ART-5 ranks stage, unique investment, then fixed family order |

Later stages within one family keep its form. Changes appear after the authoritative snapshot arrives.
Historical action frames carry their own forms; old recordings without mutation fields use the base.
ART-5 supersedes the original neutral mixed-family fallback with [explicit precedence and bounded cues](mutation-cues.md).
Inspect the Mutation Tree for the complete build; the bounded art layer does not depict every acquired branch.

## Contrast and acceptance

The eclipse backdrop is dimmed with a neutral tint; creature pixels retain their original palette on both
teams. Bone masks, limbs and silhouettes read against the ground, while gold rings and explicit tags show
acting/selected/target status independently. No team-wide red tint obscures the source colors.

Review six-creature captures at 1440×1000, 390×844 and 844×390 (also test 320px width), mirrored enemies,
active/target indicators and portrait/landscape rotation. Fine Warped tendrils and Predator claws naturally
lose detail on phones; names, stats and the full rectangular targets remain legible and usable. These are
static illustrations with transform animations, not skeletal rigs or final commissioned assets.

The documented ART-2 verifier checks the reference images. `node --test table/battle-art.test.js` additionally
checks runtime byte identity, mapping, replay and geometry. The normal battle browser suite covers rendering,
fallbacks and controls; the host's WebP tests check explicit binary routes. PNG source upload is independent
of this integration. Runtime validation does not change the historical intake's `runtimeReady: false` field.
