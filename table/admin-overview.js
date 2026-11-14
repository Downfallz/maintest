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

/** Every card the host's answer gives, in the order the operator reads them; none for a source this host lacks. */
export function overviewCards(answer, now = Date.now()) {
  const cards = [];
  const players = answer?.players;
  if (players) {
    const card = { id: 'players', tab: 'players', label: 'Players' };
    cards.push(unavailable(players) ? emptyCard(card) : {
      ...card,
      number: String(players.total),
      detail: [`${players.seenThisWeek} seen this week`, `${players.newThisWeek} new`, players.suspended ? `${players.suspended} suspended` : null].filter(Boolean).join(' · '),
      warn: false,
    });
  }

  const progress = answer?.games?.inProgress;
  if (progress) {
    const card = { id: 'in-progress', tab: 'tables', label: 'Games in progress' };
    cards.push(unavailable(progress) ? emptyCard(card) : {
      ...card,
      number: String(progress.againstPlayers + progress.againstBots),
      detail: `${progress.againstPlayers} between players · ${progress.againstBots} against bots`,
      warn: false,
    });
  }

  const finished = answer?.games?.finished;
  if (finished) {
    const card = { id: 'finished', tab: 'sessions', label: 'Finished in 24 hours' };
    cards.push(unavailable(finished) ? emptyCard(card) : {
      ...card,
      number: String(finished.lastDay),
      detail: `${finished.lastWeek} in 7 days · ${finished.voidedThisWeek} voided`,
      warn: false,
    });
  }

  const waiting = answer?.waiting;
  if (waiting) {
    const card = { id: 'waiting', tab: 'tables', label: 'Open seats' };
    cards.push(unavailable(waiting) ? emptyCard(card) : {
      ...card,
      number: String(waiting.openSeats),
      detail: `${plural(waiting.invitations, 'invitation')} unanswered · ${plural(waiting.endingSoon, 'game')} out of time within a day`,
      warn: false,
    });
  }

  const run = answer?.jobs?.lastRun;
  if (run) {
    const card = { id: 'tending', tab: 'jobs', label: 'Last tending run' };
    if (unavailable(run)) cards.push(emptyCard(card));
    else if (run.outcome === 'none') cards.push({ ...card, number: '—', detail: 'No run kept yet.', warn: false });
    else cards.push({ ...card, number: ago(run.started, now), detail: run.outcome === 'failed' ? 'It failed: open Jobs for its error.' : 'It completed.', warn: run.outcome === 'failed' });
  }

  const turns = answer?.jobs?.turns;
  if (turns) {
    const card = { id: 'turns', tab: 'jobs', label: 'Bot turns waiting' };
    cards.push(unavailable(turns) ? emptyCard(card) : {
      ...card,
      number: String(turns.waiting),
      detail: turns.failed ? `${plural(turns.failed, 'turn')} given up on` : 'None given up on',
      warn: turns.failed > 0,
    });
  }

  const registration = answer?.registration;
  if (registration) {
    cards.push({
      id: 'registration', tab: 'settings', label: 'Registration',
      number: registration.open ? 'Open' : 'Closed',
      detail: registration.open ? 'New players can make an account.' : 'No new account can be made.',
      warn: !registration.open,
    });
  }

  return cards;
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
