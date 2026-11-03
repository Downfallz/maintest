// The ladder (ADR 0124): who ranks where among the players with an account, and, for whoever is signed in,
// their own place and their record against each bot. The host computes everything; this page only draws it.

/** A record as a ladder writes it: wins, losses and draws. */
export function recordText({ wins, losses, draws }) {
  return `${wins}–${losses}–${draws}`;
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

function node(doc, tag, text, className) {
  const element = doc.createElement(tag);
  if (text !== undefined && text !== null) element.textContent = text;
  if (className) element.className = className;
  return element;
}

export function drawLadder(doc, ladder) {
  const el = id => doc.getElementById(id);
  const rows = ladder.players.map(player => {
    const row = node(doc, 'tr');
    row.className = [player.provisional ? 'provisional' : '', player.you ? 'you' : '', player.rank <= 3 && !player.provisional ? 'top' : ''].filter(Boolean).join(' ');
    const name = node(doc, 'td', player.username, 'player');
    if (player.provisional) name.append(node(doc, 'span', 'provisional', 'tag'));
    const rating = node(doc, 'td', String(player.rating), 'number');
    rating.append(node(doc, 'span', `±${player.deviation}`, 'deviation'));
    row.append(node(doc, 'td', String(player.rank), 'rank'), name, rating, node(doc, 'td', recordText(player), 'number'), node(doc, 'td', String(player.games), 'number games'));
    return row;
  });
  el('ladder-rows').replaceChildren(...rows);
  el('ladder-empty').hidden = rows.length > 0;
  el('players-count').textContent = ladder.players.length;
  el('provisional-above').textContent = ladder.provisionalAbove;

  const summary = youSummary(ladder.you);
  const you = [];
  if (summary.kind === 'ranked') you.push(node(doc, 'p', ratingText(ladder.you.standing), 'you-rating'));
  you.push(node(doc, 'p', summary.text, 'you-line'));
  if (ladder.you?.bots?.length) {
    const bots = node(doc, 'ul', null, 'bots');
    bots.setAttribute('aria-label', 'Against the bots');
    for (const bot of ladder.you.bots) {
      const item = node(doc, 'li');
      item.append(node(doc, 'span', bot.bot), node(doc, 'span', recordText(bot)));
      bots.append(item);
    }
    you.push(node(doc, 'p', 'Against the bots (not ranked)', 'eyebrow'), bots);
  }
  el('you').replaceChildren(...you);
  el('ladder-status').textContent = `${ladder.players.length} player${ladder.players.length === 1 ? '' : 's'} on the ladder · updates every half minute`;
}

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
    drawLadder(doc, await response.json());
  } catch (error) {
    problem.textContent = error.message || 'Could not read the ladder. Try again.';
    problem.hidden = false;
  }
}

if (typeof document !== 'undefined') {
  void load(document);
  globalThis.setInterval(() => { if (!document.hidden) void load(document); }, 30000);
}
