// The promotion's calendar (docs/art/promotion-plan.md, P5; promo/README.md#the-calendar): one post a file under
// promo/calendar/, named by its date. The front matter gives the hour, the channels, the media and the status; the body gives
// the post's title and its words in English and French. Nothing is posted from here: the owner publishes by hand.
//
//   node scripts/promo/calendar.mjs                       # checks promo/calendar/, exit 1 on a post it refuses
//   node scripts/promo/calendar.mjs --calendar <folder>   # another folder

import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { basename, join, relative } from 'node:path';
import { pathToFileURL } from 'node:url';
import { readGenerated } from './brief.mjs';
import { montrealToday, readShot, root } from './shoot.mjs';

// Characters as most platforms count them, at the most: UTF-16 units, which are never fewer than the code points
// or the graphemes a platform counts (an emoji is two).
const units = text => text.length;

// What X counts: 23 for each address it links, 1 for a character of the Latin, Greek, Cyrillic and other early
// ranges and of the usual punctuation, 2 for any other (an emoji, an ellipsis, the narrow space French puts before
// "?"). Anything shaped as a domain is taken for an address, the safe side since an address is never more than 23;
// an email address is not linked, so it counts its characters.
const ADDRESSES = [/(?<![@\w.-])https?:\/\/\S+/gi, /(?<![@\w.-])[a-z0-9-]+(?:\.[a-z0-9-]+)*\.[a-z]{2,}\b(?![@.\w-])\S*/gi];
const LIGHT = [[0x0000, 0x10ff], [0x2000, 0x200d], [0x2010, 0x201f], [0x2032, 0x2037]];
// X leaves the punctuation that ends a sentence out of an address: it counts as characters.
const TRAILING = new Set(`.,;:!?'"»…)]`);
const trailing = address => {
  let end = address.length;
  while (end > 0 && TRAILING.has(address[end - 1])) end -= 1;
  return address.slice(end);
};
function weighted(text) {
  let addresses = 0;
  const rest = ADDRESSES.reduce((left, pattern) => left.replace(pattern, address => {
    addresses += 1;
    return trailing(address);
  }), text);
  let count = addresses * 23;
  for (const character of rest) {
    const point = character.codePointAt(0);
    count += LIGHT.some(([low, high]) => point >= low && point <= high) ? 1 : 2;
  }
  return count;
}

/**
 * The channels of the launch (the plan's question 5), each with the most characters its post's words may hold, as
 * it counts them: the title of a Short, a Reddit post's title, a post on X or Bluesky, the caption of a TikTok or a
 * Reel, a Discord message. Each names the ratios it shows whole: the vertical feeds crop or bar anything wider
 * than 9:16, and Shorts also take a square. Each also names the hosts its posts' links are on.
 */
const ANY = Object.freeze(['9:16', '16:9', '1:1']);
export const CHANNELS = Object.freeze({
  'youtube-shorts': Object.freeze({ limit: 100, count: units, ratios: Object.freeze(['9:16', '1:1']), hosts: ['youtube.com', 'youtu.be'] }),
  tiktok: Object.freeze({ limit: 2200, count: units, ratios: Object.freeze(['9:16']), hosts: ['tiktok.com'] }),
  'instagram-reels': Object.freeze({ limit: 2200, count: units, ratios: Object.freeze(['9:16']), hosts: ['instagram.com'] }),
  reddit: Object.freeze({ limit: 300, count: units, ratios: ANY, hosts: ['reddit.com', 'redd.it'] }),
  x: Object.freeze({ limit: 280, count: weighted, ratios: ANY, hosts: ['x.com', 'twitter.com'] }),
  bluesky: Object.freeze({ limit: 300, count: units, ratios: ANY, hosts: ['bsky.app'] }),
  discord: Object.freeze({ limit: 2000, count: units, ratios: ANY, hosts: ['discord.com', 'discord.gg'] }),
});

export { montrealToday };

export const STATUSES = Object.freeze(['draft', 'approved', 'published']);
export const LANGUAGES = Object.freeze(['en', 'fr']);

