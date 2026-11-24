#!/usr/bin/env node
// The cut of a promotion (docs/art/promotion-plan.md, P3; ADR 0205): a title card, stretches of filmed shots with a
// caption each, and an end card, assembled by ffmpeg into one silent MP4 a language. A cut (`promo/cuts/*.json`)
// names shots of `promo/shots/` and the seconds of their films it keeps; the cards are drawn by the browser from the
// brand's tokens (`cards.css`, `docs/art/branding.css`), and the words come from the cut's file, in English and French.
//
//   node scripts/promo/cut.mjs promo/cuts/brute-pummel-answer.json --check        # the cut read and checked, no browser
//   node scripts/promo/cut.mjs promo/cuts/brute-pummel-answer.json                # artifacts/promo/cuts/<cut>/
//   node scripts/promo/cut.mjs promo/cuts/brute-pummel-answer.json --lang fr --out <folder>
//
// A cut reads its shots' frames where `shoot.mjs --film` writes them (`artifacts/promo/<shot>/frames-9x16/`, or
// `--frames <folder>` holding one folder a shot); drawing the cards needs the browser suite's Playwright, assembling
// them `ffmpeg`.

import { spawnSync } from 'node:child_process';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { mkdir, writeFile } from 'node:fs/promises';
import { basename, join, relative, resolve, sep } from 'node:path';
import { pathToFileURL } from 'node:url';
import { english, french } from '../../showcase/copy.mjs';
import { ENCODING, LONGEST_FILM, RATIOS, SAFE_ZONES, filmFacts, filmNames, framing, hasEncoder, readShot, root, sha256 } from './shoot.mjs';

export const CUT_FORMAT = 'downfall.promo.cut.v1';
export const LANGUAGES = Object.freeze(['en', 'fr']);

/**
 * The most characters a card's words may hold, by ratio: the title on three lines of the title card, a caption on
 * two lines of its band. The browser measures the words again when it draws them (`drawCards`); this is the check a
 * cut gets without one, set under what the measure lets through for an average line.
 */
export const CARD_LIMITS = Object.freeze({
  title: Object.freeze({ '9:16': 40, '16:9': 48, '1:1': 44 }),
  caption: Object.freeze({ '9:16': 56, '16:9': 90, '1:1': 64 }),
});

/** How long a card stays, in seconds: long enough to read, short enough for a feed. */
export const CARD_SECONDS = Object.freeze({ shortest: 1, longest: 5 });

/** The site the end card names, and the line under it: the showcase's own note, cut after "Free to play". */
export const SITE = 'downfallinstinct.com';
export const END_NOTE = Object.freeze({ en: english.playNote.split(' · ').slice(0, 2).join(' · '), fr: french.playNote.split(' · ').slice(0, 2).join(' · ') });

const KEYS = new Set(['format', 'title', 'ratio', 'titleCard', 'shots', 'endCard']);
const TITLE_KEYS = new Set(['seconds', ...LANGUAGES]);
const SHOT_KEYS = new Set(['shot', 'start', 'seconds', 'caption']);
const END_KEYS = new Set(['seconds']);

const isObject = value => value !== null && typeof value === 'object' && !Array.isArray(value);
const listed = keys => keys.map(key => JSON.stringify(key)).join(', ');

// A card's seconds: 2 when it gives none.
function cardSeconds(card, at, refuse) {
  const seconds = card.seconds ?? 2;
  if (typeof seconds !== 'number' || seconds < CARD_SECONDS.shortest || seconds > CARD_SECONDS.longest) refuse(`${at}.seconds is from ${CARD_SECONDS.shortest} to ${CARD_SECONDS.longest}.`);
  return seconds;
}

// A card's words in each language, one line each, at most `limit` characters at the cut's ratio.
function cardWords(value, at, limit, ratio, refuse) {
  if (!isObject(value)) refuse(`${at} gives its words in ${LANGUAGES.join(' and ')}.`);
  const out = {};
  for (const language of LANGUAGES) {
    const text = value[language];
    if (typeof text !== 'string' || !text.trim()) refuse(`${at}.${language} is missing: a card says its words in ${LANGUAGES.join(' and ')}.`);
    if (/[\n\r]/.test(text)) refuse(`${at}.${language} is one line: the card wraps it.`);
    if ([...text].length > limit) refuse(`${at}.${language} runs over its card: ${[...text].length} characters, at most ${limit} at ${ratio}.`);
    out[language] = text.trim();
  }
  return out;
}

