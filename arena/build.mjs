import { build } from 'esbuild';
import { copyFile, mkdir } from 'node:fs/promises';
await mkdir('../table/arena-build', { recursive: true });
await build({ entryPoints: ['src/main.ts'], bundle: true, minify: true, format: 'esm', target: 'es2022',
  outfile: '../table/arena-build/game.js', legalComments: 'linked' });
await copyFile('node_modules/phaser/LICENSE.md', '../table/arena-build/PHASER-LICENSE.txt');
