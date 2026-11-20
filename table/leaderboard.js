import { onLobbyChange } from './changes.js';
import { numberChanged } from './motion.js';

// The ladder (ADR 0124): who ranks where among the players with an account, and, for whoever is signed in,
// their own place and their record against each bot. The host computes everything; this page only draws it.

/** A record as a ladder writes it: wins, losses and draws. */
export function recordText({ wins, losses, draws }) {
  return `${wins}–${losses}–${draws}`;
}

/** A record in words, which name the parts of the drawn bar. */
export function recordWords({ wins, losses, draws }) {
  return `${wins} won, ${losses} lost, ${draws} drawn`;
}

/** A rating and how sure it is, as one line. */
export function ratingText({ rating, deviation }) {
  return `${rating} ±${deviation}`;
}

/** What the signed-in panel says, from the host's `you`: signed out, signed in but unranked, or ranked. */
export function youSummary(you) {
  if (!you) return { kind: 'signed-out', text: 'Sign in on the home page to have a place here.' };
  if (!you.standing) return { kind: 'unranked', text: `${you.username}, you have no ranked game yet. Play another player online: the result counts here.` };
  const place = you.standing.provisional ? 'provisional' : `#${you.standing.rank}`;
  return { kind: 'ranked', text: `${you.username} · ${place} · ${recordText(you.standing)} in ${you.standing.games} game${you.standing.games === 1 ? '' : 's'}` };
}

/** The place as the row shows it: the host's rank, and Provisional beside it when the host says so. */
export function placeText(standing) {
  return standing.provisional ? `#${standing.rank} · Provisional` : `#${standing.rank}`;
}

/**
 * How the player's standing moved since this browser last showed it to them (`remembered`, undefined when it never
 * did). A difference of two numbers the page showed, never one the host did not send. Null: nothing to say.
 */
export function placeMove(remembered, standing) {
  if (!remembered || !standing) return null;
  const before = remembered.standing;
  if (!before) return { text: 'New on the ladder.' };
  const rating = standing.rating - before.rating;
  const rank = before.rank !== standing.rank ? `#${before.rank} → #${standing.rank}` : '';
  if (!rating && !rank) return null;
  if (!rating) return { text: `${rank} since you last looked.`, rating, rank };
  const place = rank ? ` ${rank}.` : '';
  return { text: `${rating > 0 ? 'Up' : 'Down'} ${Math.abs(rating)} since you last looked.${place}`, rating, rank };
}

/** The ladder's ratings as one band, lowest to highest: where each player stands on it, in percent, the player's own marked. */
export function ratingBand(players) {
  if (!players?.length) return null;
  const ratings = players.map(player => player.rating);
  const low = Math.min(...ratings), high = Math.max(...ratings);
  const at = rating => high === low ? 50 : Math.round((100 * (rating - low)) / (high - low));
  const clamp = value => Math.max(0, Math.min(100, value));
  const you = players.find(player => player.you);
  const span = you && high !== low ? [clamp(at(you.rating - you.deviation)), clamp(at(you.rating + you.deviation))] : null;
  return { low, high, span, ticks: players.map(player => ({ at: at(player.rating), you: Boolean(player.you) })) };
}

/** A record as three parts, each as long as its share of the games, or null before a first game. */
export function recordParts({ wins, losses, draws }) {
  const games = wins + losses + draws;
  if (!games) return null;
  const share = count => Math.round((100 * count) / games);
  return [{ part: 'won', share: share(wins) }, { part: 'lost', share: share(losses) }, { part: 'drawn', share: share(draws) }];
}

/** What the status says when the player's rating changes while the page is open. */
export function statusText(before, standing) {
  const by = before?.rating === undefined || before?.rating === null ? 0 : standing.rating - before.rating;
  if (by) return `Your rating is now ${standing.rating}, ${by > 0 ? 'up' : 'down'} ${Math.abs(by)}.`;
  // Others' games can move the player's place while their rating stays.
  if (before?.rank && before.rank !== standing.rank) return `Your place is now #${standing.rank}, from #${before.rank}.`;
  return `Your rating is now ${standing.rating}.`;
}

/** Where this browser remembers what the ladder last showed a player, by their name (`/api/leaderboard` names no id). */
export const SEEN_KEY = 'downfall.ladder.seen.v1:';

function remembered(storage, username) {
  try { const kept = storage?.getItem(SEEN_KEY + username); return kept ? JSON.parse(kept) : undefined; } catch { return undefined; }
}

function remember(storage, username, standing) {
  try { storage?.setItem(SEEN_KEY + username, JSON.stringify({ standing: standing ? { rank: standing.rank, rating: standing.rating, provisional: standing.provisional } : null })); } catch { /* a private window keeps nothing: nothing moves */ }
}

