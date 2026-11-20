// The tutorials outside a game (ADR 0187, docs/tabletop/onboarding-plan.md, O4): the lobby's Learn to play section,
// the first tutorial opening by itself for an account it is new to, and the settings' way back to it. The host says
// all of it in `GET /api/tutorials`: the series, where the account stands with each (new, done, skipped), and the
// one that opens by itself. A host that ships no tutorial, or a player signed out, shows no section.

import { pictureElement } from './picture.js';

const OFFERED_KEY = 'downfall.arena.tutorial-offered.v1';

/**
 * Each lesson's painting (docs/art/journeys/lessons.md, UX8): the series read as growth, from the creature a team
 * starts as to a Stage III. Declared for the home in docs/art/page-pictures.json.
 */
const PICTURES = Object.freeze({ 1: 'lesson-base', 2: 'lesson-brute', 3: 'lesson-ironhide', 4: 'lesson-colossus' });
const TAGS = Object.freeze({ done: 'Done', skipped: 'Skipped' });

/**
 * Each tutorial as a card: its place in the series, its title and goal, its state as the host says it, its link, its
 * painting, and whether it is the next one: the first in the series' order the host does not say done.
 */
export function tutorialCards(answer) {
  const listed = (answer?.tutorials ?? []).filter(one => one?.link);
  const next = [...listed].sort((one, other) => (one.order ?? 0) - (other.order ?? 0)).find(one => one.state !== 'done');
  return listed.map(one => {
    const done = one.state === 'done';
    return {
      id: one.id,
      title: one.title || `Tutorial ${one.order}`,
      place: `Tutorial ${one.order} of ${Math.max(one.of ?? 0, one.order ?? 0)}`,
      goal: one.goal ?? '',
      done,
      skipped: one.state === 'skipped',
      next: one === next,
      tag: TAGS[one.state] ?? 'New',
      action: done ? 'Play again' : 'Play',
      link: one.link,
      picture: PICTURES[one.order] ?? null,
    };
  });
}

/** How far the account is in the series, in words: the tutorials the host says done. */
export function seriesProgress(cards) {
  const done = cards.filter(one => one.done).length, of = cards.length;
  return { done, of, label: of ? `${done} of ${of} done` : '' };
}

/** The first tutorial of the series, the one the settings open again; none where the host ships none. */
export function firstTutorial(answer) {
  return (answer?.tutorials ?? []).find(one => one?.order === 1 && one.link)?.link ?? null;
}

/**
 * The tutorial to open now, by itself: the one the host says opens (`opens`), once a tab (the mark is the tab's
 * session storage). The host stops saying it once the account finished, skipped or left it; the mark keeps a
 * tutorial that could not open from taking the home page away each time the person comes back to it.
 */
export function opensByItself(answer, session) {
  const link = typeof answer?.opens === 'string' && answer.opens.startsWith('/battle?scenario=') ? answer.opens : null;
  if (!link) return null;
  try {
    if (session?.getItem(OFFERED_KEY)) return null;
    session?.setItem(OFFERED_KEY, '1');
  } catch { return null; }
  return session ? link : null;
}

const fetched = path => globalThis.fetch(path, { credentials: 'same-origin' });

async function series(request) {
  try {
    const response = await request('/api/tutorials');
    return response.ok ? await response.json() : null;
  } catch { return null; }
}

function made(doc, tag, text, className) {
  const one = doc.createElement(tag);
  if (text !== undefined) one.textContent = text;
  if (className) one.className = className;
  return one;
}

/**
 * Draws the Learn to play section for whoever is signed in, and opens the first tutorial for an account it is new
 * to: `setAccount(account)` reads the series again for them, and hides the section for nobody.
 * Resolves true when it opens the first tutorial, which `opening` is told first. `listed` is told the cards each
 * read draws (null for nobody signed in): the home's featured space offers the next one.
 * @param {{ request?: (path: string) => Promise<Response>, session?: Storage | null, go?: (link: string) => void, hash?: () => string, plain?: () => boolean, opening?: (tutorial: object) => void, listed?: (cards: object[] | null) => void }} [options]
 */