// The keys an object of the cut may hold, and nothing else.
function onlyKeys(object, keys, where, refuse) {
  const unknown = Object.keys(object).filter(key => !keys.has(key));
  if (unknown.length) refuse(`unknown key ${listed(unknown)}${where}.`);
}

// One stretch: a filmed shot at the cut's ratio, and seconds of its film on its frames.
function readStretch(stretch, at, { ratio, shots, readShotOnce, refuse }) {
  if (!isObject(stretch)) refuse(`${at} is an object: its "shot", "start", "seconds" and "caption".`);
  onlyKeys(stretch, SHOT_KEYS, ` in ${at}`, refuse);
  if (typeof stretch.shot !== 'string' || !/^[a-z0-9][a-z0-9-]*$/.test(stretch.shot) || !existsSync(join(shots, `${stretch.shot}.json`))) {
    refuse(`${at} names the shot ${JSON.stringify(stretch.shot)}, which promo/shots/ does not hold.`);
  }
  const shot = readShotOnce(stretch.shot);
  if (!shot.film) refuse(`${at} names ${stretch.shot}, a shot with no "film": a cut keeps seconds of a film.`);
  if (!shot.ratios.includes(ratio)) refuse(`${at} names ${stretch.shot}, which is not filmed at ${ratio}.`);
  const fps = shot.film.fps;
  const start = stretch.start ?? 0;
  const onFrame = value => typeof value === 'number' && value >= 0 && Math.abs(value * fps - Math.round(value * fps)) < 1e-6;
  if (!onFrame(start)) refuse(`${at}.start is a number of seconds from 0, on a frame of the film's ${fps} a second.`);
  if (!onFrame(stretch.seconds) || stretch.seconds <= 0) refuse(`${at}.seconds is a number of seconds over 0, on a frame of the film's ${fps} a second.`);
  if (start + stretch.seconds > shot.film.seconds) refuse(`${at} keeps ${start} s to ${start + stretch.seconds} s of ${stretch.shot}, whose film lasts ${shot.film.seconds} s.`);
  return { shot, start, seconds: stretch.seconds, caption: cardWords(stretch.caption, `${at}.caption`, CARD_LIMITS.caption[ratio], ratio, refuse) };
}

/**
 * A cut's file, read and checked: refused when it names a shot `promo/shots/` does not hold, a shot without a film
 * at the cut's ratio, seconds past the end of a film, a card's words missing in a language or over its card, or
 * anything it does not know. Returns the cut with its name, its shots read, and its length.
 */
export function readCut(path, { shots = join(root, 'promo/shots'), readOne = readShot } = {}) {
  const name = basename(path, '.json');
  let cut;
  try {
    cut = JSON.parse(readFileSync(path, 'utf8'));
  } catch (error) {
    throw new Error(`${path}: not a cut's JSON (${error.message}).`);
  }
  const refuse = why => { throw new Error(`${path}: ${why}`); };
  if (!isObject(cut)) refuse('a cut is a JSON object.');
  if (cut.format !== CUT_FORMAT) refuse(`"format" is "${CUT_FORMAT}".`);
  onlyKeys(cut, KEYS, '', refuse);
  if (typeof cut.title !== 'string' || !cut.title.trim()) refuse('"title" says what the cut shows, for the person who picks it.');
  if (!Object.hasOwn(RATIOS, cut.ratio)) refuse(`"ratio" is one of ${Object.keys(RATIOS).join(', ')}.`);
  const ratio = cut.ratio;

  if (!isObject(cut.titleCard)) refuse('"titleCard" is an object: its "seconds" and its words in each language.');
  onlyKeys(cut.titleCard, TITLE_KEYS, ' in "titleCard"', refuse);
  const titleCard = { seconds: cardSeconds(cut.titleCard, 'titleCard', refuse), ...cardWords(cut.titleCard, 'titleCard', CARD_LIMITS.title[ratio], ratio, refuse) };

  if (!Array.isArray(cut.shots) || cut.shots.length === 0) refuse('"shots" lists one or more stretches of filmed shots.');
  const read = new Map();
  const readShotOnce = shot => {
    if (!read.has(shot)) read.set(shot, readOne(join(shots, `${shot}.json`)));
    return read.get(shot);
  };
  const stretches = cut.shots.map((stretch, index) => readStretch(stretch, `shots[${index}]`, { ratio, shots, readShotOnce, refuse }));
  const [first] = stretches;
  for (const stretch of stretches) {
    if (stretch.shot.size !== first.shot.size) refuse(`its shots are filmed at one size: ${first.shot.name} at ${first.shot.size}, ${stretch.shot.name} at ${stretch.shot.size}.`);
    if (stretch.shot.film.fps !== first.shot.film.fps) refuse(`its shots are filmed at one rate: ${first.shot.name} at ${first.shot.film.fps}, ${stretch.shot.name} at ${stretch.shot.film.fps}.`);
  }

  const endCard = cut.endCard ?? {};
  if (!isObject(endCard)) refuse('"endCard" is an object: its "seconds". Its words are the brand\'s.');
  onlyKeys(endCard, END_KEYS, ' in "endCard": its words are the brand\'s, not the cut\'s', refuse);
  const end = { seconds: cardSeconds(endCard, 'endCard', refuse) };

  const seconds = titleCard.seconds + stretches.reduce((sum, one) => sum + one.seconds, 0) + end.seconds;
  if (seconds > LONGEST_FILM) refuse(`the cut lasts ${seconds} s, past the ${LONGEST_FILM} s of a feed's clip.`);
  return { name, title: cut.title, ratio, size: first.shot.size, fps: first.shot.film.fps, titleCard, shots: stretches, endCard: end, seconds };
}

