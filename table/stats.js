import { accountMoved, onLobbyChange } from './changes.js';
import { numberChanged } from './motion.js';
import { pictureElement, wearPageHead } from './picture.js';
import { focusableScrolls, recordBar, recordParts } from './leaderboard.js';

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

/** An average length in rounds: one round, or rounds. */
export function roundsText(rounds) {
  if (rounds === null || rounds === undefined) return '–';
  return rounds === 1 ? '1 round' : `${rounds} rounds`;
}

/**
 * What a player leans on: the host's first row of mutations and of spells, which it orders by count, so the page
 * chooses nothing; and the painting the mutation draws (`artwork`, ADR 0198's `mutation-<artwork>`), or none.
 */
export function leanedOn(stats) {
  const mutation = stats.mutations?.[0] ?? null;
  return { mutation, picture: mutation?.artwork ? `mutation-${mutation.artwork}` : null, spell: stats.spells?.[0] ?? null };
}

/** The kit's picture of a spell's portrait (`portrait`, docs/art/spell-vfx.md#portraits), or none. */
export function spellPicture(spell) {
  return spell?.portrait ? `spell-${spell.portrait}` : null;
}

// A spell's portrait at the size it is drawn, decoration beside its name; a picture this build did not derive is
// left out, and the name says it.
function portraitOf(doc, spell, drawn, className) {
  const name = spellPicture(spell);
  if (!name) return null;
  try { return pictureElement(doc, name, { page: 'table/stats', class: className, sizes: `${drawn}px` }); } catch { return null; }
}

const times = count => (count === 1 ? 'once' : `${count} times`);

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

function rows(doc, items, cells, empty, width, picture = () => null) {
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
    const drawn = picture(item);
    if (drawn) name.prepend(drawn);
    row.append(name, ...rest.map(text => node(doc, 'td', text, 'number')));
    return row;
  });
}

let lastTally = null;

export function drawStats(doc, stats) {
  const el = id => doc.getElementById(id);
  const games = stats.ranked.games;
  // A player with no ranked game sees one empty state and the game that fills it, not four empty containers.
  el('stats-empty').hidden = games > 0;
  el('stats-body').hidden = games === 0;
  el('stats-status').textContent = '';
  // A loaded empty record is a tally too: the first ranked result arriving live is then announced like the next ones.
  if (!games) { lastTally = tallyText(stats.ranked); return; }

  const tally = tallyText(stats.ranked);
  const card = node(doc, 'div', null, 'stats-card record-card');
  const parts = recordParts({ wins: stats.ranked.won, losses: stats.ranked.lost, draws: stats.ranked.drawn });
  card.append(node(doc, 'small', 'Ranked 3v3'), node(doc, 'strong', tally, 'motion-number'));
  if (parts) card.append(recordBar(doc, parts));
  card.append(node(doc, 'span', `${games} game${games === 1 ? '' : 's'} · ${percent(stats.ranked.won, games)} won`));
  el('record-cards').replaceChildren(card);
  // A record that changes while the page is open plays the kit's value that changes, and is told once; never on load.
  if (lastTally !== null && lastTally !== tally) {
    numberChanged(card.querySelector('.motion-number'));
    el('stats-status').textContent = `Your record is now ${tally}.`;
  }
  lastTally = tally;

  const leaned = leanedOn(stats);
  el('leaned-mutation').hidden = !leaned.mutation;
  if (leaned.mutation) {
    const body = [];
    if (leaned.picture) {
      try { body.push(pictureElement(doc, leaned.picture, { page: 'table/stats', class: 'leaned-picture' })); } catch { /* a painting this build did not derive: the name says it */ }
    }
    const words = node(doc, 'p', null, 'leaned-words');
    words.append(node(doc, 'strong', leaned.mutation.name), node(doc, 'span', `Acquired ${times(leaned.mutation.bought)}`), node(doc, 'span', `${percent(leaned.mutation.won, leaned.mutation.bought)} won`));
    body.push(words);
    el('leaned-mutation-body').replaceChildren(...body);
  }
  el('leaned-spell').hidden = !leaned.spell;
  if (leaned.spell) {
    const words = node(doc, 'p', null, 'leaned-words leaned-spell');
    words.append(node(doc, 'strong', leaned.spell.name), node(doc, 'span', `Cast ${times(leaned.spell.casts)} in ${leaned.spell.games} game${leaned.spell.games === 1 ? '' : 's'}`));
    el('leaned-spell-body').replaceChildren(...[portraitOf(doc, leaned.spell, 40, 'leaned-portrait'), words].filter(Boolean));
  }

  const glance = [
    ['Current streak', streakText(stats.streak)],
    ['Best win streak', String(stats.bestWinStreak)],
    ['Average length', roundsText(stats.averageRounds)],
  ];
  el('glance').replaceChildren(...glance.flatMap(([term, value]) => [node(doc, 'dt', term), node(doc, 'dd', value)]));

  el('teams-note').textContent = teamsNote(stats);
  el('mutation-rows').replaceChildren(...rows(doc, stats.mutations, mutation => [mutation.name, String(mutation.bought), percent(mutation.won, mutation.bought)], 'No mutation acquired yet.', 3));
  el('spell-rows').replaceChildren(...rows(doc, stats.spells, spell => [spell.name, String(spell.casts), String(spell.games), percent(spell.won, spell.games)], 'No spell cast yet.', 4,
    spell => portraitOf(doc, spell, 24, 'row-portrait')));
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
      doc.getElementById('stats-empty').hidden = true;
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
  focusableScrolls(document);
  // The Ironhide's scar: what the games left on the player (docs/art/journeys/pages.md), cut below the plates' middle.
  wearPageHead(document, 'combat-marks', { page: 'table/stats', focus: '50% 35%' });
  // Read again when the player's own lists change (their lobby stream, ADR 0164), which a game of theirs ending
  // does, and when the stream opens -- the page loaded, back in view -- rather than on a clock.
  onLobbyChange(event => { if (!document.hidden && accountMoved(event)) void load(document); });
  await load(document);
}
