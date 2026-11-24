// The promotion's calendar (docs/art/promotion-plan.md, P5; promo/README.md#the-calendar): one post a file under
// promo/calendar/, named by its date. The front matter gives the channels, the media and the status; the body gives
// the post's title and its words in English and French. Nothing is posted from here: the owner publishes by hand.
//
//   node scripts/promo/calendar.mjs                       # checks promo/calendar/, exit 1 on a post it refuses
//   node scripts/promo/calendar.mjs --calendar <folder>   # another folder

import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { basename, join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { readShot, root } from './shoot.mjs';

/**
 * The channels of the launch (the plan's question 5), each with the most characters its post's words may hold: the
 * title of a Short, a Reddit post's title, a post on X or Bluesky, the caption of a TikTok or a Reel, a Discord
 * message. Counted in characters (code points), never fewer than the platforms count.
 */
export const CHANNELS = Object.freeze({
  'youtube-shorts': Object.freeze({ limit: 100 }),
  tiktok: Object.freeze({ limit: 2200 }),
  'instagram-reels': Object.freeze({ limit: 2200 }),
  reddit: Object.freeze({ limit: 300 }),
  x: Object.freeze({ limit: 280 }),
  bluesky: Object.freeze({ limit: 300 }),
  discord: Object.freeze({ limit: 2000 }),
});

export const STATUSES = Object.freeze(['draft', 'approved', 'published']);
export const LANGUAGES = Object.freeze(['en', 'fr']);

/** What the words of a post say when it shows a restyled clip (ADR 0205): its post says it is restyled. */
export const RESTYLED = Object.freeze({ en: 'Gameplay restyled by a model.', fr: 'Partie restylisée par un modèle.' });

const KEYS = ['channels', 'media', 'status'];
const NAME = /^(\d{4})-(\d{2})-(\d{2})-[a-z0-9][a-z0-9-]*\.md$/;
const SLUG = /^[a-z0-9][a-z0-9-]*$/;

const list = value => value.split(',').map(item => item.trim()).filter(Boolean);
const characters = text => [...text].length;

// The day a file is named by, if the calendar has it.
function dayOf(name, refuse) {
  const match = NAME.exec(name);
  if (!match) refuse('a post is named by its date and a name: YYYY-MM-DD-<name>.md, in lower case.');
  const [, year, month, day] = match.map(Number);
  const date = new Date(Date.UTC(year, month - 1, day));
  if (date.getUTCFullYear() !== year || date.getUTCMonth() !== month - 1 || date.getUTCDate() !== day) refuse(`${name.slice(0, 10)} is not a day.`);
  return name.slice(0, 10);
}

// The front matter's lines, and the body after it.
function split(text, refuse) {
  const lines = text.replaceAll('\r\n', '\n').split('\n');
  if (lines[0] !== '---') refuse('a post starts with its "---" front matter: channels, media, status.');
  const end = lines.indexOf('---', 1);
  if (end < 0) refuse('the front matter ends with a "---" line.');
  const head = new Map();
  const links = new Map();
  for (const line of lines.slice(1, end)) {
    if (!line.trim()) continue;
    const match = /^([a-z][a-z-]*(?: [a-z][a-z-]*)?): ?(.*)$/.exec(line);
    if (!match) refuse(`the front matter's lines are "key: value", not ${JSON.stringify(line)}.`);
    const [, key, value] = match;
    const into = key.startsWith('link ') ? links : head;
    const name = key.startsWith('link ') ? key.slice(5) : key;
    if (into.has(name)) refuse(`the front matter gives "${key}" twice.`);
    if (into === head && !KEYS.includes(key)) refuse(`"${key}" is not a key of a post: ${KEYS.join(', ')} and "link <channel>".`);
    into.set(name, value.trim());
  }
  return { head, links, body: lines.slice(end + 1) };
}

// The title, and the words under each language's heading.
function words(body, refuse) {
  const title = body.find(line => line.startsWith('# '))?.slice(2).trim();
  if (!title) refuse('the post has its title, a "# " heading, for the owner who reads the calendar.');
  const sections = new Map();
  let current;
  for (const line of body) {
    if (line.startsWith('## ')) {
      current = line.slice(3).trim();
      if (!LANGUAGES.includes(current)) refuse(`"## ${current}" is not a language of a post: ${LANGUAGES.map(language => `"## ${language}"`).join(', ')}.`);
      if (sections.has(current)) refuse(`"## ${current}" is given twice.`);
      sections.set(current, []);
    } else if (current) {
      sections.get(current).push(line);
    }
  }
  const out = {};
  for (const language of LANGUAGES) {
    const text = (sections.get(language) ?? []).join('\n').trim();
    if (!text) refuse(`the post's words in "## ${language}" are missing.`);
    out[language] = text;
  }
  return { title, words: out };
}

// One medium of the post, which the repository holds: a cut, a shot's still or film, or a generated file by hash.
function medium(entry, { cuts, shots, generated, refuse }) {
  const [kind, name, ratio, ...rest] = entry.split(/\s+/);
  const usage = 'each medium is "cut <cut>", "still <shot> <ratio>", "film <shot> <ratio>" or "generated <sha256>"';
  if (rest.length || !name) refuse(`${usage}, not ${JSON.stringify(entry)}.`);
  switch (kind) {
    case 'cut': {
      if (ratio !== undefined) refuse(`${usage}, not ${JSON.stringify(entry)}.`);
      const path = join(cuts, `${name}.json`);
      if (!SLUG.test(name) || !existsSync(path)) refuse(`the cut ${JSON.stringify(name)} is one promo/cuts/ does not hold.`);
      return { kind, name, ratio: JSON.parse(readFileSync(path, 'utf8')).ratio };
    }
    case 'still':
    case 'film': {
      const path = join(shots, `${name}.json`);
      if (!SLUG.test(name) || !existsSync(path)) refuse(`the shot ${JSON.stringify(name)} is one promo/shots/ does not hold.`);
      const shot = readShot(path);
      if (!shot.ratios.includes(ratio)) refuse(`the shot ${name} is not taken at ${ratio}: ${shot.ratios.join(', ')}.`);
      if (kind === 'film' && !shot.film) refuse(`the shot ${name} has no film.`);
      return { kind, name, ratio };
    }
    case 'generated': {
      if (ratio !== undefined) refuse(`${usage}, not ${JSON.stringify(entry)}.`);
      const line = generated().find(file => file.sha256 === name);
      if (!line) refuse(`the generated file ${name} is one promo/generated.json does not hold.`);
      return { kind, name, ratio: line.ratio, makes: line.makes };
    }
    default:
      return refuse(`${usage}, not ${JSON.stringify(entry)}.`);
  }
}

/**
 * One post read and checked. Refused: a name that is not its date, a key not in the format, no channel or one not
 * of the launch, no media or media the repository does not hold, words missing in a language or over a channel's
 * limit, a restyled clip its words do not call restyled, a link before it is published, and a published post
 * without the link of each channel, or dated after today.
 */
export function readPost(path, {
  cuts = join(root, 'promo/cuts'),
  shots = join(root, 'promo/shots'),
  generated = join(root, 'promo/generated.json'),
  today = new Date().toISOString().slice(0, 10),
} = {}) {
  const file = basename(path);
  const refuse = why => { throw new Error(`${path}: ${why}`); };
  const date = dayOf(file, refuse);
  const { head, links, body } = split(readFileSync(path, 'utf8'), refuse);
  const { title, words: said } = words(body, refuse);

  const channels = list(head.get('channels') ?? '');
  if (channels.length === 0) refuse(`"channels" lists one or more of ${Object.keys(CHANNELS).join(', ')}.`);
  for (const channel of channels) if (!Object.hasOwn(CHANNELS, channel)) refuse(`the channel ${JSON.stringify(channel)} is not one of ${Object.keys(CHANNELS).join(', ')}.`);
  if (new Set(channels).size !== channels.length) refuse('"channels" names a channel twice.');

  const entries = list(head.get('media') ?? '');
  if (entries.length === 0) refuse('"media" names what the post shows: a cut, a still, a film or a generated file.');
  if (new Set(entries).size !== entries.length) refuse('"media" names a medium twice.');
  let record;
  const readRecord = () => {
    record ??= existsSync(generated) ? JSON.parse(readFileSync(generated, 'utf8')).files ?? [] : [];
    return record;
  };
  const media = entries.map(entry => medium(entry, { cuts, shots, generated: readRecord, refuse }));

  for (const language of LANGUAGES) {
    const count = characters(said[language]);
    for (const channel of channels) {
      if (count > CHANNELS[channel].limit) refuse(`its words in ${language} are ${count} characters, and ${channel} takes ${CHANNELS[channel].limit}.`);
    }
    if (media.some(item => item.makes === 'restyled-clip') && !said[language].includes(RESTYLED[language])) {
      refuse(`it shows a restyled clip, so its words in ${language} say "${RESTYLED[language]}" (ADR 0205).`);
    }
  }

  const status = head.get('status');
  if (!STATUSES.includes(status)) refuse(`"status" is ${STATUSES.slice(0, -1).join(', ')} or ${STATUSES.at(-1)}.`);
  for (const [channel, link] of links) {
    if (!channels.includes(channel)) refuse(`"link ${channel}" names ${channel}, which is not one of its channels.`);
    if (!/^https:\/\/[^\s/]+\/\S*$/.test(link)) refuse(`"link ${channel}" is the post's https:// address.`);
  }
  if (status === 'published') {
    if (date > today) refuse(`it is published, and dated ${date}, after today (${today}).`);
    for (const channel of channels) if (!links.has(channel)) refuse(`it is published, so it gives the link of ${channel}: "link ${channel}: https://...".`);
  } else if (links.size > 0) {
    refuse(`it is ${status}, so it gives no link yet: a link comes once it is published.`);
  }
  return { file, date, title, channels, media, words: said, status, links: Object.fromEntries(links) };
}

/** Every post of the calendar, in the order of their dates. Refuses a folder that is missing or holds no post. */
export function readCalendar(folder, options = {}) {
  if (!existsSync(folder)) throw new Error(`${folder}: the calendar is missing.`);
  const names = readdirSync(folder).filter(name => name.endsWith('.md') && name !== 'README.md').sort((a, b) => a.localeCompare(b));
  if (names.length === 0) throw new Error(`${folder}: the calendar holds no post.`);
  return names.map(name => readPost(join(folder, name), options));
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    const argv = process.argv.slice(2);
    let folder = join(root, 'promo/calendar');
    if (argv.length === 2 && argv[0] === '--calendar') folder = argv[1];
    else if (argv.length) throw new Error('usage: node scripts/promo/calendar.mjs [--calendar <folder>]');
    const posts = readCalendar(folder);
    const count = status => posts.filter(post => post.status === status).length;
    console.log(`promo/calendar: ${posts.length} post(s), ${count('draft')} draft, ${count('approved')} approved, ${count('published')} published.`);
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}