/** What the words of a post say when it shows a restyled clip (ADR 0205): its post says it is restyled. */
export const RESTYLED = Object.freeze({ en: 'Gameplay restyled by a model.', fr: 'Partie restylisée par un modèle.' });

const KEYS = ['hour', 'channels', 'media', 'status'];
const HOUR = /^(?:[01]\d|2[0-3]):[0-5]\d$/;
const NAME = /^(\d{4})-(\d{2})-(\d{2})-[a-z0-9][a-z0-9-]*\.md$/;
const SLUG = /^[a-z0-9][a-z0-9-]*$/;

const list = value => value.split(',').map(item => item.trim()).filter(Boolean);

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
  const lines = text.replace(/^\uFEFF/, '').replaceAll('\r\n', '\n').split('\n');
  if (lines[0] !== '---') refuse('a post starts with its "---" front matter: hour, channels, media, status.');
  const end = lines.indexOf('---', 1);
  if (end < 0) refuse('the front matter ends with a "---" line.');
  const head = new Map();
  const links = new Map();
  for (const line of lines.slice(1, end).filter(line => line.trim())) frontLine(line, { head, links }, refuse);
  return { head, links, body: lines.slice(end + 1) };
}

// One line of the front matter: a key of the format, or the link of a channel.
function frontLine(line, { head, links }, refuse) {
  const match = /^([a-z][a-z-]*(?: [a-z][a-z-]*)?): ?(.*)$/.exec(line);
  if (!match) refuse(`the front matter's lines are "key: value", not ${JSON.stringify(line)}.`);
  const [, key, value] = match;
  const link = key.startsWith('link ');
  const into = link ? links : head;
  const name = link ? key.slice(5) : key;
  if (into.has(name)) refuse(`the front matter gives "${key}" twice.`);
  if (!link && !KEYS.includes(key)) refuse(`"${key}" is not a key of a post: ${KEYS.join(', ')} and "link <channel>".`);
  into.set(name, value.trim());
}

const HEADINGS = LANGUAGES.map(language => '"## ' + language + '"').join(', ');

// The one title, before the first language's heading.
function titleOf(body, refuse) {
  const first = body.findIndex(line => line.startsWith('## '));
  const titles = body.slice(0, first < 0 ? body.length : first).filter(line => line.startsWith('# '));
  if (titles.length > 1) refuse(`the post has one title, not ${titles.length}.`);
  const title = titles[0]?.slice(2).trim();
  if (!title) refuse('the post has its title, a "# " heading before its words, for the owner who reads the calendar.');
  return title;
}

// The lines under each language's heading.
function sectionsOf(body, refuse) {
  const sections = new Map();
  let current;
  for (const line of body) {
    if (line.startsWith('## ')) {
      current = line.slice(3).trim();
      if (!LANGUAGES.includes(current)) refuse(`"## ${current}" is not a language of a post: ${HEADINGS}.`);
      if (sections.has(current)) refuse(`"## ${current}" is given twice.`);
      sections.set(current, []);
    } else if (current) {
      if (line.startsWith('# ')) refuse(`the post has one title, before its words, not ${JSON.stringify(line)} under "## ${current}".`);
      sections.get(current).push(line);
    }
  }
  return sections;
}

// The title, and the words under each language's heading.
function words(body, refuse) {
  const title = titleOf(body, refuse);
  const sections = sectionsOf(body, refuse);
  const out = {};
  for (const language of LANGUAGES) {
    const text = (sections.get(language) ?? []).join('\n').trim();
    if (!text) refuse(`the post's words in "## ${language}" are missing.`);
    out[language] = text;
  }
  return { title, words: out };
}

const USAGE = 'each medium is "cut <cut>", "still <shot> <ratio>", "film <shot> <ratio>" or "generated <sha256>"';