export function mountTutorials(doc, { request = fetched, session = null, go = link => globalThis.location.assign(link), hash = () => globalThis.location?.hash ?? '', plain = () => !globalThis.location?.search && !globalThis.location?.hash, opening = () => {}, listed: told = () => {} } = {}) {
  const section = doc.getElementById('learn-to-play');
  const cards = doc.getElementById('tutorial-cards');
  let reading = 0;

  async function load(account) {
    const ticket = ++reading;
    if (!section) return;
    if (!account) {
      section.hidden = true;
      told(null);
      return;
    }
    const answer = await series(request);
    if (ticket !== reading) return;
    // An address that asks for something (a rematch, the main menu's Learn to play) is answered first: the tutorial
    // waits for a plain visit.
    const first = plain() ? opensByItself(answer, session) : null;
    // Said before the page changes, so the window the player named themselves in can say where they are going.
    if (first) { opening(answer.tutorials.find(one => one.link === first) ?? {}); go(first); return true; }
    const listed = tutorialCards(answer);
    told(listed);
    cards.replaceChildren(...listed.map(card => {
      const root = made(doc, 'article', undefined, 'game-card practice tutorial-card motion-arrive');
      root.dataset.tutorial = card.id;
      // The painting is decoration: the title says the lesson. It holds its room by its width and height.
      if (card.picture) root.append(pictureElement(doc, card.picture, { page: 'table/home', class: 'tutorial-picture' }));
      const top = made(doc, 'div', undefined, 'game-card-top');
      top.append(made(doc, 'h3', card.title), made(doc, 'span', card.tag, card.done ? 'status-tag outcome won' : 'status-tag invited'));
      root.append(top);
      if (card.goal) root.append(made(doc, 'p', card.goal, 'practice-goal'));
      const ways = made(doc, 'div', undefined, 'card-ways');
      // Every card's action is an outline: the next lesson not done is filled once, in the home's featured space
      // (lobby-featured.js), when it is what the player came to do. Here it is marked, not filled.
      const open = made(doc, 'a', card.action, 'button-link');
      if (card.next) root.dataset.next = 'true';
      // Its place in the series, which the card no longer writes, is in its name for whoever cannot see the order.
      open.setAttribute?.('aria-label', `${card.action}: ${card.title}, ${card.place.toLowerCase()}`);
      open.href = card.link;
      ways.append(open);
      root.append(ways);
      return root;
    }));
    const progress = doc.getElementById('tutorial-progress');
    if (progress) progress.textContent = seriesProgress(listed).label;
    section.hidden = listed.length === 0;
    // The main menu's Learn to play names this section, which was not on the page when the address was read.
    if (listed.length && hash() === '#learn-to-play') section.scrollIntoView?.({ block: 'start' });
  }

  return { setAccount: account => load(account) };
}

/**
 * The settings' Show the first tutorial again: it opens tutorial 1 now, whatever the account did with it. Shown to
 * a signed-in player; a host with no tutorial says so when asked.
 * @param {{ request?: (path: string) => Promise<Response>, go?: (link: string) => void }} [options]
 */
export function mountShowAgain(doc, { request = fetched, go = link => globalThis.location.assign(link) } = {}) {
  const row = doc.getElementById('tutorial-again-row'), button = doc.getElementById('tutorial-again'), problem = doc.getElementById('tutorial-again-problem');
  button?.addEventListener('click', async () => {
    problem.hidden = true; button.disabled = true;
    const link = firstTutorial(await series(request));
    button.disabled = false;
    if (link) { go(link); return; }
    problem.textContent = 'No tutorial is available here right now.'; problem.hidden = false;
  });
  return { setAccount(account) { if (row) row.hidden = !account; } };
}