/** A cut's file, by language and ratio: `cut-en-9x16.mp4`. */
export const cutName = (language, ratio) => `cut-${language}-${ratio.replace(':', 'x')}.mp4`;

/** How many frames a stretch of `seconds` holds at `fps`. */
export const frameCount = (seconds, fps) => Math.round(seconds * fps);

/** The brand's mark (`table/brand-mark.svg`), drawn in the colour of the text around it rather than its own. */
export function brandMark() {
  const svg = readFileSync(join(root, 'table/brand-mark.svg'), 'utf8');
  return svg.replace(/<svg /, '<svg class="mark" aria-hidden="true" ').replace(/fill="#[0-9a-f]{3,8}"/i, 'fill="currentColor"');
}

/**
 * The palette with the display face carried in it: refused when the palette no longer names the font's file, since a
 * card would then fall back to another face, and the browser measure the words in it.
 */
export function withFont(palette, base64) {
  const source = "url('/fonts/marcellus-latin-400.woff2')";
  if (!palette.includes(source)) throw new Error(`docs/art/branding.css no longer loads the display face from ${source}: the cards would draw in another face.`);
  return palette.replace(source, `url(data:font/woff2;base64,${base64})`);
}

// The palette and the font, as the pages read them, with the font carried in the page: a card is drawn from a
// page of its own, served by no host.
function brandStyle() {
  const font = readFileSync(join(root, 'table/fonts/marcellus-latin-400.woff2')).toString('base64');
  return withFont(readFileSync(join(root, 'docs/art/branding.css'), 'utf8'), font) + readFileSync(join(root, 'scripts/promo/cards.css'), 'utf8');
}

const escape = text => text.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');

/**
 * The page of each card of a cut in one language: the title card, one caption a stretch (on a clear ground, laid over
 * the shot's frames) and the end card. A caption's band sits inside the margins the feed covers at the ratio
 * (`SAFE_ZONES`), at their top.
 */
export function cardPages(cut, language) {
  const style = `<style>${brandStyle()}</style>`;
  const page = body => `<!doctype html><html lang="${language}"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width">${style}</head><body>${body}</body></html>`;
  const zone = SAFE_ZONES[cut.ratio];
  const margin = side => `${Math.max(zone[side] * 100, 6)}%`;
  // At the top of what the feed leaves clear: the creatures' names and the battle view's dock are lower down.
  const top = zone.top > 0 ? `${zone.top * 100}%` : '6%';
  const band = `left:${margin('left')};right:${margin('right')};top:${top}`;
  return {
    title: page(`<main class="card card-title">${brandMark()}<h1 class="title" data-lines="3">${escape(cut.titleCard[language])}</h1><div class="rule"></div></main>`),
    captions: cut.shots.map(stretch => page(`<div class="caption" style="${band}"><p data-lines="2">${escape(stretch.caption[language])}</p></div>`)),
    end: page(`<main class="card card-end">${brandMark()}<p class="name">Downfall Instinct</p><div class="rule"></div>`
      + `<p class="site">${SITE}</p><p class="note" data-lines="1">${escape(END_NOTE[language])}</p></main>`),
  };
}