// How each kind of medium is read: whether it takes a ratio, and what the repository holds of it.
const MEDIA = {
  cut: { ratio: false, read: (name, _, { cuts, refuse }) => {
    const path = join(cuts, `${name}.json`);
    if (!SLUG.test(name) || !existsSync(path)) refuse(`the cut ${JSON.stringify(name)} is one promo/cuts/ does not hold.`);
    try {
      return { ratio: JSON.parse(readFileSync(path, 'utf8')).ratio };
    } catch (error) {
      return refuse(`the cut ${name} is not a cut's JSON (${error.message}).`);
    }
  } },
  still: { ratio: true, read: (name, ratio, options) => ({ ratio: shotAt(name, ratio, options).ratio }) },
  film: { ratio: true, read: (name, ratio, options) => {
    if (!shotAt(name, ratio, options).film) options.refuse(`the shot ${name} has no film.`);
    return { ratio };
  } },
  generated: { ratio: false, read: (name, _, { generated, refuse }) => {
    const line = generated().find(file => file.sha256 === name);
    if (!line) refuse(`the generated file ${name} is one promo/generated.json does not hold.`);
    return { ratio: line.ratio, makes: line.makes };
  } },
};

// A shot of promo/shots/, taken at the ratio.
function shotAt(name, ratio, { shots, refuse }) {
  const path = join(shots, `${name}.json`);
  if (!SLUG.test(name) || !existsSync(path)) refuse(`the shot ${JSON.stringify(name)} is one promo/shots/ does not hold.`);
  const shot = readShot(path);
  if (!shot.ratios.includes(ratio)) refuse(`the shot ${name} is not taken at ${ratio}: ${shot.ratios.join(', ')}.`);
  return { ratio, film: shot.film };
}

// One medium of the post, which the repository holds: a cut, a shot's still or film, or a generated file by hash.
function medium(entry, options) {
  const [kind, name, ratio, ...rest] = entry.split(/\s+/);
  const reader = Object.hasOwn(MEDIA, kind) ? MEDIA[kind] : undefined;
  if (!reader || rest.length || !name || reader.ratio !== (ratio !== undefined)) options.refuse(`${USAGE}, not ${JSON.stringify(entry)}.`);
  return { kind, name, ...reader.read(name, ratio, options) };
}

// The channels a post goes to, each of the launch, each once.
function channelsOf(head, refuse) {
  const channels = list(head.get('channels') ?? '');
  if (channels.length === 0) refuse(`"channels" lists one or more of ${Object.keys(CHANNELS).join(', ')}.`);
  for (const channel of channels) if (!Object.hasOwn(CHANNELS, channel)) refuse(`the channel ${JSON.stringify(channel)} is not one of ${Object.keys(CHANNELS).join(', ')}.`);
  if (new Set(channels).size !== channels.length) refuse('"channels" names a channel twice.');
  return channels;
}

// What a post shows, each medium once.
function mediaOf(head, options) {
  const entries = list(head.get('media') ?? '');
  if (entries.length === 0) options.refuse('"media" names what the post shows: a cut, a still, a film or a generated file.');
  if (new Set(entries).size !== entries.length) options.refuse('"media" names a medium twice.');
  return entries.map(entry => medium(entry, options));
}

// The record of generated files, read once, when a post names one; its refusal names the post.
function recordReader(generated, readRecord, path, refuse) {
  let record;
  return () => {
    if (record) return record;
    if (!existsSync(generated)) refuse(`it names a generated file, and the record ${generated} is missing.`);
    try {
      record = readRecord(generated);
    } catch (error) {
      refuse(`it names a generated file, and ${error.message}`);
    }
    return record;
  };
}

// The hour the owner posts it, Montreal's, on a 24-hour clock.
function hourOf(head, refuse) {
  const hour = head.get('hour') ?? '';
  if (!HOUR.test(hour)) refuse(`"hour" is when the owner posts it, Montreal's time, as HH:MM on a 24-hour clock, not ${JSON.stringify(hour)}.`);
  return hour;
}

// Each medium is at a ratio each channel shows whole.
function checkRatios(channels, media, refuse) {
  for (const item of media) {
    if (item.ratio === undefined) continue;
    for (const channel of channels) {
      if (!CHANNELS[channel].ratios.includes(item.ratio)) refuse(`it shows the ${item.kind} ${item.name} at ${item.ratio}, and ${channel} takes ${CHANNELS[channel].ratios.join(' or ')}.`);
    }
  }
}

