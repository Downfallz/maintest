#!/usr/bin/env node
// The brief of a promotion's generated part (docs/art/promotion-plan.md, P4; ADR 0205): what a model may make, asked
// of ChatGPT or Gemini through the owner's own account. A brief (`promo/briefs/*.json`) names what to make and the
// filmed shot it starts from; this script writes the prompt pack (the prompt, the reference frames of the shot, the
// palette, the real creature's and spell's words), and takes the owner's file back in unchanged with where it came
// from, into `promo/generated.json`.
//
//   node scripts/promo/brief.mjs promo/briefs/brute-pummel-key-art.json --check     # the prompt printed, no frames
//   node scripts/promo/brief.mjs promo/briefs/brute-pummel-key-art.json             # artifacts/promo/briefs/<brief>/
//   node scripts/promo/brief.mjs promo/briefs/brute-pummel-key-art.json --intake <file> --model "<model>" --date 2026-11-24
//
// The pack reads the shot's frames where `shoot.mjs` writes them (`artifacts/promo/<shot>/`, or `--frames <folder>`);
// a clip to restyle is encoded with `ffmpeg`. No model's API is called: the owner says first that the prompts are
// proven (the plan's question 2).

import { spawnSync } from 'node:child_process';
import { copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { basename, join, relative, resolve, sep } from 'node:path';
import { pathToFileURL } from 'node:url';
import { ENCODING, RATIOS, filmNames, readShot, root, sha256, stillName } from './shoot.mjs';

export const BRIEF_FORMAT = 'downfall.promo.brief.v1';
export const GENERATED_FORMAT = 'downfall.promo.generated.v1';

/**
 * What a model may make (ADR 0205), each with what the prompt asks of it. A model never makes gameplay from a
 * prompt alone: every kind starts from frames of a filmed shot, and a restyled clip from a stretch of its film.
 */
export const KINDS = Object.freeze({
  'key-art': 'Make a painted key art of the moment the reference frames show. It is for the game\'s launch and its store banners.',
  background: 'Make a painted background in the world of the reference frames, with no creature in it. Words and cards will sit on it.',
  transition: 'Make a short painted transition between two moments of the reference frames, for a clip to pass from one to the other.',
  'styled-still': 'Repaint the reference frame: the same creatures, poses and places, in a richer painted finish.',
  thumbnail: 'Make a thumbnail for a clip: the moment of the reference frames, read at a glance on a phone.',
  'restyled-clip': 'Restyle the reference clip, video to video: keep every creature, move, cut and timing, and repaint only the look.',
});

/** The kinds of file a generated part comes back as, by how its bytes start, and the extension it is kept under. */
const TYPES = Object.freeze([
  { type: 'png', test: bytes => bytes.subarray(0, 8).equals(Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a])) },
  { type: 'jpg', test: bytes => bytes.subarray(0, 3).equals(Buffer.from([0xff, 0xd8, 0xff])) },
  { type: 'webp', test: bytes => bytes.subarray(0, 4).toString('latin1') === 'RIFF' && bytes.subarray(8, 12).toString('latin1') === 'WEBP' },
  { type: 'mp4', test: bytes => bytes.subarray(4, 8).toString('latin1') === 'ftyp' },
]);

const KEYS = new Set(['format', 'title', 'makes', 'shot', 'ratio', 'frames', 'clip', 'ask']);
const CLIP_KEYS = new Set(['start', 'seconds']);
const RECORD_KEYS = ['brief', 'makes', 'shot', 'ratio', 'references', 'type', 'sha256', 'bytes', 'model', 'date', 'prompt', 'blob'];

const isObject = value => value !== null && typeof value === 'object' && !Array.isArray(value);
const listed = keys => keys.map(key => JSON.stringify(key)).join(', ');
const NAME = /^[a-z0-9][a-z0-9-]*$/;