/**
 * The cards drawn by the browser at the cut's size, one PNG each: the caption on a clear ground. Refused when the
 * words run over their card (more lines than the card holds), which the character limit only estimates.
 */
export async function drawCards(browser, cut, language) {
  const { viewport, density } = framing(cut.ratio, cut.size);
  const pages = cardPages(cut, language);
  // A desktop page of the size asked, whatever the caller's defaults: a mobile one would lay out at 980 px.
  const context = await browser.newContext({ viewport, deviceScaleFactor: density, isMobile: false, hasTouch: false, reducedMotion: 'reduce', locale: 'en-US', timezoneId: 'UTC' });
  try {
    const draw = async (html, words, what, clear) => {
      const page = await context.newPage();
      await page.setContent(html);
      await page.evaluate(() => document.fonts.ready);
      // Lines counted by the box's height over the line's: a glyph's ascent past its line box is not a line more.
      const over = await page.locator(words).evaluate(node => Math.round(node.getBoundingClientRect().height / Number.parseFloat(getComputedStyle(node).lineHeight)) > Number(node.dataset.lines)
        || node.scrollWidth > node.clientWidth + 1);
      if (over) throw new Error(`${cut.name}: ${what} in ${language} runs over its card.`);
      const png = await page.screenshot({ omitBackground: clear });
      await page.close();
      return png;
    };
    const title = await draw(pages.title, '.title', 'the title card', false);
    // Each card on a page of its own: the captions are drawn side by side.
    const captions = await Promise.all(pages.captions.map((html, index) => draw(html, '.caption p', `the caption of shots[${index}]`, true)));
    const end = await draw(pages.end, '.note', 'the end card', false);
    return { title, captions, end };
  } finally {
    await context.close();
  }
}

/**
 * The arguments of the ffmpeg run that assembles a cut: the title card held for its seconds, each stretch of frames
 * from its folder with its caption laid over it, the end card, joined and encoded as a shot's film is
 * (`ENCODING`). `frames` holds one folder of PNG frames a stretch; `cards` the cards' files.
 */
export function assembly(cut, { frames, cards, out }) {
  const fps = String(cut.fps);
  const inputs = [];
  const chains = [];
  const card = (file, seconds) => {
    inputs.push('-loop', '1', '-framerate', fps, '-t', String(seconds), '-i', file);
    return inputs.filter(word => word === '-i').length - 1;
  };
  const still = file => {
    inputs.push('-i', file);
    return inputs.filter(word => word === '-i').length - 1;
  };
  const sequence = (folder, start) => {
    inputs.push('-framerate', fps, '-start_number', String(start), '-i', join(folder, '%06d.png'));
    return inputs.filter(word => word === '-i').length - 1;
  };
  const opening = card(cards.title, cut.titleCard.seconds);
  chains.push(`[${opening}:v]setsar=1,format=rgb24[s0]`);
  cut.shots.forEach((stretch, index) => {
    const film = sequence(frames[index], frameCount(stretch.start, cut.fps));
    const caption = still(cards.captions[index]);
    chains.push(`[${film}:v]trim=end_frame=${frameCount(stretch.seconds, cut.fps)},setpts=PTS-STARTPTS[f${index}]`,
      `[f${index}][${caption}:v]overlay=0:0:eof_action=repeat,setsar=1,format=rgb24[s${index + 1}]`);
  });
  const closing = card(cards.end, cut.endCard.seconds);
  const parts = cut.shots.length + 2;
  chains.push(`[${closing}:v]setsar=1,format=rgb24[s${parts - 1}]`,
    Array.from({ length: parts }, (_, index) => `[s${index}]`).join('') + `concat=n=${parts}:v=1:a=0,format=yuv420p[cut]`);
  return ['-y', '-loglevel', 'error', ...inputs, '-filter_complex', chains.join(';'), '-map', '[cut]', '-r', fps, '-an', ...ENCODING, out];
}