// The words fit each channel as it counts them, and say a restyled clip is restyled.
function checkWords(said, channels, media, refuse) {
  const restyled = media.some(item => item.makes === 'restyled-clip');
  for (const language of LANGUAGES) {
    for (const channel of channels) {
      const count = CHANNELS[channel].count(said[language]);
      if (count > CHANNELS[channel].limit) refuse(`its words in ${language} are ${count} characters, and ${channel} takes ${CHANNELS[channel].limit}.`);
    }
    if (restyled && !said[language].includes(RESTYLED[language])) refuse(`it shows a restyled clip, so its words in ${language} say "${RESTYLED[language]}" (ADR 0205).`);
  }
}

// The host of a post's link, on its channel's own site.
function linkHost(channel, link, refuse) {
  let address;
  try {
    address = new URL(link);
  } catch {
    address = undefined;
  }
  if (address?.protocol !== 'https:' || /\s/.test(link) || address.pathname.length < 2) refuse(`"link ${channel}" is the post's https:// address.`);
  const host = address.hostname;
  if (!CHANNELS[channel].hosts.some(known => host === known || host.endsWith(`.${known}`))) refuse(`"link ${channel}" is on ${CHANNELS[channel].hosts.join(' or ')}, not ${host}.`);
}

// A published post gives each channel's link, and is not dated after today; another gives none.
function checkLinks(links, { channels, status, date, today }, refuse) {
  for (const [channel, link] of links) {
    if (!channels.includes(channel)) refuse(`"link ${channel}" names ${channel}, which is not one of its channels.`);
    linkHost(channel, link, refuse);
  }
  if (status !== 'published') {
    if (links.size > 0) refuse(`it is ${status}, so it gives no link yet: a link comes once it is published.`);
    return;
  }
  if (date > today) refuse(`it is published, and dated ${date}, after today (${today}).`);
  for (const channel of channels) if (!links.has(channel)) refuse(`it is published, so it gives the link of ${channel}: "link ${channel}: https://...".`);
}

/**
 * One post read and checked. Refused: a name that is not its date, a key not in the format, no hour, no channel or
 * one not of the launch, no media or media the repository does not hold, a medium at a ratio a channel does not
 * show whole, words missing in a language or over a channel's
 * limit, a restyled clip its words do not call restyled, a link before it is published, and a published post
 * without the link of each channel, or dated after today.
 */
export function readPost(path, {
  cuts = join(root, 'promo/cuts'),
  shots = join(root, 'promo/shots'),
  generated = join(root, 'promo/generated.json'),
  today = montrealToday(),
  readRecord = file => readGenerated(file, { shots, until: today }),
} = {}) {
  const file = basename(path);
  const refuse = why => { throw new Error(`${path}: ${why}`); };
  const date = dayOf(file, refuse);
  const { head, links, body } = split(readFileSync(path, 'utf8'), refuse);
  const { title, words: said } = words(body, refuse);

  const hour = hourOf(head, refuse);
  const channels = channelsOf(head, refuse);
  const media = mediaOf(head, { cuts, shots, refuse, generated: recordReader(generated, readRecord, path, refuse) });
  checkRatios(channels, media, refuse);
  checkWords(said, channels, media, refuse);
  const status = head.get('status');
  if (!STATUSES.includes(status)) refuse(`"status" is ${STATUSES.slice(0, -1).join(', ')} or ${STATUSES.at(-1)}.`);
  checkLinks(links, { channels, status, date, today }, refuse);
  return { file, date, hour, title, channels, media, words: said, status, links: Object.fromEntries(links) };
}

/** Every post of the calendar, in the order of their dates. Refuses a folder that is missing or holds no post. */
export function readCalendar(folder, options = {}) {
  if (!existsSync(folder)) throw new Error(`${folder}: the calendar is missing.`);
  const entries = readdirSync(folder).filter(name => name !== 'README.md');
  const other = entries.find(name => !name.endsWith('.md'));
  if (other) throw new Error(`${join(folder, other)}: the calendar holds posts only, each a YYYY-MM-DD-<name>.md file.`);
  const names = entries.sort((a, b) => a.localeCompare(b));
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
    console.log(`${folder.startsWith(root) ? relative(root, folder) : folder}: ${posts.length} post(s), ${count('draft')} draft, ${count('approved')} approved, ${count('published')} published.`);
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}
