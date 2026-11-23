#!/usr/bin/env node
// The picture kit's script (ADR 0198, docs/art/page-pictures.md). A page's pictures are declared in
// docs/art/page-pictures.json: a painting of the library, or the frames of a strip, and the width it is drawn at.
// This derives each at the size it is drawn, once and twice, into table/page-art/, and writes table/page-art.js,
// the module table/picture.js and the showcase's build read, and table/battle-page-art.js, the battle's pictures
// alone, which the battle bundle carries instead of every page's (#1016). Derived once and committed: the gate reads the
// committed files (--check, studio/ux-pictures.test.js) and encodes nothing.
//
//   node scripts/ux/pictures.mjs            derive what changed (needs python3 with Pillow)
//   node scripts/ux/pictures.mjs --check    hold the committed files against the declaration
//   node scripts/ux/pictures.mjs --budget   each page's bytes, as a phone takes them, against its budget
//   node scripts/ux/pictures.mjs --asked    the pictures the briefs ask of the owner, each with its prompt
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { copyFileSync, mkdirSync, mkdtempSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { focusAttributes } from '../../table/picture-focus.js';

const ROOT = new URL('../../', import.meta.url);
export const DECLARATION = 'docs/art/page-pictures.json', BUDGET = 'docs/art/page-budget.json';
export const FOLDER = 'table/page-art', MODULE = 'table/page-art.js', BRIEFS = 'docs/art/journeys';
export const BATTLE_MODULE = 'table/battle-page-art.js', BATTLE_PAGE = 'table/battle';
const ROLES = new Set(['largest', 'first', 'lazy', 'wide']);
const ASKED = 'asked of the owner';

/** The widths a picture is sent at: each width it is drawn at, once and twice, never past its painting. */
export function widthsOf(drawn, sourceWidth) {
  const all = [...new Set(drawn.flatMap(width => [width, width * 2]).map(width => Math.min(Math.round(width), sourceWidth)))].sort((a, b) => a - b);
  // Two files a tenth apart are one file sent twice: the larger serves both.
  return all.filter((width, index) => index === all.length - 1 || all[index + 1] > width * 1.1);
}

/** A WebP file's width and height, read from its header: the lossy, the lossless and the extended form. */
export function webpSize(bytes) {
  const form = bytes.toString('latin1', 12, 16), at = 20, needs = { 'VP8 ': 30, VP8L: 25, VP8X: 30 }[form];
  if (bytes.toString('latin1', 0, 4) !== 'RIFF' || bytes.toString('latin1', 8, 12) !== 'WEBP' || !needs || bytes.length < needs) throw new Error('not a WebP file');
  if (form === 'VP8 ') return { width: bytes.readUInt16LE(at + 6) & 0x3fff, height: bytes.readUInt16LE(at + 8) & 0x3fff };
  if (form === 'VP8L') { const bits = bytes.readUInt32LE(at + 1); return { width: (bits & 0x3fff) + 1, height: ((bits >>> 14) & 0x3fff) + 1 }; }
  return { width: bytes.readUIntLE(at + 4, 3) + 1, height: bytes.readUIntLE(at + 7, 3) + 1 };
}

/** A painting's width and height: the owner hands a PNG or a WebP over; what is derived from it is always WebP. */
export function paintingSize(bytes) {
  const png = bytes.length >= 24 && bytes.readUInt32BE(0) === 0x89504e47 && bytes.toString('latin1', 12, 16) === 'IHDR';
  return png ? { width: bytes.readUInt32BE(16), height: bytes.readUInt32BE(20) } : webpSize(bytes);
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

/** The widths a page draws a picture at on the phone: those its `phone` names, or the first it is drawn at. */
export const phoneWidths = (picture, page) => picture.phone?.[page] ?? [picture.drawn[0]];

/**
 * A page's bytes of pictures as a phone takes them: those asked for before any scroll, all of them, and how many
 * are asked for first. A picture the page draws at two widths counts each file it takes, once; before any scroll,
 * only the file of its first width. Pictures of one
 * `group` on the page are drawn one at a time: the group counts as its heaviest member, not their sum (#993).
 */
export function budgetOf(art, page, density) {
  const sum = { first: 0, all: 0, largest: 0 }, groups = new Map();
  for (const picture of Object.values(art)) {
    const role = picture.pages?.[page];
    if (!role || role === 'wide') continue;
    const [screen, ...below] = phoneWidths(picture, page).map(width => phoneSize(picture.sizes, width, density, picture.frames));
    const bytes = [...new Set([screen, ...below])].reduce((total, size) => total + size.bytes, 0);
    // Where it is on the first screen, the first width of `phone` is that use; the others are drawn further down,
    // with `lazy`, and wait until they are near (#1082).
    const counted = { first: role === 'lazy' ? 0 : screen.bytes, all: bytes, largest: role === 'largest' ? 1 : 0 };
    const group = picture.group?.[page];
    if (!group) { for (const key of Object.keys(sum)) sum[key] += counted[key]; continue; }
    const held = groups.get(group) ?? { first: 0, all: 0, largest: 0 };
    groups.set(group, Object.fromEntries(Object.keys(held).map(key => [key, Math.max(held[key], counted[key])])));
  }
  for (const held of groups.values()) for (const key of Object.keys(sum)) sum[key] += held[key];
  return sum;
}

const paintingsOf = picture => picture.frames ?? [picture.source];
const phoneOf = picture => (picture.phone ? { phone: picture.phone } : {});
const groupOf = picture => (picture.group ? { group: picture.group } : {});
// Where CSS cuts the painting is carried to the module and is no part of its files: changing it derives nothing.
const focusOf = picture => (picture.focus === undefined ? {} : { focus: picture.focus });
const carried = picture => ({ ...phoneOf(picture), ...groupOf(picture), ...focusOf(picture) });

/** A declaration's `focus`: two percentages a box that cuts the painting centres on; a strip is never cut. */
function focusProblems(picture, name) {
  if (!('focus' in picture)) return [];
  if (picture.frames) return [`${name}: a strip is never cut, and takes no focus`];
  try { focusAttributes(picture.focus); return []; } catch (error) { return [`${name}: ${error.message}`]; }
}

function declared(picture) {
  const problems = [], name = String(picture.name);
  if (!/^[a-z0-9]+(-[a-z0-9]+)*$/.test(name)) problems.push(`${name}: a name is lower case, digits and hyphens, as its files are named`);
  if (picture.frames ? picture.source !== undefined : typeof picture.source !== 'string') problems.push(`${name}: one painting as its source, or the frames of a strip`);
  if (picture.frames && !(Array.isArray(picture.frames) && picture.frames.length >= 2)) problems.push(`${name}: a strip has at least two frames`);
  if (!(Array.isArray(picture.drawn) && picture.drawn.length && picture.drawn.every(width => Number.isInteger(width) && width > 0)))
    problems.push(`${name}: the width it is drawn at, in whole pixels, the phone's first`);
  for (const [page, role] of Object.entries(picture.pages ?? {})) if (!ROLES.has(role)) problems.push(`${name}: on ${page} it is largest, first, lazy or wide, not ${role}`);
  return [...problems, ...phoneProblems(picture, name), ...groupProblems(picture, name), ...focusProblems(picture, name)];
}

/** A declaration's `group`: for each page that draws one picture of a set at a time, the set's name. */
function groupProblems(picture, name) {
  if (!('group' in picture)) return [];
  const group = picture.group;
  if (!group || typeof group !== 'object' || Array.isArray(group)) return [`${name}: group gives each page the set it is drawn one of`];
  return Object.entries(group).flatMap(([page, set]) => {
    if (!picture.pages?.[page]) return [`${name}: group names ${page}, a page that does not draw it`];
    return typeof set === 'string' && /^[a-z0-9]+(-[a-z0-9]+)*$/.test(set) ? [] : [`${name}: on ${page} its group is a name in lower case, digits and hyphens`];
  });
}

/** A group a page draws one of at a time has two members or more: one alone is no choice. */
function lonelyGroups(pictures) {
  const members = new Map();
  for (const picture of pictures) for (const [page, set] of Object.entries(picture.group ?? {})) members.set(`${page} ${set}`, [...(members.get(`${page} ${set}`) ?? []), picture.name]);
  return [...members].filter(([, names]) => names.length < 2).map(([key, names]) => `${names[0]}: on ${key.split(' ')[0]} it is the only picture of the group ${key.split(' ')[1]}`);
}

/** A declaration's `phone`: for each page that draws the picture, one width or more, each listed in `drawn`. */
function phoneProblems(picture, name) {
  if (!('phone' in picture)) return [];
  const phone = picture.phone;
  if (!phone || typeof phone !== 'object' || Array.isArray(phone)) return [`${name}: phone gives each page the widths it draws`];
  return Object.entries(phone).flatMap(([page, widths]) => {
    if (!picture.pages?.[page]) return [`${name}: phone names ${page}, a page that does not draw it`];
    if (!Array.isArray(widths) || !widths.length) return [`${name}: on ${page} it is drawn at one width or more`];
    return widths.filter(width => !picture.drawn?.includes(width)).map(width => `${name}: on ${page} it is drawn at ${width} px, a width its drawn does not list`);
  });
}

/** One derived file held against what the module says of it. */
function fileProblems(name, size, files, read) {
  if (!files.includes(size.file)) return [`${name}: ${size.file} is missing from ${FOLDER}`];
  const problems = [], bytes = read(`${FOLDER}/${size.file}`), actual = webpSize(bytes);
  if (size.file !== fileName(name, size.width, bytes)) problems.push(`${size.file} is not named by its own bytes`);
  if (bytes.length !== size.bytes || actual.width !== size.width || actual.height !== size.height)
    problems.push(`${size.file}: the module says ${size.width} by ${size.height}, ${size.bytes} bytes; the file is ${actual.width} by ${actual.height}, ${bytes.length}`);
  return problems;
}

/** One declared picture held against its entry of the module, its paintings and its files. */
function pictureProblems(picture, entry, { authored, read, files }) {
  const again = 'run node scripts/ux/pictures.mjs';
  if (!entry) return [`${picture.name} is declared but not derived: ${again}`];
  let paintings, widths;
  try { paintings = paintingsOf(picture).map(read); } catch { return [`${picture.name}: a painting it names is not in the repository`]; }
  const problems = [], frames = paintings.length, same = (one, other) => JSON.stringify(one) === JSON.stringify(other);
  if (entry.from !== origin(picture, paintings, authored.encoding)) problems.push(`${picture.name}: its painting or its declaration changed since it was derived: ${again}`);
  if (!same(entry.pages, picture.pages ?? {}) || !same(entry.drawn, picture.drawn) || !same(entry.phone, picture.phone) || !same(entry.group, picture.group) || entry.focus !== picture.focus || entry.frames !== frames)
    problems.push(`${picture.name}: the module does not say what the declaration says: ${again}`);
  try { widths = widthsOf(picture.drawn, Math.min(...paintings.map(one => paintingSize(one).width))); } catch { return [...problems, `${picture.name}: a painting it names is neither a PNG nor a WebP file`]; }
  const sent = entry.sizes.map(size => size.width / frames);
  if (!same(sent, widths)) problems.push(`${picture.name}: it is sent at ${widths.join(', ')} px, and the module names ${sent.join(', ')}: ${again}`);
  return [...problems, ...entry.sizes.flatMap(size => fileProblems(picture.name, size, files, read))];
}

/**
 * The committed pictures held against the declaration. `read(path)` gives a repository file's bytes and `list()`
 * the names in table/page-art. Returns what is wrong, in words; nothing is encoded.
 */
export function check({ authored, art, read, list }) {
  const problems = [], files = list(), seen = new Set();
  for (const picture of authored.pictures) {
    const wrong = declared(picture);
    if (seen.has(picture.name)) wrong.push(`${picture.name} is declared twice`);
    seen.add(picture.name);
    problems.push(...(wrong.length ? wrong : pictureProblems(picture, art[picture.name], { authored, read, files })));
  }
  problems.push(...lonelyGroups(authored.pictures));
  for (const name of Object.keys(art)) if (!seen.has(name)) problems.push(`${name} is derived but not declared`);
  const expected = new Set(authored.pictures.flatMap(picture => art[picture.name]?.sizes.map(size => size.file) ?? []));
  for (const file of files) if (!expected.has(file)) problems.push(`${FOLDER}/${file} is no picture of the declaration`);
  return problems;
}

function entriesText(art) {
  return Object.entries(art).map(([name, entry]) => {
    const sizes = entry.sizes.map(size => `      { file: '${size.file}', width: ${size.width}, height: ${size.height}, bytes: ${size.bytes} },`).join('\n');
    const phone = entry.phone ? ` phone: ${JSON.stringify(entry.phone)},` : '';
    const group = entry.group ? ` group: ${JSON.stringify(entry.group)},` : '';
    const focus = entry.focus === undefined ? '' : ` focus: '${entry.focus}',`;
    const from = entry.from === undefined ? '' : `from: '${entry.from}', `;
    return `  '${name}': {\n    ${from}frames: ${entry.frames}, drawn: ${JSON.stringify(entry.drawn)}, pages: ${JSON.stringify(entry.pages)},${phone}${group}${focus}\n    sizes: [\n${sizes}\n    ],\n  },`;
  });
}

/** The pictures the battle page draws, whole: their entries are written once, in table/battle-page-art.js (#1016). */
export const battleArt = art => Object.fromEntries(Object.entries(art).filter(([, entry]) => entry.pages[BATTLE_PAGE]));

const LEGEND = ['// The pictures a page draws, each at the sizes it is sent: `drawn` is the width it is drawn at, the phone\'s first;',
  '// `frames` is over 1 for a strip, its frames side by side; `pages` says where it is asked for first; `phone`, the',
  '// widths a page draws it at on the phone when it draws more than the first; `group`, the set a page draws it one',
  '// of at a time; `focus`, where a box that cuts it centres the cut.'];

/**
 * The text of table/page-art.js: every page's pictures. Those the battle draws are its module's, imported, so each
 * entry is written once and the battle bundle carries no other page's.
 */
export function moduleText(art) {
  const others = Object.fromEntries(Object.entries(art).filter(([, entry]) => !entry.pages[BATTLE_PAGE]));
  return ['// Generated by scripts/ux/pictures.mjs from docs/art/page-pictures.json: never edited by hand (ADR 0198).',
    // Marked pure, so a bundle that imports picture.js but draws nothing from it drops this module (#1016).
    ...LEGEND, "import { BATTLE_PAGE_ART } from './battle-page-art.js';", '',
    'export const PAGE_ART = /* @__PURE__ */ Object.assign({}, BATTLE_PAGE_ART, {', ...entriesText(others), '});', ''].join('\n');
}

/** The text of table/battle-page-art.js: the pictures declared for the battle page, which page-art.js imports. */
export function battleModuleText(art) {
  return ['// Generated by scripts/ux/pictures.mjs from docs/art/page-pictures.json: never edited by hand (ADR 0198).',
    `// The pictures declared for ${BATTLE_PAGE}: the battle bundle imports these alone, and page-art.js adds every other`,
    "// page's to them (#1016).", ...LEGEND,
    'export const BATTLE_PAGE_ART = {', ...entriesText(battleArt(art)), '};', ''].join('\n');
}

/** The pictures a brief asks of the owner: the row of its Pictures table, and the prompt in the fenced block after it. */
export function asked(markdown, brief) {
  const requests = [];
  let screen = null, waiting = [], fence = null;
  for (const line of markdown.split('\n')) {
    const mark = line.startsWith('```');
    if (fence && mark) {
      const request = waiting.shift();
      if (request) requests.push({ ...request, prompt: fence.join('\n') });
      fence = null;
    } else if (fence) fence.push(line);
    else if (mark) fence = [];
    else if (line.startsWith('## ')) {
      // The heading without the walk's screens it names in brackets.
      const bracket = line.indexOf('(');
      screen = line.slice(3, bracket < 0 ? line.length : bracket).trim();
      waiting = [];
    } else if (line.startsWith('|') && line.includes(ASKED)) {
      const cells = line.split('|').slice(1, -1).map(cell => cell.trim());
      waiting.push({ brief, screen, shows: cells[0], drawn: cells[1], holds: /`([^`]+\.\w+)`/.exec(cells.at(-1))?.[1] ?? null });
    }
  }
  return requests;
}

const read = path => readFileSync(new URL(path, ROOT));
const json = path => JSON.parse(read(path).toString('utf8'));
// Read afresh after a derivation: page-art.js's own import of battle-page-art.js is the one cached before it, so the
// battle's entries are taken from a fresh import of their module, and only the other pages' from page-art.js.
const fresh = async path => import(`${new URL(path, ROOT).href}?at=${Date.now()}`);
const committed = async () => {
  const [{ PAGE_ART }, { BATTLE_PAGE_ART }] = [await fresh(MODULE), await fresh(BATTLE_MODULE)];
  return { ...Object.fromEntries(Object.entries(PAGE_ART).filter(([, entry]) => !entry.pages[BATTLE_PAGE])), ...BATTLE_PAGE_ART };
};
const list = () => { try { return readdirSync(new URL(FOLDER, ROOT)); } catch { return []; } };

/** Derives what the declaration asks for and the folder lacks. Encoding is Pillow's, through scripts/ux/encode.py. */
async function derive() {
  const authored = json(DECLARATION), before = await committed().catch(() => ({}));
  const names = authored.pictures.map(picture => picture.name);
  const wrong = [...authored.pictures.flatMap(declared), ...names.filter((name, index) => names.indexOf(name) !== index).map(name => `${name} is declared twice`), ...lonelyGroups(authored.pictures)];
  if (wrong.length) throw new Error(wrong.join('\n'));
  const art = {}, work = mkdtempSync(join(tmpdir(), 'page-art-'));
  mkdirSync(new URL(FOLDER, ROOT), { recursive: true });
  try {
    for (const picture of authored.pictures) {
      const paintings = paintingsOf(picture), bytes = paintings.map(read), from = origin(picture, bytes, authored.encoding);
      const kept = before[picture.name];
      if (kept?.from === from && kept.sizes.every(size => list().includes(size.file))) { art[picture.name] = { from, frames: kept.frames, drawn: picture.drawn, pages: picture.pages ?? {}, ...carried(picture), sizes: kept.sizes }; continue; }
      const widths = widthsOf(picture.drawn, Math.min(...bytes.map(one => paintingSize(one).width)));
      const jobs = widths.map(width => ({ sources: paintings.map(path => fileURLToPath(new URL(path, ROOT))), width, out: join(work, `${picture.name}-${width}.webp`), ...authored.encoding }));
      // NOSONAR S4036: python3 is found on the PATH of whoever derives a picture; nothing a visitor sends reaches this.
      const ran = spawnSync('python3', [fileURLToPath(new URL('encode.py', import.meta.url))], { input: JSON.stringify(jobs), encoding: 'utf8' }); // NOSONAR
      if (ran.status !== 0) throw new Error(`python3 scripts/ux/encode.py failed (it needs Pillow: python3 -m pip install pillow)\n${ran.stderr ?? ran.error}`);
      const sizes = jobs.map(job => {
        // A strip's file is its frames side by side: its width is all of them, and that is what names it.
        const made = readFileSync(job.out), size = webpSize(made), file = fileName(picture.name, size.width, made);
        // Copied, not renamed: the work folder is often on another disk than the repository.
        copyFileSync(job.out, new URL(`${FOLDER}/${file}`, ROOT));
        return { file, ...size, bytes: made.length };
      });
      art[picture.name] = { from, frames: paintings.length, drawn: picture.drawn, pages: picture.pages ?? {}, ...carried(picture), sizes };
      const sent = sizes.map(size => size.width + ' px, ' + size.bytes + ' bytes').join('; ');
      console.log(`${picture.name}: ${sent}`);
    }
    const wanted = new Set(Object.values(art).flatMap(entry => entry.sizes.map(size => size.file)));
    for (const file of list()) if (!wanted.has(file)) { rmSync(new URL(`${FOLDER}/${file}`, ROOT)); console.log(`removed ${file}`); }
    writeFileSync(new URL(BATTLE_MODULE, ROOT), battleModuleText(art));
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
    const art = await committed(), problems = check({ authored: json(DECLARATION), art, read, list });
    if (read(BATTLE_MODULE).toString('utf8') !== battleModuleText(art) || read(MODULE).toString('utf8') !== moduleText(art)) problems.push(`${MODULE} and ${BATTLE_MODULE} are not what the script writes: run node scripts/ux/pictures.mjs`);
    for (const problem of problems) console.error(problem);
    process.exitCode = problems.length ? 1 : 0;
  } else if (mode === undefined) { await derive(); printBudget(await committed()); }
  else { console.error('Usage: node scripts/ux/pictures.mjs [--check | --budget | --asked]'); process.exitCode = 2; }
}