/** Assemble a cut with ffmpeg (`assembly`). */
export function assemble(cut, files) {
  const run = spawnSync('ffmpeg', assembly(cut, files), { encoding: 'utf8' }); // NOSONAR: S4036, ffmpeg on the PATH, as in shoot.mjs
  if (run.status !== 0) throw new Error(`ffmpeg could not assemble ${cut.name}: ${run.stderr || run.error?.message}`);
}

/**
 * The folder of each stretch's frames: `<frames>/<shot>/frames-<ratio>/` as `shoot.mjs --film --out` writes them.
 * Refused when a folder holds fewer frames than the stretch keeps, with the command that films it.
 */
export function framesOf(cut, base = join(root, 'artifacts/promo')) {
  return cut.shots.map(stretch => {
    const folder = join(base, stretch.shot.name, filmNames(cut.ratio).frames);
    const needed = frameCount(stretch.start + stretch.seconds, cut.fps);
    const held = existsSync(folder) ? readdirSync(folder).filter(file => /^\d{6}\.png$/.test(file)).length : 0;
    if (held < needed) {
      throw new Error(`${relative(root, folder) || folder} holds ${held} frames, the cut needs ${needed}: run `
        + `node scripts/promo/shoot.mjs promo/shots/${stretch.shot.name}.json --film --ratio ${cut.ratio}`);
    }
    return folder;
  });
}

export function parseArguments(argv) {
  const options = { cut: null, check: false, lang: null, out: null, frames: null };
  for (let index = 0; index < argv.length; index++) {
    const word = argv[index];
    if (word === '--check') options.check = true;
    else if (word === '--lang' || word === '--out' || word === '--frames') {
      if (index + 1 >= argv.length) throw new Error(`${word} needs a value.`);
      options[word.slice(2)] = argv[++index];
    } else if (word.startsWith('--')) throw new Error(`Unknown option ${word}.`);
    else if (options.cut) throw new Error('Name one cut.');
    else options.cut = word;
  }
  if (!options.cut) throw new Error('Usage: node scripts/promo/cut.mjs promo/cuts/<cut>.json [--check] [--lang en|fr] [--frames <folder>] [--out <folder>]');
  if (options.lang !== null && !LANGUAGES.includes(options.lang)) throw new Error(`--lang is one of ${LANGUAGES.join(', ')}.`);
  return options;
}

const shownPath = path => path.startsWith(`${root}${sep}`) ? relative(root, path) : path;

async function main(argv) {
  const options = parseArguments(argv);
  const cut = readCut(resolve(options.cut));
  if (options.check) {
    console.log(`${shownPath(resolve(options.cut))}: ${cut.shots.length} stretch(es), ${cut.seconds} s at ${cut.ratio}.`);
    return;
  }
  if (!hasEncoder()) throw new Error('A cut needs ffmpeg and ffprobe on the PATH.');
  const frames = framesOf(cut, options.frames ? resolve(options.frames) : undefined);
  const { playwright } = await import('../battle/filmstrip.mjs');
  const folder = resolve(options.out ?? join(root, 'artifacts/promo/cuts', cut.name));
  await mkdir(folder, { recursive: true });
  const { chromium } = playwright();
  const browser = await chromium.launch({ executablePath: process.env.STUDIO_CHROMIUM || undefined });
  try {
    // One language after the other, each cut printed as it is made.
    const cutOne = async language => {
      const drawn = await drawCards(browser, cut, language);
      const file = name => join(folder, name);
      const cards = { title: file(`title-${language}.png`), captions: drawn.captions.map((_, index) => file(`caption-${index + 1}-${language}.png`)), end: file(`end-${language}.png`) };
      await writeFile(cards.title, drawn.title);
      await Promise.all(drawn.captions.map((png, index) => writeFile(cards.captions[index], png)));
      await writeFile(cards.end, drawn.end);
      const out = file(cutName(language, cut.ratio));
      assemble(cut, { frames, cards, out });
      const facts = filmFacts(out);
      console.log(`${shownPath(out)}  sha256:${sha256(readFileSync(out))}  ${facts.width}x${facts.height} frames:${facts.frames} ${facts.seconds.toFixed(2)} s`);
    };
    await (options.lang ? [options.lang] : LANGUAGES).reduce((previous, language) => previous.then(() => cutOne(language)), Promise.resolve());
  } finally {
    await browser.close();
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    await main(process.argv.slice(2));
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}
