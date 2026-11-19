import { build } from 'esbuild';
import { createHash } from 'node:crypto';
import { copyFile, mkdir, readFile } from 'node:fs/promises';
import { benchSpells } from './bench-spells.mjs';
import { engineOrLaboratory, kitOnly, laboratoryOnly } from './live-guard.mjs';

// What the content says of each spell, for the effects bench (`bench-spells.mjs`): its name and what casts it.
// The laboratory's bundle alone carries it; the live page names no spell.
const spells = {
  name: 'bench-spells',
  setup(builder) {
    builder.onResolve({ filter: /^virtual:bench-spells$/ }, () => ({ path: 'bench-spells', namespace: 'bench-spells' }));
    builder.onLoad({ filter: /.*/, namespace: 'bench-spells' }, async () => ({ contents: JSON.stringify(await benchSpells('../data')), loader: 'json' }));
  },
};

await mkdir('../table/battle-build', { recursive: true });
const options = { bundle: true, minify: true, format: 'esm', target: 'es2022', outdir: '../table/battle-build', legalComments: 'linked', metafile: true };
const inputs = (result, name) => Object.keys(Object.entries(result.metafile.outputs).find(([path]) => path.endsWith(`/${name}.js`))?.[1].inputs ?? {});
// The effects kit first: a bundle of its own, which the other two fetch after their first frame (ADR 0196). It
// must hold no engine, and they must not hold it. They name it by the hash of what was built, so a cache cannot
// pair a page with another deploy's kit, and a kit that changed alone is fetched again.
const kit = await build({ ...options, entryPoints: { fx: 'src/fx/index.ts' } });
if (!inputs(kit, 'fx').length || inputs(kit, 'fx').some(engineOrLaboratory)) {
  throw new Error('The effects kit must not include the engine or the laboratory: it is handed the Phaser the page already holds.');
}
const tag = createHash('sha256').update(await readFile('../table/battle-build/fx.js')).digest('hex').slice(0, 12);
const result = await build({ ...options, entryPoints: { game: 'src/main.ts', live: 'src/live.ts' }, plugins: [spells], define: { __KIT_TAG__: JSON.stringify(tag) } });
// Enforce the separate live dependency graph at build time, including transitive imports.
const live = inputs(result, 'live');
if (!live.length || live.some(laboratoryOnly)) {
  throw new Error('The live battle must not include the laboratory sample, its reducer or the effects bench.');
}
if ([...live, ...inputs(result, 'game')].some(kitOnly)) {
  throw new Error('The effects kit loads after the first frame, as its own file: no other bundle may include it.');
}
await copyFile('node_modules/phaser/LICENSE.md', '../table/battle-build/PHASER-LICENSE.txt');
