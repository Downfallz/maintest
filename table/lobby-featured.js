// The home's featured space (docs/art/journeys/lobby.md, UX9 of docs/art/ux-craft-plan.md): one game, one lesson or
// a new battle, by what the player came to do, with its one filled action. A turn of the player's comes first, then a
// game of theirs waiting on someone else, then the next lesson, then a new battle. The host says all of it: whose
// turn it is (`/api/lobby/mine`), where the account stands with each lesson (`/api/tutorials`). Nothing is decided here.

import { gameKind, invitedState, tableState, waitingGames, versus } from './lobby.js';
import { sinceVisitLine, turnOf } from './players.js';
import { pictureElement } from './picture.js';
import { writeArrival } from './arrival.js';

/**
 * The ground a game is on, by its round, as the battle lays its plates (battle/src/battle-horizon.ts): the eclipse,
 * the breach from round 6, the shattered sky from round 10. Each is declared for the home in docs/art/page-pictures.json.
 */
const GROUNDS = Object.freeze([[10, 'home-ground-sky'], [6, 'home-ground-breach'], [1, 'home-ground-eclipse']]);
export function groundOf(round) {
  return GROUNDS.find(([from]) => (Number.isInteger(round) ? round : 1) >= from)?.[1] ?? 'home-ground-eclipse';
}

/**
 * The ancestral creature: a new battle's, and the first lesson's. The same painting as the first lesson's card, but
 * asked for with the featured space's ground, before Learn to play's lazy ones.
 */
const ANCESTRAL = 'home-ancestral';
const creatureOf = picture => !picture || picture === 'lesson-base' ? ANCESTRAL : picture;

const held = entry => (entry?.seats ?? []).map(seat => seat.seat);

/** The lesson to continue: the next one not done, unless it and every lesson after it were skipped. */
function nextLesson(lessons) {
  const left = (lessons ?? []).filter(one => !one.done);
  return left.some(one => !one.skipped) ? left[0] : null;
}

/**
 * What the featured space shows. `previous` is the table it showed last: it stays while it is still a game at the
 * best rank on offer, so several turns at once do not swap under the player. `pinned` (its action has the focus, or a
 * finger on it) keeps it whatever arrives, while it is not over.
 */
export function featuredChoice({ mine = [], lessons = [], previous = null, pinned = false }) {
  const ongoing = mine.filter(entry => !entry.table.over);
  // A person's turn before a bot's: a bot always waits for its player, and would hide a person whose time runs.
  const waiting = waitingGames(ongoing), turns = [...waiting.filter(entry => gameKind(entry.table) === 'players'), ...waiting.filter(entry => gameKind(entry.table) === 'bots')];
  const others = ongoing.filter(entry => !turns.includes(entry));
  const kept = ongoing.find(entry => entry.table.id === previous);
  if (kept && pinned) return { kind: turns.includes(kept) ? 'turn' : 'waiting', entry: kept };
  for (const [kind, list] of [['turn', turns], ['waiting', others]]) {
    if (list.length) return { kind, entry: list.find(entry => entry.table.id === previous) ?? list[0] };
  }
  const lesson = nextLesson(lessons);
  return lesson ? { kind: 'lesson', lesson } : { kind: 'new' };
}

/** Whom a game is against, as the featured space names them: a person by name, a bot as one, an open seat as none. */
function against(table, seats) {
  const other = versus(table).find(side => !seats.includes(side.seat));
  if (!other || other.kind === 'open') return null;
  return other.kind === 'bot' ? `${other.name} (bot)` : other.name || 'another player';
}

/** The line a game is named by, in the featured space and in the battle's first frame: its round and whom it is against. */
export function arrivalLine(table, seats) {
  const who = against(table, seats);
  if (!who) return `Your ${table.format} table`;
  return Number.isInteger(table.round) && !table.waiting ? `Round ${table.round} against ${who}` : `${table.format} battle against ${who}`;
}

/** What the featured space says and shows for a choice. `started` is whether the account has done a lesson yet. */
export function featuredView(choice, { started = false } = {}) {
  if (choice.kind === 'turn' || choice.kind === 'waiting') {
    const { table } = choice.entry, seats = held(choice.entry), mine = choice.kind === 'turn';
    const title = arrivalLine(table, seats);
    const turn = turnOf(table, seats);
    // A table not yet begun says whom it waits for, as its card does: anyone, or the friend its seat is kept for.
    const status = turn?.text ?? invitedState(choice.entry.invited)?.tag ?? (table.waiting || table.openSeats > 0 ? tableState(table) : 'The round is being played');
    const action = mine ? 'Play your turn →' : 'Open the game →';
    const since = sinceVisitLine(choice.entry.recap);
    return { kind: choice.kind, id: table.id, eyebrow: mine ? 'Your turn' : turn ? 'Their turn' : table.waiting ? 'Your table' : 'In play', title, status,
      detail: since ? since[0].toUpperCase() + since.slice(1) : '', action, label: `${action.replace(' →', '')}: ${title[0].toLowerCase()}${title.slice(1)}`,
      ground: groundOf(table.round), creature: null, link: null };
  }
  if (choice.kind === 'lesson') {
    const { lesson } = choice, action = started || lesson.done ? 'Continue →' : 'Play →';
    return { kind: 'lesson', id: lesson.id, eyebrow: lesson.place, title: lesson.title, status: '', detail: lesson.goal ?? '', action,
      label: `${action.replace(' →', '')}: ${lesson.title}, ${lesson.place.toLowerCase()}`, ground: groundOf(1), creature: creatureOf(lesson.picture), link: lesson.link };
  }
  // A match started the quickest way, a bot answering at once; New battle, just under it, opens every other way.
  return { kind: 'new', id: null, eyebrow: 'A new battle', title: 'Your next battle.', status: '', detail: 'A bot answers at once. Or challenge a friend, or take an open seat.',
    action: 'Play a bot →', label: 'Play a bot', ground: groundOf(1), creature: ANCESTRAL, link: null };
}

