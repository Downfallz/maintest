#!/usr/bin/env node
// The picture kit's script (ADR 0198, docs/art/page-pictures.md). A page's pictures are declared in
// docs/art/page-pictures.json: a painting of the library, or the frames of a strip, and the width it is drawn at.
// This derives each at the size it is drawn, once and twice, into table/page-art/, and writes table/page-art.js,
// the module table/picture.js and the showcase's build read. Derived once and committed: the gate reads the
// committed files (--check, studio/ux-pictures.test.js) and encodes nothing.
//
//   node scripts/ux/pictures.mjs            derive what changed (needs python3 with Pillow)
//   node scripts/ux/pictures.mjs --check    hold the committed files against the declaration
//   node scripts/ux/pictures.mjs --budget   each page's bytes, as a phone takes them, against its budget
//   node scripts/ux/pictures.mjs --asked    the pictures the briefs ask of the owner, each with its prompt
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { mkdirSync, mkdtempSync, readdirSync, readFileSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const ROOT = new URL('../../', import.meta.url);
export const DECLARATION = 'docs/art/page-pictures.json', BUDGET = 'docs/art/page-budget.json';
export const FOLDER = 'table/page-art', MODULE = 'table/page-art.js', BRIEFS = 'docs/art/journeys';
const ROLES = ['largest', 'first', 'lazy'];
const ASKED = 'asked of the owner';

/** The widths a picture is sent at: each width it is drawn at, once and twice, never past its painting. */
export function widthsOf(drawn, sourceWidth) {
  const all = [...new Set(drawn.flatMap(width => [width, width * 2]).map(width => Math.min(Math.round(width), sourceWidth)))].sort((a, b) => a - b);
  // Two files a tenth apart are one file sent twice: the larger serves both.
  return all.filter((width, index) => index === all.length - 1 || all[index + 1] > width * 1.1);
}

/** A WebP file's width and height, read from its header: the lossy, the lossless and the extended form. */
export function webpSize(bytes) {
  if (bytes.length < 25 || bytes.toString('latin1', 0, 4) !== 'RIFF' || bytes.toString('latin1', 8, 12) !== 'WEBP') throw new Error('not a WebP file');
  const form = bytes.toString('latin1', 12, 16), at = 20;
  if (form === 'VP8 ') return { width: bytes.readUInt16LE(at + 6) & 0x3fff, height: bytes.readUInt16LE(at + 8) & 0x3fff };
  if (form === 'VP8L') { const bits = bytes.readUInt32LE(at + 1); return { width: (bits & 0x3fff) + 1, height: ((bits >>> 14) & 0x3fff) + 1 }; }
  if (form === 'VP8X') return { width: bytes.readUIntLE(at + 4, 3) + 1, height: bytes.readUIntLE(at + 7, 3) + 1 };
  throw new Error('not a WebP file');
}

const sha = bytes => createHash('sha256').update(bytes).digest('hex');

/** A derived file's name: its own bytes are in it, since the host lets a browser keep a picture for a day. */
export const fileName = (name, width, bytes) => `${name}-${width}.${sha(bytes).slice(0, 8)}.webp`;

/** What a picture was derived from: its declaration, its paintings' bytes and the encoding. A change to any is a new origin. */
export function origin(picture, paintings, encoding) {
  const hash = createHash('sha256').update(JSON.stringify([picture.name, picture.source ?? picture.frames, picture.drawn, encoding]));
  for (const painting of paintings) hash.update(sha(painting));
  return hash.digest('hex').slice(0, 16);
}

/** The file a phone takes: the smallest that fills the width drawn, at the screen's density; a strip is its frames side by side. */
export function phoneSize(sizes, drawn, density, frames = 1) {
  return sizes.find(size => size.width >= drawn * density * frames) ?? sizes.at(-1);
}

/** A page's bytes of pictures as a phone takes them: those asked for before any scroll, all of them, and how many are asked for first. */
export function budgetOf(art, page, density) {
  const sum = { first: 0, all: 0, largest: 0 };
  for (const picture of Object.values(art)) {
    const role = picture.pages?.[page];
    if (!role) continue;
    const { bytes } = phoneSize(picture.sizes, picture.drawn[0], density, picture.frames);
    sum.all += bytes;
    if (role !== 'lazy') sum.first += bytes;
    if (role === 'largest') sum.largest += 1;
  }
  return sum;
}

const paintingsOf = picture => picture.frames ?? [picture.source];

function declared(picture) {
  const problems = [], name = String(picture.name);
  if (!/^[a-z0-9]+(-[a-z0-9]+)*$/.test(name)) problems.push(`${name}: a name is lower case, digits and hyphens, as its files are named`);
  if (picture.frames ? picture.source !== undefined : typeof picture.source !== 'string') problems.push(`${name}: one painting as its source, or the frames of a strip`);
  if (picture.frames && !(Array.isArray(picture.frames) && picture.frames.length >= 2)) problems.push(`${name}: a strip has at least two frames`);
  if (!(Array.isArray(picture.drawn) && picture.drawn.length && picture.drawn.every(width => Number.isInteger(width) && width > 0)))
    problems.push(`${name}: the width it is drawn at, in whole pixels, the phone's first`);
  for (const [page, role] of Object.entries(picture.pages ?? {})) if (!ROLES.includes(role)) problems.push(`${name}: on ${page} it is largest, first or lazy, not ${role}`);
  return problems;
}

/**
 * The committed pictures held against the declaration. `read(path)` gives a repository file's bytes and `list()`
 * the names in table/page-art. Returns what is wrong, in words; nothing is encoded.
 */
export function check({ authored, art, read, list }) {
  const problems = [], expected = new Set(), files = list();
  for (const picture of authored.pictures) {
    const wrong = declared(picture);
    problems.push(...wrong);
    if (wrong.length) continue;
    const entry = art[picture.name];
    if (!entry) { problems.push(`${picture.name} is declared but not derived: run node scripts/ux/pictures.mjs`); continue; }
    let paintings;
    try { paintings = paintingsOf(picture).map(read); } catch { problems.push(`${picture.name}: a painting it names is not in the repository`); continue; }
    if (entry.from !== origin(picture, paintings, authored.encoding)) problems.push(`${picture.name}: its painting or its declaration changed since it was derived: run node scripts/ux/pictures.mjs`);
    if (JSON.stringify(entry.pages) !== JSON.stringify(picture.pages ?? {}) || JSON.stringify(entry.drawn) !== JSON.stringify(picture.drawn) || entry.frames !== paintingsOf(picture).length)
      problems.push(`${picture.name}: the module does not say what the declaration says: run node scripts/ux/pictures.mjs`);
    for (const size of entry.sizes) {
      expected.add(size.file);
      if (!files.includes(size.file)) { problems.push(`${picture.name}: ${size.file} is missing from ${FOLDER}`); continue; }
      const bytes = read(`${FOLDER}/${size.file}`), actual = webpSize(bytes);
      if (size.file !== fileName(picture.name, size.width, bytes)) problems.push(`${size.file} is not named by its own bytes`);
      if (bytes.length !== size.bytes || actual.width !== size.width || actual.height !== size.height)
        problems.push(`${size.file}: the module says ${size.width} by ${size.height}, ${size.bytes} bytes; the file is ${actual.width} by ${actual.height}, ${bytes.length}`);
    }
  }
  const names = new Set(authored.pictures.map(picture => picture.name));
  for (const name of Object.keys(art)) if (!names.has(name)) problems.push(`${name} is derived but not declared`);
  for (const file of files) if (!expected.has(file)) problems.push(`${FOLDER}/${file} is no picture of the declaration`);
  return problems;
}

/** The text of table/page-art.js. */
export function moduleText(art) {
  const entries = Object.entries(art).map(([name, entry]) => {
    const sizes = entry.sizes.map(size => `      { file: '${size.file}', width: ${size.width}, height: ${size.height}, bytes: ${size.bytes} },`).join('\n');
    return `  '${name}': {\n    from: '${entry.from}', frames: ${entry.frames}, drawn: ${JSON.stringify(entry.drawn)}, pages: ${JSON.stringify(entry.pages)},\n    sizes: [\n${sizes}\n    ],\n  },`;
  });
  return ['// Generated by scripts/ux/pictures.mjs from docs/art/page-pictures.json: never edited by hand (ADR 0198).',
    '// The pictures a page draws, each at the sizes it is sent: `drawn` is the width it is drawn at, the phone\'s first;',
    '// `frames` is over 1 for a strip, its frames side by side; `pages` says where it is asked for first.',
    'export const PAGE_ART = {', ...entries, '};', ''].join('\n');
}

/** The pictures a brief asks of the owner: the row of its Pictures table, and the prompt in the fenced block after it. */
export function asked(markdown, brief) {
  const requests = [], lines = markdown.split('\n');
  let screen = null, waiting = [], fence = null;
  for (const line of lines) {
    if (fence) {
      if (line.startsWith('```')) { const request = waiting.shift(); if (request) requests.push({ ...request, prompt: fence.join('\n') }); fence = null; }
      else fence.push(line);
    } else if (line.startsWith('```')) fence = [];
    else if (/^## /.test(line)) { screen = line.slice(3).replace(/\s*\(.*$/, '').trim(); waiting = []; }
    else if (line.startsWith('|') && line.includes(ASKED)) {
      const cells = line.split('|').slice(1, -1).map(cell => cell.trim());
      waiting.push({ brief, screen, shows: cells[0], drawn: cells[1], holds: /`([^`]+\.\w+)`/.exec(cells.at(-1))?.[1] ?? null });
    }
  }
  return requests;
}

const read = path => readFileSync(new URL(path, ROOT));
const json = path => JSON.parse(read(path).toString('utf8'));
const committed = async () => (await import(`${new URL(MODULE, ROOT).href}?at=${Date.now()}`)).PAGE_ART;
const list = () => { try { return readdirSync(new URL(FOLDER, ROOT)); } catch { return []; } };

/** Derives what the declaration asks for and the folder lacks. Encoding is Pillow's, through scripts/ux/encode.py. */
async function derive() {
  const authored = json(DECLARATION), before = await committed().catch(() => ({}));
  const wrong = authored.pictures.flatMap(declared);
  if (wrong.length) throw new Error(wrong.join('\n'));
  const art = {}, work = mkdtempSync(join(tmpdir(), 'page-art-'));
  mkdirSync(new URL(FOLDER, ROOT), { recursive: true });
  try {
    for (const picture of authored.pictures) {
      const paintings = paintingsOf(picture), bytes = paintings.map(read), from = origin(picture, bytes, authored.encoding);
      const kept = before[picture.name];
      if (kept?.from === from && kept.sizes.every(size => list().includes(size.file))) { art[picture.name] = { ...kept, pages: picture.pages ?? {}, drawn: picture.drawn }; continue; }
      const widths = widthsOf(picture.drawn, Math.min(...bytes.map(one => webpSize(one).width)));
      const jobs = widths.map(width => ({ sources: paintings.map(path => fileURLToPath(new URL(path, ROOT))), width, out: join(work, `${picture.name}-${width}.webp`), ...authored.encoding }));
      const ran = spawnSync('python3', [fileURLToPath(new URL('encode.py', import.meta.url))], { input: JSON.stringify(jobs), encoding: 'utf8' });
      if (ran.status !== 0) throw new Error(`python3 scripts/ux/encode.py failed (it needs Pillow: python3 -m pip install pillow)\n${ran.stderr ?? ran.error}`);
      const sizes = jobs.map(job => {
        // A strip's file is its frames side by side: its width is all of them, and that is what names it.
        const made = readFileSync(job.out), size = webpSize(made), file = fileName(picture.name, size.width, made);
        renameSync(job.out, new URL(`${FOLDER}/${file}`, ROOT));
        return { file, ...size, bytes: made.length };
      });
      art[picture.name] = { from, frames: paintings.length, drawn: picture.drawn, pages: picture.pages ?? {}, sizes };
      console.log(`${picture.name}: ${sizes.map(size => `${size.width} px, ${size.bytes} bytes`).join('; ')}`);
    }
    const wanted = new Set(Object.values(art).flatMap(entry => entry.sizes.map(size => size.file)));
    for (const file of list()) if (!wanted.has(file)) { rmSync(new URL(`${FOLDER}/${file}`, ROOT)); console.log(`removed ${file}`); }
    writeFileSync(new URL(MODULE, ROOT), moduleText(art));
  } finally { rmSync(work, { recursive: true, force: true }); }
}

function printBudget(art) {
  const budget = json(BUDGET);
  let over = false;
  for (const [page, limit] of Object.entries(budget.pages)) {
    const measured = budgetOf(art, page, budget.density);
    over ||= measured.first > limit.first || measured.all > limit.all;
    console.log(`${page}: ${measured.first} of ${limit.first} bytes before any scroll, ${measured.all} of ${limit.all} in all, ${measured.largest} asked for first`);
  }
  return over;
}

function printAsked() {
  const folder = new URL(`${BRIEFS}/`, ROOT);
  const requests = readdirSync(folder).filter(name => name.endsWith('.md')).flatMap(name => asked(readFileSync(new URL(name, folder), 'utf8'), `${BRIEFS}/${name}`));
  if (!requests.length) console.log('No brief asks the owner for a picture.');
  requests.forEach((one, index) => console.log(`${index + 1}. ${one.shows}\n   ${one.brief}, "${one.screen}", drawn at ${one.drawn}; ${one.holds ?? 'nothing'} holds its place.\n\n${one.prompt}\n`));
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  const mode = process.argv[2];
  if (mode === '--asked') printAsked();
  else if (mode === '--budget') process.exitCode = printBudget(await committed()) ? 1 : 0;
  else if (mode === '--check') {
    const problems = check({ authored: json(DECLARATION), art: await committed(), read, list });
    for (const problem of problems) console.error(problem);
    process.exitCode = problems.length ? 1 : 0;
  } else if (mode === undefined) { await derive(); printBudget(await committed()); }
  else { console.error('Usage: node scripts/ux/pictures.mjs [--check | --budget | --asked]'); process.exitCode = 2; }
}
