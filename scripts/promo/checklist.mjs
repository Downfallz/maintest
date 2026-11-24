// The promotion's posting checklist (docs/art/promotion-plan.md, P7; promo/README.md#the-checklist): the posts of
// a batch as the steps the owner follows on a phone, in the order they are posted, one step a channel: the hour,
// the channel, where the file is and the words to paste. The owner answers with each post's links, and a session
// marks it published. Nothing is posted from here: no platform API, no account, no secret.
//
//   node scripts/promo/checklist.mjs --page <url> [<post>...]                     # the checklist, in Markdown
//   node scripts/promo/checklist.mjs --approve <post>...                          # merged drafts become approved
//   node scripts/promo/checklist.mjs --publish <post> --link <channel> <url>...   # a post the owner posted
//
// A post is its file under promo/calendar/, by path or by name. `--calendar <folder>` reads another folder.

import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { basename, join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { CHANNELS, readCalendar, readPost } from './calendar.mjs';
import { readMedia } from './media.mjs';
import { root } from './shoot.mjs';

/** Each channel as the owner finds it on a phone. */
export const CHANNEL_NAMES = Object.freeze({
  'youtube-shorts': 'YouTube Shorts',
  tiktok: 'TikTok',
  'instagram-reels': 'Instagram Reels',
  reddit: 'Reddit',
  x: 'X',
  bluesky: 'Bluesky',
  discord: 'Discord',
});

const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
const longDay = date => `${Number(date.slice(8, 10))} ${MONTHS[Number(date.slice(5, 7)) - 1]} ${date.slice(0, 4)}`;
const name = post => post.file.replace(/\.md$/, '');

/** The posts in the order the owner posts them: by day, then by hour, then by name. */
export function inPostingOrder(posts) {
  return [...posts].sort((a, b) => `${a.date} ${a.hour} ${a.file}`.localeCompare(`${b.date} ${b.hour} ${b.file}`));
}

// The page is the batch's contact sheet, where a cut, a still and a generated file are taken (/promo, step 8).
function pageLink(page, what) {
  if (!page) throw new Error(`${what} is taken from the batch's contact sheet page: give its link with --page <url>.`);
  return `${what}, on [the contact sheet page](${page})`;
}

// Where the owner finds one medium of a post.
function where(item, { page, media }) {
  if (item.kind === 'cut') return pageLink(page, `the cut \`${item.name}\` at ${item.ratio}: the English MP4 with the English words, the French MP4 with the French words`);
  if (item.kind === 'still') return pageLink(page, `the still of \`${item.name}\` at ${item.ratio}`);
  if (item.kind === 'generated') return pageLink(page, `the generated file \`${item.name.slice(0, 12)}\` at ${item.ratio}`);
  const file = `film-${item.ratio.replace(':', 'x')}.mp4`;
  const line = media().find(entry => entry.shot === item.name && entry.file === file);
  if (!line) {
    return `the film of \`${item.name}\` at ${item.ratio}, not filmed on \`main\` yet: GitHub, *Actions*, *Film a promotion shot*, `
      + `*Run workflow*, the branch \`main\`, \`promo/shots/${item.name}.json\`, the ratio \`${item.ratio}\``;
  }
  return `the film of \`${item.name}\` at ${item.ratio}, in the Azure portal: *Storage accounts*, the table's account, *Containers*, \`promo\`, \`${line.blob}\``;
}

// The words as a block the owner copies whole.
const block = (label, text) => [`   ${label}:`, '', '   ```text', ...text.split('\n').map(line => (line ? `   ${line}` : '')), '   ```', ''];

/**
 * The checklist in Markdown: a heading a post, then one numbered step a channel, numbered across the batch, each with
 * the hour (Montreal's), the channel, where each medium is, and the English and French words, one block each. It
 * ends with the answer the owner gives once posted. `page` is the batch's contact sheet page, needed when a post
 * shows a cut, a still or a generated file; `media` reads promo/media.json, where a film's blob is.
 */
export function checklist(posts, { page, media = () => readMedia(join(root, 'promo/media.json')) } = {}) {
  if (page !== undefined && !/^https:\/\/\S+$/.test(page)) throw new Error(`--page is the contact sheet page's https:// link, not ${JSON.stringify(page)}.`);
  const waiting = posts.filter(post => post.status !== 'published');
  if (waiting.length === 0) throw new Error('the checklist has no post to publish: each one named is published.');
  let records;
  const options = { page, media: () => (records ??= media()) };
  const lines = [
    '## Posting checklist',
    '',
    'In this order, on a phone. Each step: take the file, paste the words, post on the channel at the hour, Montreal\'s.',
    '',
  ];
  let step = 0;
  for (const post of inPostingOrder(waiting)) {
    lines.push(`### ${longDay(post.date)}, ${post.hour}: ${post.title}`, '', `\`${name(post)}\`, ${post.status}`, '');
    const said = post.media.map(item => where(item, options)).join('; ');
    const files = said[0].toUpperCase() + said.slice(1);
    for (const channel of post.channels) {
      step += 1;
      lines.push(`${step}. **${post.hour}, ${CHANNEL_NAMES[channel]}.** ${files}.`, '');
      lines.push(...block('English', post.words.en), ...block('French', post.words.fr));
    }
  }
  lines.push(
    '### Once posted',
    '',
    'Answer with each post\'s link, one line a channel, and a session marks the post published:',
    '',
    '```text',
    ...inPostingOrder(waiting).flatMap(post => post.channels.map(channel => `${name(post)} ${channel} https://...`)),
    '```',
    '',
  );
  return lines.join('\n');
}

// A post's file, by its path or its name under the calendar.
function postPath(post, calendar) {
  if (existsSync(post) && post.endsWith('.md')) return post;
  const path = join(calendar, post.endsWith('.md') ? basename(post) : `${basename(post)}.md`);
  if (!existsSync(path)) throw new Error(`${post}: the calendar ${calendar} holds no such post.`);
  return path;
}

// The post's front matter given a new status and these links, checked as any post is; refused, the file is kept.
function rewrite(path, status, links, options) {
  const before = readFileSync(path, 'utf8');
  const lines = before.replaceAll('\r\n', '\n').split('\n');
  const end = lines.indexOf('---', 1);
  const head = lines.slice(1, end).filter(line => !line.startsWith('link ')).map(line => (line.startsWith('status:') ? `status: ${status}` : line));
  const after = ['---', ...head, ...Object.entries(links).map(([channel, link]) => `link ${channel}: ${link}`), ...lines.slice(end)].join('\n');
  writeFileSync(path, after);
  try {
    return readPost(path, options);
  } catch (error) {
    writeFileSync(path, before);
    throw error;
  }
}

/** Drafts whose pull request is merged become approved. A post that is not a draft is refused. */
export function approve(post, { calendar = join(root, 'promo/calendar'), ...options } = {}) {
  const path = postPath(post, calendar);
  const read = readPost(path, options);
  if (read.status !== 'draft') throw new Error(`${path}: it is ${read.status}, and only a draft becomes approved.`);
  return rewrite(path, 'approved', {}, options);
}

/**
 * A post the owner posted becomes published, with the link of each of its channels; refused, as readPost refuses,
 * when a link is missing, off its channel's site or of a channel it does not go to, or the post is dated after
 * today, and the file is kept as it was.
 */
export function publish(post, links, { calendar = join(root, 'promo/calendar'), ...options } = {}) {
  const path = postPath(post, calendar);
  const read = readPost(path, options);
  if (read.status === 'published') throw new Error(`${path}: it is published already.`);
  for (const channel of Object.keys(links)) if (!Object.hasOwn(CHANNELS, channel)) throw new Error(`${path}: "${channel}" is not a channel of the launch.`);
  return rewrite(path, 'published', links, options);
}

const USAGE = 'usage: node scripts/promo/checklist.mjs [--calendar <folder>] (--page <url> [<post>...] | --approve <post>... | --publish <post> --link <channel> <url>...)';

// The command's arguments: the calendar, the mode, the posts, the page and the links.
export function parseArguments(argv) {
  const parsed = { mode: 'checklist', posts: [], links: {} };
  const value = (index, flag) => {
    if (argv[index + 1] === undefined) throw new Error(`${flag} takes a value. ${USAGE}`);
    return argv[index + 1];
  };
  for (let index = 0; index < argv.length; index += 1) {
    const argument = argv[index];
    if (argument === '--calendar') parsed.calendar = value(index++, argument);
    else if (argument === '--page') parsed.page = value(index++, argument);
    else if (argument === '--approve' || argument === '--publish') {
      if (parsed.mode !== 'checklist') throw new Error(USAGE);
      parsed.mode = argument.slice(2);
    } else if (argument === '--link') {
      const channel = value(index, argument);
      const link = value(index + 1, argument);
      if (Object.hasOwn(parsed.links, channel)) throw new Error(`--link ${channel} is given twice.`);
      parsed.links[channel] = link;
      index += 2;
    } else if (argument.startsWith('--')) throw new Error(USAGE);
    else parsed.posts.push(argument);
  }
  const { mode, posts, links, page } = parsed;
  if (mode !== 'publish' && Object.keys(links).length) throw new Error(`--link goes with --publish. ${USAGE}`);
  if (mode !== 'checklist' && page !== undefined) throw new Error(`--page goes with the checklist. ${USAGE}`);
  if (mode === 'approve' && posts.length === 0) throw new Error(USAGE);
  if (mode === 'publish' && (posts.length !== 1 || Object.keys(links).length === 0)) throw new Error(USAGE);
  return parsed;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    const { mode, posts, links, page, calendar = join(root, 'promo/calendar') } = parseArguments(process.argv.slice(2));
    if (mode === 'approve') {
      for (const post of posts) console.log(`${approve(post, { calendar }).file}: approved.`);
    } else if (mode === 'publish') {
      console.log(`${publish(posts[0], links, { calendar }).file}: published.`);
    } else {
      const read = posts.length ? posts.map(post => readPost(postPath(post, calendar))) : readCalendar(calendar);
      process.stdout.write(checklist(read, { page }));
    }
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}
