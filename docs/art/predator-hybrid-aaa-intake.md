# Predator hybrid AAA WebP intake

Twelve owner-supplied remastered Predator hybrid portraits were converted to
WebP (`quality=88`, `method=6`, `alpha-quality=100`) and staged in
`table/battle-art/evolutions/hybrids/`.

| Primary Stage 3 form | Portrait variants | Files |
| --- | ---: | --- |
| Soulreaver | 4 | `predator-soulreaver-01.webp` through `-04.webp` |
| Deathstalker | 4 | `predator-deathstalker-05.webp` through `-08.webp` |
| Blightweaver | 4 | `predator-blightweaver-09.webp` through `-12.webp` |

These images are recorded in `table/battle-predator-hybrid-art.js` with stable
relative paths and canvas dimensions, each naming the Stage 3 illustration it varies
by its page id (`predator-stage3-soulreaver`, ...): the page ships no content id, and
`data/presentation/mutation-art.json` already maps each Stage 3 tier to that id. The
host serves the module like every module beside the page. Variant numbers reflect the order in the
approved art batch; they **do not imply a particular secondary tier**. A
secondary-tier-to-variant mapping must be approved before any image is selected
by purchased mutation IDs.

The current combat sprite manifest, selection logic and eager-loading budget
remain unchanged. These illustrations are not rigged animation sprites. Their
alpha edges and contact anchors still require gameplay-scene QA before runtime
activation, especially on the wide `09` source that contains edge artifacts.

Validation: `node --test table/battle-predator-hybrid-art.test.js`.
