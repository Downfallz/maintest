// The tutorials outside a game (ADR 0187, docs/tabletop/onboarding-plan.md, O4): the lobby's Learn to play section,
// the first tutorial opening by itself for an account it is new to, and the settings' way back to it. The host says
// all of it in `GET /api/tutorials`: the series, where the account stands with each (new, done, skipped), and the
// one that opens by itself. A host that ships no tutorial, or a player signed out, shows no section.

const OFFERED_KEY = 'downfall.arena.tutorial-offered.v1';

/** Each tutorial as a card: its place in the series, its title and goal, whether it is done, and its link. */
export function tutorialCards(answer) {
  return (answer?.tutorials ?? []).filter(one => one?.link).map(one => {
    const done = one.state === 'done';
    return {
      id: one.id,
      title: one.title || `Tutorial ${one.order}`,
      place: `Tutorial ${one.order} of ${Math.max(one.of ?? 0, one.order ?? 0)}`,
      goal: one.goal ?? '',
      done,
      tag: done ? 'Done' : 'New',
      action: done ? 'Play again →' : 'Play →',
      link: one.link,
    };
  });
}

/** The first tutorial of the series, the one the settings open again; none where the host ships none. */
export function firstTutorial(answer) {
  return (answer?.tutorials ?? []).find(one => one?.order === 1 && one.link)?.link ?? null;
}

/**
 * The tutorial to open now, by itself: the one the host says opens (`opens`), once a browser session. The host
 * stops saying it once the account finished, skipped or left it; the session's mark keeps a tutorial that could
 * not open, or a tab closed on it, from taking the home page away on every visit.
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
 * @param {{ request?: (path: string) => Promise<Response>, session?: Storage | null, go?: (link: string) => void, hash?: () => string }} [options]
 */
export function mountTutorials(doc, { request = fetched, session = null, go = link => globalThis.location.assign(link), hash = () => globalThis.location?.hash ?? '' } = {}) {
  const section = doc.getElementById('learn-to-play');
  const cards = doc.getElementById('tutorial-cards');
  let reading = 0;

  async function load(account) {
    const ticket = ++reading;
    if (!section || !account) { if (section) section.hidden = true; return; }
    const answer = await series(request);
    if (ticket !== reading) return;
    const first = opensByItself(answer, session);
    if (first) { go(first); return; }
    const listed = tutorialCards(answer);
    cards.replaceChildren(...listed.map(card => {
      const root = made(doc, 'article', undefined, 'game-card practice');
      root.dataset.tutorial = card.id;
      const top = made(doc, 'div', undefined, 'game-card-top');
      top.append(made(doc, 'h3', card.title), made(doc, 'span', card.tag, card.done ? 'status-tag outcome won' : 'status-tag invited'));
      root.append(top, made(doc, 'p', card.place, 'game-turn'));
      if (card.goal) root.append(made(doc, 'p', card.goal, 'practice-goal'));
      const ways = made(doc, 'div', undefined, 'card-ways');
      const open = made(doc, 'a', card.action, 'primary');
      open.href = card.link;
      ways.append(open);
      root.append(ways);
      return root;
    }));
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
