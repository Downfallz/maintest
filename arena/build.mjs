import { build } from 'esbuild';
import { copyFile, mkdir } from 'node:fs/promises';
await mkdir('../table/arena-build', { recursive: true });
const result = await build({ entryPoints: { game: 'src/main.ts', live: 'src/live.ts' }, bundle: true, minify: true, format: 'esm', target: 'es2022',
  outdir: '../table/arena-build', legalComments: 'linked', metafile: true });
// Enforce the separate live dependency graph at build time, including transitive imports.
const live = Object.entries(result.metafile.outputs).find(([path]) => path.endsWith('/live.js'))?.[1];
if (!live || Object.keys(live.inputs).some(path => /arena-lab-(?:fixture|state)\.js$/.test(path))) {
  throw new Error('The live arena must not include the laboratory sample or reducer.');
}
await copyFile('node_modules/phaser/LICENSE.md', '../table/arena-build/PHASER-LICENSE.txt');