/**
 * Draws the featured space: `setGames(mine)` and `setLessons(cards)` as the host answers (null lessons for nobody
 * signed in), `setDisabled` when new tables cannot be made. It waits for both before it chooses, so it does not show
 * a new battle and then a lesson. Its elements are changed in place: the button the player may be about to press is
 * the same button, with the same focus, whatever it now says.
 * @param {{ enter: (entry: object) => void, openCreate: (asked?: object) => void, go: (link: string) => void, session?: Storage | null, ambience?: () => void }} actions
 */
export function mountFeatured(doc, { enter, openCreate, go, session = null, ambience = () => {} }) {
  const el = id => doc.getElementById(id);
  const root = el('featured'), action = el('featured-action'), create = el('new-table'), status = el('featured-status');
  root.dataset.featured = 'loading';
  root.setAttribute('aria-busy', 'true');
  let games = null, lessons = undefined, shown = null, drawnGround = null, drawnCreature = null, choice = null, opening = false, pressed = false;
  const pinned = () => pressed || (doc.activeElement != null && doc.activeElement === action);

  function art(view) {
    if (view.ground === drawnGround && view.creature === drawnCreature) return;
    drawnGround = view.ground; drawnCreature = view.creature;
    // Decoration: the title says what the game or the lesson is. Each holds its room by its width and height.
    // The one quiet movement is the creature's breath; a ground alone stays still, since a landscape that swells reads as a zoom.
    const pictures = [pictureElement(doc, view.ground, { page: 'table/home', class: 'featured-ground', sizes: '(min-width: 761px) 640px, 100vw' })];
    if (view.creature) pictures.push(pictureElement(doc, view.creature, { page: 'table/home', class: 'featured-creature motion-breath', sizes: '150px' }));
    el('featured-art').replaceChildren(...pictures);
    ambience();
  }

  function draw() {
    if (games === null || lessons === undefined || opening) return;
    choice = featuredChoice({ mine: games, lessons: lessons ?? [], previous: shown?.id ?? null, pinned: pinned() });
    const view = featuredView(choice, { started: (lessons ?? []).some(one => one.done) });
    root.dataset.featured = view.kind;
    root.removeAttribute('aria-busy');
    el('featured-eyebrow').textContent = view.eyebrow;
    el('welcome').textContent = view.title;
    el('featured-detail').textContent = view.detail;
    // The status of the same game that changed (its turn arrived) plays the kit's beat for a value that changes.
    if (status.textContent !== view.status) {
      status.textContent = view.status;
      if (shown?.id === view.id && view.status) { status.classList.remove('motion-changed'); status.getBoundingClientRect(); status.classList.add('motion-changed'); }
    }
    // Every line keeps its room in every state (lobby.css): an empty one is unseen, never taken out.
    status.dataset.empty = String(!view.status);
    action.hidden = false;
    action.textContent = view.action;
    action.setAttribute('aria-label', view.label);
    action.removeAttribute('aria-busy');
    art(view);
    shown = view;
  }

  action.addEventListener('click', () => {
    if (!choice || opening) return;
    if (choice.kind === 'lesson') { go(choice.lesson.link); return; }
    if (choice.kind === 'new') { openCreate({ opponent: 'bot' }); return; }
    // The press is answered at once, before the battle's page arrives; the battle's first frame names the game.
    opening = true;
    action.textContent = 'Opening…';
    action.setAttribute('aria-busy', 'true');
    writeArrival(session, choice.entry.table.id, shown.title);
    enter(choice.entry);
  });
  for (const [kind, down] of [['pointerdown', true], ['pointerup', false], ['pointercancel', false], ['pointerleave', false]]) {
    action.addEventListener(kind, () => { pressed = down; if (!down) draw(); });
  }
  action.addEventListener('blur', () => draw());

  return {
    /** The player's games, as the host lists them. */
    setGames(next) { games = next ?? []; draw(); },
    /** The lessons as Learn to play draws them (lobby-tutorials.js), or null where nobody can take one. */
    setLessons(next) { lessons = next ?? null; draw(); },
    /** Who is signed in changed: what was shown is not theirs. */
    reset() { games = null; lessons = undefined; shown = null; opening = false; root.dataset.featured = 'loading'; root.setAttribute('aria-busy', 'true'); },
  };
}
