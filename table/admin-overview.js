// Headquarters' Overview (docs/tabletop/admin-plan.md, A5): the numbers at a glance, each a card that links to the
// tab that explains it, and drawn in the warning colour when it needs the operator: a failed tending run, turns given
// up on, registration closed. The host counts; this page draws its answer, a card per source the host has.

import { ago } from './admin-players.js';

function plural(count, one, many = one + 's') {
  return `${count} ${count === 1 ? one : many}`;
}

const unavailable = section => section?.unavailable === true;

/** A card for a source that did not answer: its place kept, its number a dash. */
function emptyCard(card) {
  return { ...card, number: '—', detail: 'The host could not read this just now.', warn: false };
}

// One card a source, each built from its own part of the host's answer, in the order the operator reads them.
const CARDS = [
  { id: 'players', tab: 'players', label: 'Players', from: answer => answer?.players,
    draw: players => ({
      number: String(players.total),
      detail: [`${players.seenThisWeek} seen this week`, `${players.newThisWeek} new`, players.suspended ? `${players.suspended} suspended` : null].filter(Boolean).join(' · '),
    }) },
  { id: 'in-progress', tab: 'tables', label: 'Games in progress', from: answer => answer?.games?.inProgress,
    draw: games => ({ number: String(games.againstPlayers + games.againstBots), detail: `${games.againstPlayers} between players · ${games.againstBots} against bots` }) },
  { id: 'finished', tab: 'sessions', label: 'Finished in 24 hours', from: answer => answer?.games?.finished,
    draw: finished => ({ number: String(finished.lastDay), detail: `${finished.lastWeek} in 7 days · ${finished.voidedThisWeek} voided` }) },
  { id: 'waiting', tab: 'tables', label: 'Open seats', from: answer => answer?.waiting,
    draw: waiting => ({
      number: String(waiting.openSeats),
      detail: `${plural(waiting.invitations, 'invitation')} unanswered · ${plural(waiting.endingSoon, 'game')} out of time within a day`,
    }) },
  { id: 'tending', tab: 'jobs', label: 'Last tending run', from: answer => answer?.jobs?.lastRun, draw: tendingCard },
  { id: 'turns', tab: 'jobs', label: 'Bot turns waiting', from: answer => answer?.jobs?.turns,
    draw: turns => ({ number: String(turns.waiting), detail: turns.failed ? `${plural(turns.failed, 'turn')} given up on` : 'None given up on', warn: turns.failed > 0 }) },
  { id: 'registration', tab: 'settings', label: 'Registration', from: answer => answer?.registration,
    draw: registration => ({
      number: registration.open ? 'Open' : 'Closed',
      detail: registration.open ? 'New players can make an account.' : 'No new account can be made.',
      warn: !registration.open,
    }) },
];

function tendingCard(run, now) {
  if (run.outcome === 'none') return { number: '—', detail: 'No run kept yet.' };
  const failed = run.outcome === 'failed';
  return { number: ago(run.started, now), detail: failed ? 'It failed: open Jobs for its error.' : 'It completed.', warn: failed };
}

/** Every card the host's answer gives, in the order the operator reads them; none for a source this host lacks. */
export function overviewCards(answer, now = Date.now()) {
  return CARDS.flatMap(({ from, draw, ...card }) => {
    const section = from(answer);
    if (!section) return [];
    if (unavailable(section)) return [emptyCard(card)];
    return [{ ...card, warn: false, ...draw(section, now) }];
  });
}

// ---- The tab itself. Everything above is pure and tested; this part only moves it onto the screen.

function made(tag, className, text) {
  const one = document.createElement(tag);
  if (className) one.className = className;
  if (text !== undefined) one.textContent = text;
  return one;
}

/** Draws the Overview into the page from the host's answer; `send(method, path)` is the admin transport's. */
export function mountOverview({ send, element }) {
  async function redraw() {
    const answer = await send('GET', '/api/admin/overview');
    const told = element('overview-said');
    if (!answer.ok) {
      told.textContent = answer.body?.message ?? `The host answered ${answer.status}.`;
      told.classList.add('refused');
      return;
    }
    told.textContent = '';
    told.classList.remove('refused');
    const cards = overviewCards(answer.body);
    element('overview-cards').replaceChildren(...cards.map(card => {
      const item = made('li');
      const link = made('a', `overview-card${card.warn ? ' warning' : ''}`);
      link.href = `#${card.tab}`;
      link.dataset.card = card.id;
      link.append(made('span', 'overview-label', card.label), made('strong', 'overview-number', card.number), made('span', 'overview-detail muted', card.detail));
      item.append(link);
      return item;
    }));
    element('tab-overview-count').textContent = cards.some(card => card.warn) ? '!' : '';
  }

  return { redraw };
}