/** The brand's colours a painting may use: the grounds, the light and the accents, not the pages' surfaces and lines. */
const PAINTED_COLOURS = Object.freeze(['--brand-slate', '--brand-soot', '--brand-bone', '--brand-ivory', '--brand-muted', '--brand-gold', '--brand-gold-bright', '--brand-ember']);
const lowerFirst = text => text.charAt(0).toLowerCase() + text.slice(1);
const ORDINALS = Object.freeze(['the first', 'the second', 'the third', 'the fourth']);

/** A reference frame's file in the pack: `frame-6.50s.png`. */
export const frameName = seconds => `frame-${seconds.toFixed(2)}s.png`;

// A time in a film, in seconds, on one of its frames and inside it.
function onFrame(value, film, at, refuse) {
  const frames = value * film.fps;
  if (typeof value !== 'number' || value < 0 || Math.abs(frames - Math.round(frames)) > 1e-6) refuse(`${at} is a number of seconds from 0, on a frame of the film's ${film.fps} a second.`);
  return Math.round(frames);
}

/**
 * A brief's file, read and checked: refused when it names a kind a model may not make, a shot `promo/shots/` does
 * not hold or a ratio it is not taken at, frames past its film or on a shot never filmed, a restyled clip without
 * its stretch, or anything it does not know. Returns the brief with its name and its shot read.
 */
export function readBrief(path, { shots = join(root, 'promo/shots'), readOne = readShot } = {}) {
  const name = basename(path, '.json');
  let brief;
  try {
    brief = JSON.parse(readFileSync(path, 'utf8'));
  } catch (error) {
    throw new Error(`${path}: not a brief's JSON (${error.message}).`);
  }
  const refuse = why => { throw new Error(`${path}: ${why}`); };
  if (!isObject(brief)) refuse('a brief is a JSON object.');
  if (brief.format !== BRIEF_FORMAT) refuse(`"format" is "${BRIEF_FORMAT}".`);
  const unknown = Object.keys(brief).filter(key => !KEYS.has(key));
  if (unknown.length) refuse(`unknown key ${listed(unknown)}.`);
  if (typeof brief.title !== 'string' || !brief.title.trim()) refuse('"title" says what the brief asks for, for the person who picks it.');
  if (!Object.hasOwn(KINDS, brief.makes)) refuse(`"makes" is what a model may make (ADR 0205): one of ${Object.keys(KINDS).join(', ')}. Gameplay from a prompt alone is not one.`);
  if (typeof brief.ask !== 'string' || !brief.ask.trim()) refuse('"ask" says, in a sentence or two, what the picture should be.');
  if (typeof brief.shot !== 'string' || !NAME.test(brief.shot) || !existsSync(join(shots, `${brief.shot}.json`))) {
    refuse(`"shot" names ${JSON.stringify(brief.shot)}, which promo/shots/ does not hold: a model starts from the game filmed.`);
  }
  const shot = readOne(join(shots, `${brief.shot}.json`));
  if (!shot.ratios.includes(brief.ratio)) refuse(`"ratio" is one of the shot's: ${shot.ratios.join(', ')}.`);

  let frames = [];
  if (brief.frames !== undefined) {
    if (!shot.film) refuse(`"frames" are seconds of a film, and ${shot.name} has none: leave them out for its still.`);
    if (!Array.isArray(brief.frames) || brief.frames.length === 0 || brief.frames.length > 4) refuse('"frames" lists one to four seconds of the shot\'s film.');
    frames = brief.frames.map((seconds, index) => {
      const at = `"frames"[${index}]`;
      const frame = onFrame(seconds, shot.film, at, refuse);
      if (frame >= shot.film.seconds * shot.film.fps) refuse(`${at} is ${seconds} s, past the ${shot.film.seconds} s of ${shot.name}'s film.`);
      return { seconds, frame, file: frameName(seconds) };
    });
    if (new Set(frames.map(one => one.frame)).size !== frames.length) refuse('"frames" names a frame twice.');
  }

  let clip = null;
  if (brief.makes === 'restyled-clip') {
    if (!shot.film) refuse(`a restyled clip starts from a film, and ${shot.name} has none.`);
    if (!isObject(brief.clip)) refuse('a restyled clip gives its "clip": the "start" and "seconds" of the shot\'s film it restyles.');
    const extra = Object.keys(brief.clip).filter(key => !CLIP_KEYS.has(key));
    if (extra.length) refuse(`unknown key ${listed(extra)} in "clip".`);
    const start = brief.clip.start ?? 0;
    const first = onFrame(start, shot.film, '"clip.start"', refuse);
    const count = onFrame(brief.clip.seconds, shot.film, '"clip.seconds"', refuse);
    if (count === 0) refuse('"clip.seconds" is over 0.');
    if (start + brief.clip.seconds > shot.film.seconds) refuse(`"clip" keeps ${start} s to ${start + brief.clip.seconds} s of ${shot.name}, whose film lasts ${shot.film.seconds} s.`);
    clip = { start, seconds: brief.clip.seconds, first, count, file: `clip-${brief.ratio.replace(':', 'x')}.mp4` };
  } else if (brief.clip !== undefined) refuse('"clip" is for a restyled clip only.');

  return { name, title: brief.title.trim(), makes: brief.makes, ask: brief.ask.trim(), shot, ratio: brief.ratio, frames, clip };
}

