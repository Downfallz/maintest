// What the live bundle may never carry: the laboratory's sample and reducer, and the effects bench with the
// spell art it lists. `build.mjs` reads every input of `live.js`, transitive imports included, through this.
const LABORATORY_ONLY = /battle-lab-(?:fixture|state)\.js$|battle-bench\.js$|(?:^|\/)bench\.ts$|spell-art\.json$/;
export const laboratoryOnly = path => LABORATORY_ONLY.test(path);
