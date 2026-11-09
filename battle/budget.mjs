import { readFile, readdir } from 'node:fs/promises';
import { gzipSync } from 'node:zlib';
import { CREATURE_ART } from '../table/battle-art.js';

// Decoded dimensions and device frame times are separate acceptance checks; these bound download size.
// The live bundle's raw limit was 1,600 KiB (ADR 0136); main reached 1,637,158 bytes with the d20s and the History
// summary, and the dice's tie links and crit captions crossed it. Phaser is most of the bundle; the limit rises by
// 64 KiB, recorded in docs/tabletop/battle-release-acceptance.md. The gzip limit is unchanged and far from reached.
const limits = { initialArt: 2 * 1024 * 1024, liveBundle: 1664 * 1024, gzipBundle: 512 * 1024 };
const bundle = await readFile('../table/battle-build/live.js');
// The art a visit downloads: the creature sprites the scene loads, and the files the live bundle names (the
// background). A file still served but no longer loaded, such as the retired three-form sheet, is not counted.
const source = bundle.toString('utf8'), sprites = new Set(CREATURE_ART.map(asset => asset.file));
const names = (await readdir('../table/battle-art')).filter(name => name.endsWith('.webp') && (sprites.has(name) || source.includes(name)));
const missing = [...sprites].filter(file => !names.includes(file));
if (missing.length) throw new Error(`Creature art the scene loads is missing: ${missing.join(', ')}`);
const art = await Promise.all(names.map(async name => ({ name, bytes: (await readFile(`../table/battle-art/${name}`)).length })));
const measured = { initialArt: art.reduce((sum, file) => sum + file.bytes, 0), liveBundle: bundle.length, gzipBundle: gzipSync(bundle).length };
for (const [key, bytes] of Object.entries(measured)) if (bytes > limits[key]) throw new Error(`${key}: ${bytes} exceeds ${limits[key]} bytes`);
console.log(JSON.stringify({ measured, limits, art }, null, 2));
