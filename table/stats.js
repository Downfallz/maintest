import { onLobbyChange } from './changes.js';

// A player's own statistics (ADR 0168): how their ranked 3v3 games went (ADR 0171), and the mutations and spells
// they played in them. The host computes everything from the results it keeps for the player; this page only draws it.

/** Won–lost–drawn, as a ladder writes it. */
export function tallyText({ won, lost, drawn }) {
  return `${won}–${lost}–${drawn}`;
}

/** A share of games as a whole percent, or a dash when there is nothing to share. */
export function percent(part, whole) {
  return whole ? `${Math.round((100 * part) / whole)}%` : '–';
}

/** The current streak in words: "3 wins in a row", "1 loss", or nothing before a first game. */
export function streakText(streak) {
  if (!streak) return 'No game yet';
  const word = { won: ['win', 'wins'], lost: ['loss', 'losses'], drawn: ['draw', 'draws'] }[streak.outcome] ?? ['game', 'games'];
  return streak.length === 1 ? `1 ${word[0]}` : `${streak.length} ${word[1]} in a row`;
}

/** What the mutations and spells say about which games they count, when not all of them do. */
export function teamsNote(stats) {
  const games = stats.ranked.games;
  if (!games) return '';
  if (stats.withTeams === games) return `From your ${games} game${games === 1 ? '' : 's'}`;
  return `From ${stats.withTeams} of your ${games} games: older games did not keep their teams`;
}

function node(doc, tag, text, className) {
  const element = doc.createElement(tag);
  if (text !== undefined && text !== null) element.textContent = text;
  if (className) element.className = className;
  return element;
}

function rows(doc, items, cells, empty, width) {
  if (!items.length) {
    const row = node(doc, 'tr');
    const cell = node(doc, 'td', empty, 'empty');
    cell.colSpan = width;
    row.append(cell);
    return [row];
  }
  return items.map(item => {
    const row = node(doc, 'tr');
    const [first, ...rest] = cells(item);
    const name = node(doc, 'td', first, 'player');
    name.title = item.id;
    row.append(name, ...rest.map(text => node(doc, 'td', text, 'number')));
    return row;
  });
}

export function drawStats(doc, stats) {
  const el = id => doc.getElementById(id);
  const card = (label, tally) => {
    const element = node(doc, 'div', null, 'stats-card');
    element.append(node(doc, 'small', label), node(doc, 'strong', tallyText(tally)), node(doc, 'span', `${tally.games} game${tally.games === 1 ? '' : 's'} · ${percent(tally.won, tally.games)} won`));
    return element;
  };
  el('record-cards').replaceChildren(card('Ranked 3v3', stats.ranked));

  const glance = [
    ['Current streak', streakText(stats.streak)],
    ['Best win streak', String(stats.bestWinStreak)],
    ['Average length', stats.averageRounds === null ? '–' : `${stats.averageRounds} rounds`],
  ];
  el('glance').replaceChildren(...glance.flatMap(([term, value]) => [node(doc, 'dt', term), node(doc, 'dd', value)]));

  el('teams-note').textContent = teamsNote(stats);
  el('mutation-rows').replaceChildren(...rows(doc, stats.mutations, mutation => [mutation.name, String(mutation.bought), percent(mutation.won, mutation.bought)], 'No mutation bought yet.', 3));
  el('spell-rows').replaceChildren(...rows(doc, stats.spells, spell => [spell.name, String(spell.casts), String(spell.games), percent(spell.won, spell.games)], 'No spell cast yet.', 4));

  const games = stats.ranked.games;
  const counted = `${games} ranked game${games === 1 ? '' : 's'} · updates as your games end`;
  el('stats-status').textContent = games ? counted : 'No ranked game yet. Play a ranked 3v3 game against another player: it counts here as soon as it ends.';
  el('stats-body').hidden = false;
}

async function load(doc) {
  const problem = doc.getElementById('stats-problem');
  try {
    const response = await fetch('/api/stats', { credentials: 'same-origin' });
    if (response.status === 404) {
      doc.getElementById('stats-status').textContent = 'This host keeps no statistics: players do not sign in here.';
      return;
    }
    if (response.status === 401) {
      doc.getElementById('stats-status').textContent = 'Sign in on the home page to see your statistics.';
      doc.getElementById('stats-body').hidden = true;
      return;
    }
    if (!response.ok) throw new Error(`The host answered ${response.status}. Try again.`);
    problem.hidden = true;
    drawStats(doc, await response.json());
  } catch (error) {
    problem.textContent = error.message || 'Could not read your statistics. Try again.';
    problem.hidden = false;
  }
}

if (typeof document !== 'undefined') {
  void load(document);
  // Read again when the player's own lists change (their lobby stream, ADR 0164), which a game of theirs ending
  // does, and when the stream opens -- the page loaded, back in view -- rather than on a clock.
  onLobbyChange(event => { if (!document.hidden && (event.event === 'ready' || String(event.data).startsWith('account'))) void load(document); });
}
