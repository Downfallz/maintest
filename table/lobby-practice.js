// The lobby's Practice section (docs/tabletop/scenarios-plan.md, S6, ADR 0179): the match scenarios the operator
// listed, for a signed-in player to open as practice. Opening one creates nothing; the battle view starts the
// practice from the scenario's cut. A host that keeps no scenarios, or a player signed out, shows no section.

/** Each listed scenario as a card, from `GET /api/scenarios`. */
export function practiceCards(answer) {
  return (answer?.scenarios ?? []).filter(one => one?.link).map(one => ({
    id: one.id,
    title: one.title || `Round ${one.round}`,
    goal: one.goal ?? '',
    facts: [one.format, `from round ${one.round}`, one.seat === 'player2' ? 'you play player 2' : 'you play player 1'].filter(Boolean).join(' · '),
    link: one.link,
  }));
}

function made(doc, tag, text, className) {
  const one = doc.createElement(tag);
  if (text !== undefined) one.textContent = text;
  if (className) one.className = className;
  return one;
}

/**
 * Draws the section for whoever is signed in: `setAccount(account)` reads the list again for them, and hides the
 * section for nobody. `request(path)` resolves to a fetch response.
 */
export function mountPracticeList(doc, request = path => globalThis.fetch(path, { credentials: 'same-origin' })) {
  const section = doc.getElementById('practice-scenarios');
  const cards = doc.getElementById('practice-cards');
  let reading = 0;

  async function load(account) {
    const ticket = ++reading;
    if (!section || !account) { if (section) section.hidden = true; return; }
    let listed = [];
    try {
      const response = await request('/api/scenarios');
      listed = response.ok ? practiceCards(await response.json()) : [];
    } catch { listed = []; }
    if (ticket !== reading) return;
    cards.replaceChildren(...listed.map(card => {
      const root = made(doc, 'article', undefined, 'game-card practice');
      root.dataset.scenario = card.id;
      const top = made(doc, 'div', undefined, 'game-card-top');
      top.append(made(doc, 'h3', card.title), made(doc, 'span', 'Practice', 'status-tag practice'));
      root.append(top, made(doc, 'p', card.facts, 'game-turn'));
      if (card.goal) root.append(made(doc, 'p', card.goal, 'practice-goal'));
      const ways = made(doc, 'div', undefined, 'card-ways');
      const open = made(doc, 'a', 'Practise →', 'primary');
      open.href = card.link;
      ways.append(open);
      root.append(ways);
      return root;
    }));
    doc.getElementById('practice-count').textContent = listed.length ? String(listed.length) : '';
    section.hidden = listed.length === 0;
  }

  return { setAccount: account => void load(account) };
}
