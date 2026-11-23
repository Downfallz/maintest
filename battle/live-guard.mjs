// What the live bundle may never carry: the laboratory's sample and reducer, and the effects bench with the
// spell art and the spell names and mutations it lists. `build.mjs` reads every input of `live.js`, transitive imports
// included, through this.
const LABORATORY_ONLY = /battle-lab-(?:fixture|state)\.js$|battle-bench\.js$|(?:^|\/)bench\.ts$|spell-art\.json$|^bench-spells:/;
export const laboratoryOnly = path => LABORATORY_ONLY.test(path);
// What the effects kit may never carry: the same, and the engine. It is a small file because the page hands it
// the Phaser it already holds (ADR 0196).
export const engineOrLaboratory = path => laboratoryOnly(path) || /(?:^|\/)node_modules\//.test(path);
// The kit's own modules: no other bundle carries one, or the kit would be paid for in the first frame.
export const kitOnly = path => /(?:^|\/)src\/fx\//.test(path);
// Every page's pictures (table/page-art.js): the battle draws from its own, table/battle-page-art.js, which
// scripts/ux/pictures.mjs writes beside it, so no bundle pays for the showcase's and the lobby's entries (#1016).
export const everyPagesPictures = path => /(?:^|\/)table\/page-art\.js$/.test(path);
