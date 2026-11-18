import { build } from 'esbuild';
import { copyFile, mkdir } from 'node:fs/promises';
await mkdir('../table/battle-build', { recursive: true });
const result = await build({ entryPoints: { game: 'src/main.ts', live: 'src/live.ts' }, bundle: true, minify: true, format: 'esm', target: 'es2022',
  outdir: '../table/battle-build', legalComments: 'linked', metafile: true });
// Enforce the separate live dependency graph at build time, including transitive imports.
const live = Object.entries(result.metafile.outputs).find(([path]) => path.endsWith('/live.js'))?.[1];
// The effects bench is the laboratory's too: its modules and the spell art it lists stay out of the live bundle.
if (!live || Object.keys(live.inputs).some(path => /battle-lab-(?:fixture|state)\.js$|battle-bench\.js$|\/bench\.ts$|spell-art\.json$/.test(path))) {
  throw new Error('The live battle must not include the laboratory sample, its reducer or the effects bench.');
}
await copyFile('node_modules/phaser/LICENSE.md', '../table/battle-build/PHASER-LICENSE.txt');
