import { readFile, readdir } from 'node:fs/promises';
import { gzipSync } from 'node:zlib';

// Decoded dimensions and device frame times are separate acceptance checks; these bound download size.
const limits = { initialArt: 2 * 1024 * 1024, liveBundle: 1600 * 1024, gzipBundle: 512 * 1024 };
const names = (await readdir('../table/arena-art')).filter(name => name.endsWith('.webp'));
const art = await Promise.all(names.map(async name => ({ name, bytes: (await readFile(`../table/arena-art/${name}`)).length })));
const bundle = await readFile('../table/arena-build/live.js');
const measured = { initialArt: art.reduce((sum, file) => sum + file.bytes, 0), liveBundle: bundle.length, gzipBundle: gzipSync(bundle).length };
for (const [key, bytes] of Object.entries(measured)) if (bytes > limits[key]) throw new Error(`${key}: ${bytes} exceeds ${limits[key]} bytes`);
console.log(JSON.stringify({ measured, limits, art }, null, 2));
