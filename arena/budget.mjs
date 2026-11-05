import { readFile, readdir } from 'node:fs/promises';
import { gzipSync } from 'node:zlib';
import { CREATURE_ART } from '../table/arena-art.js';

// Decoded dimensions and device frame times are separate acceptance checks; these bound download size.
const limits = { initialArt: 2 * 1024 * 1024, liveBundle: 1600 * 1024, gzipBundle: 512 * 1024 };
const bundle = await readFile('../table/arena-build/live.js');
// The art a visit downloads: the creature sprites the scene loads, and the files the live bundle names (the
// background). A file still served but no longer loaded, such as the retired three-form sheet, is not counted.
const source = bundle.toString('utf8'), sprites = new Set(CREATURE_ART.map(asset => asset.file));
const names = (await readdir('../table/arena-art')).filter(name => name.endsWith('.webp') && (sprites.has(name) || source.includes(name)));
const missing = [...sprites].filter(file => !names.includes(file));
if (missing.length) throw new Error(`Creature art the scene loads is missing: ${missing.join(', ')}`);
const art = await Promise.all(names.map(async name => ({ name, bytes: (await readFile(`../table/arena-art/${name}`)).length })));
const measured = { initialArt: art.reduce((sum, file) => sum + file.bytes, 0), liveBundle: bundle.length, gzipBundle: gzipSync(bundle).length };
for (const [key, bytes] of Object.entries(measured)) if (bytes > limits[key]) throw new Error(`${key}: ${bytes} exceeds ${limits[key]} bytes`);
console.log(JSON.stringify({ measured, limits, art }, null, 2));