/** The references a brief hands the model, by file name: its frames, its clip, or the shot's still. */
export function references(brief) {
  const files = brief.frames.map(one => one.file);
  if (brief.clip) files.push(brief.clip.file);
  if (!files.length) files.push(stillName(brief.ratio));
  return files;
}

// The tiers a scenario's creatures took before its cut, each seat's in order, once each; the roster's creature
// when none evolved. These are the creatures the shot shows.
function shownTiers(scenario) {
  const evolved = (scenario.before ?? []).filter(action => action.kind === 'Evolution' && typeof action.tier === 'string').map(action => action.tier);
  return [...new Set(evolved)];
}

// A row of a table in a Markdown file, by its first cell, as its cells.
function tableRow(markdown, first) {
  const line = markdown.split('\n').find(row => row.startsWith('|') && row.split('|')[1]?.trim().toLowerCase().startsWith(first.toLowerCase()));
  return line ? line.split('|').slice(1, -1).map(cell => cell.trim()) : null;
}

// A spell's staging cue in the art direction's tables (`| Mutation | Spell EN / FR | Cue | Status |`), or null.
function spellCue(markdown, name) {
  const row = markdown.split('\n').map(line => line.split('|').map(cell => cell.trim())).find(cells => cells.length > 4 && cells[2].replaceAll('*', '').startsWith(`${name} /`));
  return row ? row[3] : null;
}

// What a spell's targeting reaches, in a player's words.
function reach(targeting) {
  const who = { Enemy: 'enemy', Ally: 'ally', Self: 'self' }[targeting?.origin] ?? String(targeting?.origin ?? '').toLowerCase();
  if (who === 'self') return 'itself';
  const most = targeting?.maxTargets ?? 1;
  return most === 1 ? `one ${who}` : `up to ${most} ${who === 'ally' ? 'allies' : 'enemies'}`;
}

// What one effect does, from its numbers.
function effectWords(effect) {
  const kind = effect.kind.replaceAll(/([a-z])([A-Z])/g, '$1 $2').toLowerCase();
  const amount = effect.amount ?? effect.amountPerRound;
  const words = amount === undefined ? `gives ${kind}` : `deals ${amount} ${kind}${effect.amountPerRound === undefined ? '' : ' a round'}`;
  return effect.durationRounds ? `${words} for ${effect.durationRounds} rounds` : words;
}

/** Every spell the authored content declares, by id. */
function spellsById(folder) {
  const spells = new Map();
  const walk = path => {
    for (const entry of readdirSync(path, { withFileTypes: true })) {
      const inner = join(path, entry.name);
      if (entry.isDirectory()) walk(inner);
      else if (entry.name.endsWith('.json')) {
        const spell = JSON.parse(readFileSync(inner, 'utf8'));
        if (typeof spell.id === 'string') spells.set(spell.id, spell);
      }
    }
  };
  walk(folder);
  return spells;
}