function node(doc, tag, text, className) {
  const element = doc.createElement(tag);
  if (text !== undefined && text !== null) element.textContent = text;
  if (className) element.className = className;
  return element;
}

function initial(doc, name) {
  const square = node(doc, 'span', String(name ?? '').slice(0, 1).toUpperCase() || '?', 'account-initial');
  square.setAttribute('aria-hidden', 'true');
  return square;
}

/** The record drawn: one bar, its parts as long as the games, each named in words by the line beside it. */
export function recordBar(doc, parts) {
  const bar = node(doc, 'span', null, 'record-bar');
  bar.setAttribute('aria-hidden', 'true');
  for (const { part, share } of parts) {
    const piece = node(doc, 'span', null, `record-${part}`);
    piece.style.setProperty('--share', share);
    bar.append(piece);
  }
  return bar;
}

const OPEN_RANKED = '/lobby?rematch=person';

function openRanked(doc, filled) {
  const link = node(doc, 'a', 'Open a ranked table →', filled ? 'primary page-action' : 'button-link page-action');
  link.href = OPEN_RANKED;
  return link;
}

let drawnPlayers = null;
// What this browser remembered when the page opened: the move is said against it for the page's whole life, since
// the stream's first word reads the ladder again at once. `drawnStanding` is what the last draw showed.
let seenAtOpen;
let opened = false;
let drawnStanding;
let drawnRating = null;

const plural = (count, word) => `${count} ${word}${count === 1 ? '' : 's'}`;

function playerRow(doc, player, arrived) {
  const row = node(doc, 'tr');
  row.className = [player.provisional ? 'provisional' : '', player.you ? 'you' : '', player.rank <= 3 && !player.provisional ? 'top' : '', arrived ? 'motion-arrive' : ''].filter(Boolean).join(' ');
  const name = node(doc, 'td', null, 'player');
  // The cell stays a table cell; the line inside it lays out the initial, the name and the tags.
  const line = node(doc, 'span', null, 'player-line');
  name.append(line);
  line.append(initial(doc, player.username), node(doc, 'span', player.username, 'player-name'));
  if (player.you) line.append(node(doc, 'span', '(you)', 'you-mark'));
  if (player.provisional) {
    const tag = node(doc, 'span', 'provisional', 'tag');
    tag.title = 'Rating still being found: a few more ranked games settle it';
    line.append(tag);
  }
  const rating = node(doc, 'td', String(player.rating), 'number');
  rating.append(node(doc, 'span', `±${player.deviation}`, 'deviation'));
  row.append(node(doc, 'td', String(player.rank), 'rank'), name, rating, node(doc, 'td', recordText(player), 'number'), node(doc, 'td', String(player.games), 'number games'));
  return row;
}

function drawPlayers(doc, ladder) {
  const el = id => doc.getElementById(id);
  const arrived = new Set(drawnPlayers ? ladder.players.map(player => player.username).filter(name => !drawnPlayers.has(name)) : []);
  drawnPlayers = new Set(ladder.players.map(player => player.username));
  const rows = ladder.players.map(player => playerRow(doc, player, arrived.has(player.username)));
  el('ladder-rows').replaceChildren(...rows);
  el('ladder-table').hidden = rows.length === 0;
  el('ladder-empty').hidden = rows.length > 0;
  el('players-caption').hidden = rows.length === 0;
  el('players-count').textContent = ladder.players.length;
  el('provisional-above').textContent = ladder.provisionalAbove;
}

/** The player's rating drawn among the ladder's: a band with a tick a player, theirs marked, and its two ends. */
function drawnBand(doc, players, standing) {
  const band = ratingBand(players);
  if (!band) return null;
  const drawn = node(doc, 'div', null, 'rating-band');
  drawn.setAttribute('role', 'img');
  drawn.setAttribute('aria-label', `Your rating ${standing.rating} ±${standing.deviation}, among ${plural(players.length, 'player')} from ${band.low} to ${band.high}`);
  const track = node(doc, 'span', null, 'rating-track');
  if (band.span) {
    const span = node(doc, 'span', null, 'rating-span');
    span.style.setProperty('--from', band.span[0]);
    span.style.setProperty('--to', band.span[1]);
    track.append(span);
  }
  for (const tick of band.ticks) {
    const mark = node(doc, 'span', null, tick.you ? 'rating-tick mine' : 'rating-tick');
    mark.style.setProperty('--at', tick.at);
    track.append(mark);
  }
  const ends = node(doc, 'span', null, 'rating-ends');
  ends.setAttribute('aria-hidden', 'true');
  ends.append(node(doc, 'span', String(band.low)), node(doc, 'span', String(band.high)));
  drawn.append(track, ends);
  return drawn;
}