/**
 * The game's own words for what the shot shows: each creature's form by its name in `data/Tiers/`, its family in
 * `data/presentation/mutation-art.json` and its look in the art direction's visual language; each spell the film
 * casts by its name and numbers in `data/Spells/` and its staging cue in the art direction.
 */
export function shownWords(shot, { base = root } = {}) {
  const scenario = JSON.parse(readFileSync(join(base, 'scenarios', shot.scenario), 'utf8'));
  const direction = readFileSync(join(base, 'docs/art/creature-art-direction.md'), 'utf8');
  const families = JSON.parse(readFileSync(join(base, 'data/presentation/mutation-art.json'), 'utf8'));
  const tiers = shownTiers(scenario);
  const creatures = tiers.length
    ? tiers.map(id => {
      const tier = JSON.parse(readFileSync(join(base, 'data/Tiers', `${id.split(':')[1]}.${id.split(':')[2]}.json`), 'utf8'));
      const family = families[id]?.family ?? 'creature';
      const look = tableRow(direction, family) ?? tableRow(direction, 'Base Creature');
      return { id, name: tier.name, family: look[0].split(' (')[0], look: look[1], reads: look[2] };
    })
    : [{ id: scenario.match.roster[0], name: 'Creature', family: 'Base Creature', ...(([, look, reads]) => ({ look, reads }))(tableRow(direction, 'Base Creature')) }];
  const spells = spellsById(join(base, 'data/Spells'));
  const casts = (shot.film?.casts ?? []).map(id => {
    const spell = spells.get(id);
    const cue = spellCue(direction, spell.name);
    return { id, name: spell.name, does: `${spell.effects.map(effectWords).join(' and ')} to ${reach(spell.targeting)}`, cue };
  });
  const shared = /Shared anatomy across all: ([^.]+)\. Palette: ([^.]+)\./.exec(direction);
  if (!shared) throw new Error('docs/art/creature-art-direction.md no longer says the shared anatomy and palette of the creatures.');
  const ground = tableRow(readFileSync(join(base, 'battle/ART.md'), 'utf8'), '`table/battle-art/eclipse-battle.webp`');
  if (!ground) throw new Error('battle/ART.md no longer describes the eclipse battlefield.');
  return { creatures, spells: casts, anatomy: shared[1].trim(), creaturePalette: shared[2].trim(), ground: lowerFirst(ground[2].replace(/;[^;]*$/, '.')) };
}