/** Your place for a player on the ladder: the rating, how it moved, the rating and the record drawn. */
function standingParts(doc, ladder, standing, move) {
  const parts = [node(doc, 'p', ratingText(standing), 'you-rating motion-number')];
  if (move) parts.push(node(doc, 'p', move.text, 'you-move'));
  const band = drawnBand(doc, ladder.players, standing);
  if (band) parts.push(band);
  const record = node(doc, 'p', null, 'you-record');
  const shares = recordParts(standing);
  if (shares) record.append(recordBar(doc, shares));
  record.append(node(doc, 'span', `${recordWords(standing)} in ${plural(standing.games, 'game')}`));
  parts.push(record, openRanked(doc, false));
  return parts;
}

function botsParts(doc, bots) {
  if (!bots?.length) return [];
  const list = node(doc, 'ul', null, 'bots');
  list.setAttribute('aria-label', 'Against the bots');
  for (const bot of bots) {
    const item = node(doc, 'li');
    item.append(node(doc, 'span', bot.bot), node(doc, 'span', recordText(bot)));
    list.append(item);
  }
  return [node(doc, 'p', 'Against the bots (not ranked)', 'eyebrow'), list];
}

export function drawLadder(doc, ladder, { storage = globalThis.localStorage, first = true } = {}) {
  const el = id => doc.getElementById(id);
  drawPlayers(doc, ladder);

  const standing = ladder.you?.standing ?? null;
  if (!opened && ladder.you) { seenAtOpen = remembered(storage, ladder.you.username); opened = true; }
  const key = standing ? `${standing.rank}:${standing.rating}` : null;
  const changed = drawnStanding !== undefined && drawnStanding !== key;
  const before = drawnRating;
  drawnRating = standing ? { rating: standing.rating, rank: standing.rank } : null;
  drawnStanding = key;
  const move = standing ? placeMove(seenAtOpen, standing) : null;

  const you = [];
  if (ladder.you) {
    const head = node(doc, 'p', null, 'you-who');
    head.append(initial(doc, ladder.you.username), node(doc, 'strong', ladder.you.username));
    if (standing) head.append(node(doc, 'span', placeText(standing), 'you-place motion-number'));
    you.push(head);
    if (standing) you.push(...standingParts(doc, ladder, standing, move));
    else you.push(node(doc, 'p', 'Not on the ladder yet. Your first ranked game against another player puts you on it.', 'you-line'), openRanked(doc, true));
    remember(storage, ladder.you.username, standing);
  } else you.push(node(doc, 'p', youSummary(null).text, 'you-line'));
  you.push(...botsParts(doc, ladder.you?.bots));
  el('you').replaceChildren(...you);

  // A place that moved plays the kit's value that changes, once: when the page opens on a move since this browser
  // last showed it, and when a later read changes what the page shows; not on a read that changes nothing.
  const moved = first ? Boolean(move) : changed;
  if (moved) for (const number of el('you').querySelectorAll('.motion-number')) numberChanged(number);
  // At rest the status says nothing; a change while the page is open is told once.
  el('ladder-status').textContent = !first && changed && standing ? statusText(before, standing) : '';
}

/** A scrolling table can be reached by the keyboard, so it can be scrolled without a pointer (WCAG 2.1.1). */
export function focusableScrolls(doc) {
  for (const box of doc.querySelectorAll('.ladder-scroll')) box.tabIndex = 0;
}

let loaded = false;

async function load(doc) {
  const problem = doc.getElementById('ladder-problem');
  try {
    const response = await fetch('/api/leaderboard', { credentials: 'same-origin' });
    if (response.status === 404) {
      doc.getElementById('ladder-status').textContent = 'This host keeps no ladder: players do not sign in here.';
      return;
    }
    if (!response.ok) throw new Error(`The host answered ${response.status}. Try again.`);
    problem.hidden = true;
    drawLadder(doc, await response.json(), { first: !loaded });
    loaded = true;
  } catch (error) {
    problem.textContent = error.message || 'Could not read the ladder. Try again.';
    problem.hidden = false;
  }
}

// The statistics import the drawn record from here: the ladder reads itself on its own page only.
if (typeof document !== 'undefined' && document.getElementById('ladder-rows')) {
  focusableScrolls(document);
  // Read when a game ends or begins somewhere (the lobby's public changes, ADR 0164) and when the stream opens --
  // the page loaded, back in view -- rather than on a clock. A ladder moves only when a ranked game ends.
  onLobbyChange(event => { if (!document.hidden && (event.event === 'ready' || String(event.data).startsWith('lobby'))) void load(document); });
  await load(document);
}