/** The brand's palette, as `docs/art/branding.css` declares it: each `--brand-` token and its colour. */
export function palette(base = root) {
  const css = readFileSync(join(base, 'docs/art/branding.css'), 'utf8');
  return [...css.matchAll(/--brand-([a-z-]+):\s*(#[0-9a-f]{6})\s*;/gi)].map(([, token, colour]) => ({ token: `--brand-${token}`, name: token.replaceAll('-', ' '), colour: colour.toLowerCase() }));
}

/** The prompt of a brief: what to make, from which frames, what they show in the game's words, and what not to do. */
export function prompt(brief, { base = root } = {}) {
  const { width, height } = RATIOS[brief.ratio];
  const scale = brief.shot.size / Math.min(width, height);
  const words = shownWords(brief.shot, { base });
  const lines = [
    `# ${brief.title}`,
    '',
    KINDS[brief.makes],
    '',
    brief.ask,
    '',
    '## The reference',
    '',
    `Start from the attached ${brief.clip ? 'clip and frames' : 'frames'} of Downfall Instinct, a turn-based tactics game, filmed as it plays: the shot "${brief.shot.name}", titled "${brief.shot.title}".`,
    ...references(brief).map((file, index, all) => `- ${file}${all.length > 1 ? `, ${ORDINALS[index]} of ${all.length}, in the order the game plays them` : ''}`),
    `Frame it at ${brief.ratio}, ${width * scale} by ${height * scale} pixels.`,
    '',
    '## What the frames show, in the game\'s words',
    '',
    `- The ground, as the game paints it: ${words.ground}`,
    ...words.creatures.map(one => `- ${one.name}, of the ${one.family} family: ${lowerFirst(one.look)}. It reads as ${one.reads.toLowerCase()}. Like every creature of the game: ${words.anatomy}.`),
    ...words.spells.map(one => `- The spell ${one.name}, which ${one.does}.${one.cue ? ` The game stages it as: ${one.cue.toLowerCase()}.` : ''}`),
    '',
    '## The palette',
    '',
    `The creatures keep their own colours: ${words.creaturePalette}. Bright warm gold is the game's sign for what can be done: it stays off the creatures' bodies, whose own amber is dimmer and smaller.`,
    'The rest keeps to the brand\'s colours, mostly slate and soot, with bone and ivory light, gold only in a small share of the picture (the eclipse, the light of the blow) and copper as a small detail:',
    ...palette(base).filter(one => PAINTED_COLOURS.includes(one.token)).map(one => `- ${one.name} ${one.colour}`),
    'The teams\' tints stay as the frames show them.',
    '',
    '## What not to do',
    '',
    '- No words, letters, numbers or logo: words are added later, over the picture.',
    '- Keep each creature as the frames draw it: its shape, its number of legs, its colours. Add no creature, weapon or armour the frames do not show.',
    '- Show no moment the frames do not hold: no other spell, no other place, no interface the game does not have.',
  ];
  if (brief.makes === 'restyled-clip') lines.push('- Keep every move, its timing and the camera frame for frame: only the look changes. The post says the clip is restyled.');
  return `${lines.join('\n')}\n`;
}

/** The pack's folder holds the prompt, its references and `pack.json`; returns what it wrote. */
export function writePack(brief, { from = join(root, 'artifacts/promo', brief.shot.name), out = join(root, 'artifacts/promo/briefs', brief.name), base = root, encode = encodeClip } = {}) {
  const film = filmNames(brief.ratio);
  const sources = brief.frames.map(one => ({ file: one.file, path: join(from, film.frames, `${String(one.frame).padStart(6, '0')}.png`) }));
  if (!brief.frames.length && !brief.clip) sources.push({ file: stillName(brief.ratio), path: join(from, stillName(brief.ratio)) });
  const how = brief.shot.film ? `--film --ratio ${brief.ratio}` : `--still --ratio ${brief.ratio}`;
  const lacking = [...sources.map(one => one.path), ...(brief.clip ? [join(from, film.frames, `${String(brief.clip.first + brief.clip.count - 1).padStart(6, '0')}.png`)] : [])].find(path => !existsSync(path));
  if (lacking) throw new Error(`${shownPath(lacking)} is missing: run node scripts/promo/shoot.mjs promo/shots/${brief.shot.name}.json ${how}`);
  mkdirSync(out, { recursive: true });
  const text = prompt(brief, { base });
  writeFileSync(join(out, 'prompt.md'), text);
  for (const one of sources) copyFileSync(one.path, join(out, one.file));
  if (brief.clip) encode(join(from, film.frames), brief.shot.film.fps, brief.clip, join(out, brief.clip.file));
  const files = references(brief).map(file => ({ file, sha256: sha256(readFileSync(join(out, file))) }));
  const pack = { brief: brief.name, makes: brief.makes, shot: brief.shot.name, ratio: brief.ratio, prompt: sha256(text), references: files };
  writeFileSync(join(out, 'pack.json'), `${JSON.stringify(pack, null, 2)}\n`);
  return pack;
}

/** The ffmpeg arguments that encode a clip's stretch of a film's frames, as a film is encoded (`ENCODING`). */
export function clipArguments(folder, fps, clip, out) {
  return ['-y', '-loglevel', 'error', '-framerate', String(fps), '-start_number', String(clip.first), '-i', join(folder, '%06d.png'),
    '-frames:v', String(clip.count), ...ENCODING, out];
}

function encodeClip(folder, fps, clip, out) {
  const run = spawnSync('ffmpeg', clipArguments(folder, fps, clip, out), { encoding: 'utf8' }); // NOSONAR: S4036, ffmpeg on the PATH, as in shoot.mjs
  if (run.status !== 0) throw new Error(`ffmpeg could not encode the clip: ${run.stderr || run.error?.message}`);
}

/** What a generated file is, by how its bytes start: png, jpg, webp or mp4, or null. */
export const fileType = bytes => TYPES.find(one => one.test(bytes))?.type ?? null;

const DAY = /^\d{4}-\d{2}-\d{2}$/;
const isDay = text => typeof text === 'string' && DAY.test(text) && new Date(`${text}T00:00:00Z`).toISOString().startsWith(text);

/**
 * The record of what models made: each file by its hash, with the brief it answers, the model and the day it was
 * made, the prompt it was made from, and the shot's references it started from. Refused when a line lacks any of
 * them. Returns the files.
 */
export function readGenerated(path, { briefs = join(root, 'promo/briefs') } = {}) {
  let record;
  try {
    record = JSON.parse(readFileSync(path, 'utf8'));
  } catch (error) {
    throw new Error(`${path}: not the record's JSON (${error.message}).`);
  }
  const refuse = why => { throw new Error(`${path}: ${why}`); };
  if (record?.format !== GENERATED_FORMAT || !Array.isArray(record.files)) refuse(`the record is {"format": "${GENERATED_FORMAT}", "files": [...]}.`);
  const seen = new Set();
  record.files.forEach((file, index) => {
    const at = `file ${index + 1}`;
    if (!isObject(file) || Object.keys(file).sort((a, b) => a.localeCompare(b)).join() !== [...RECORD_KEYS].sort((a, b) => a.localeCompare(b)).join()) refuse(`${at} has exactly ${RECORD_KEYS.join(', ')}.`);
    if (!NAME.test(file.brief) || !existsSync(join(briefs, `${file.brief}.json`))) refuse(`${at} answers the brief ${JSON.stringify(file.brief)}, which promo/briefs/ does not hold.`);
    if (!Object.hasOwn(KINDS, file.makes)) refuse(`${at} is one of what a model may make: ${Object.keys(KINDS).join(', ')}.`);
    if (typeof file.model !== 'string' || !file.model.trim()) refuse(`${at} names the model that made it.`);
    if (!isDay(file.date)) refuse(`${at} gives the day it was made, as YYYY-MM-DD.`);
    if (typeof file.prompt !== 'string' || !file.prompt.trim()) refuse(`${at} keeps the prompt it was made from.`);
    if (!Array.isArray(file.references) || file.references.length === 0) refuse(`${at} names the shot's references it started from.`);
    if (!/^[0-9a-f]{64}$/.test(file.sha256)) refuse(`${at} gives its SHA-256 as 64 hexadecimal digits.`);
    if (!Number.isInteger(file.bytes) || file.bytes <= 0) refuse(`${at} gives its size in bytes.`);
    if (file.blob !== `generated/${file.brief}/${file.sha256}.${file.type}`) refuse(`${at} goes to generated/${file.brief}/${file.sha256}.${file.type}, its hash under its brief.`);
    if (seen.has(file.sha256)) refuse(`${at} records ${file.sha256} twice.`);
    seen.add(file.sha256);
  });
  return record.files;
}

/**
 * The owner's file taken in, unchanged: refused without the model that made it, the day, or a prompt; when it is not
 * a picture (or, for a restyled clip, an MP4); when the day is after today; or when the record holds it already. The
 * file is copied under its hash to `store`, and its line added to the record. Returns the line.
 */
export function intake(brief, file, { model, date, promptText, today = new Date().toISOString().slice(0, 10), record = join(root, 'promo/generated.json'), store = join(root, 'artifacts/promo/generated'), briefs = join(root, 'promo/briefs') } = {}) {
  const refuse = why => { throw new Error(`${file}: ${why}`); };
  if (typeof model !== 'string' || !model.trim()) refuse('name the model that made it (--model "ChatGPT, GPT-image-1"): a generated file comes in with where it came from.');
  if (!isDay(date)) refuse('give the day it was made (--date YYYY-MM-DD).');
  if (date > today) refuse(`${date} is after today, ${today}.`);
  const text = promptText ?? prompt(brief);
  if (typeof text !== 'string' || !text.trim()) refuse('give the prompt it was made from (--prompt <file>).');
  if (!existsSync(file)) refuse('no such file.');
  const bytes = readFileSync(file);
  const type = fileType(bytes);
  const wanted = brief.makes === 'restyled-clip' ? ['mp4'] : ['png', 'jpg', 'webp'];
  if (!wanted.includes(type)) refuse(`a ${brief.makes} comes back as ${wanted.join(', ')}; this file is ${type ?? 'none of png, jpg, webp or mp4'}.`);
  const files = existsSync(record) ? readGenerated(record, { briefs }) : [];
  const hash = sha256(bytes);
  if (files.some(one => one.sha256 === hash)) refuse(`the record holds it already (${hash}).`);
  const line = { brief: brief.name, makes: brief.makes, shot: brief.shot.name, ratio: brief.ratio, references: references(brief), type, sha256: hash, bytes: bytes.length, model: model.trim(), date, prompt: text, blob: `generated/${brief.name}/${hash}.${type}` };
  mkdirSync(join(store, brief.name), { recursive: true });
  copyFileSync(file, join(store, brief.name, `${hash}.${type}`));
  writeFileSync(record, `${JSON.stringify({ format: GENERATED_FORMAT, files: [...files, line] }, null, 2)}\n`);
  return line;
}

export function parseArguments(argv) {
  const options = { brief: null, check: false, intake: null, model: null, date: null, prompt: null, frames: null, out: null };
  for (let index = 0; index < argv.length; index++) {
    const word = argv[index];
    if (word === '--check') options.check = true;
    else if (['--intake', '--model', '--date', '--prompt', '--frames', '--out'].includes(word)) {
      if (index + 1 >= argv.length) throw new Error(`${word} needs a value.`);
      options[word.slice(2)] = argv[++index];
    } else if (word.startsWith('--')) throw new Error(`Unknown option ${word}.`);
    else if (options.brief) throw new Error('Name one brief.');
    else options.brief = word;
  }
  if (!options.brief) throw new Error('Usage: node scripts/promo/brief.mjs promo/briefs/<brief>.json [--check] [--frames <folder>] [--out <folder>] | --intake <file> --model "<model>" --date YYYY-MM-DD [--prompt <file>]');
  if (options.check && options.intake) throw new Error('Name --check or --intake, not both.');
  if (!options.intake && (options.model || options.date || options.prompt)) throw new Error('--model, --date and --prompt go with --intake.');
  return options;
}

const shownPath = path => path.startsWith(`${root}${sep}`) ? relative(root, path) : path;

function main(argv) {
  const options = parseArguments(argv);
  const brief = readBrief(resolve(options.brief));
  if (options.check) {
    process.stdout.write(prompt(brief));
    return;
  }
  if (options.intake) {
    const promptText = options.prompt ? readFileSync(resolve(options.prompt), 'utf8') : undefined;
    const line = intake(brief, resolve(options.intake), { model: options.model, date: options.date, promptText });
    console.log(`promo/generated.json: ${line.blob}  ${line.bytes} bytes, ${line.model}, ${line.date}`);
    return;
  }
  const out = options.out ? resolve(options.out) : undefined;
  const pack = writePack(brief, { ...(options.frames ? { from: join(resolve(options.frames), brief.shot.name) } : {}), ...(out ? { out } : {}) });
  const folder = out ?? join(root, 'artifacts/promo/briefs', brief.name);
  console.log(`${shownPath(join(folder, 'prompt.md'))}  sha256:${pack.prompt}`);
  for (const one of pack.references) console.log(`${shownPath(join(folder, one.file))}  sha256:${one.sha256}`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    main(process.argv.slice(2));
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}
