// The home's featured space (docs/art/journeys/lobby.md, UX9 of docs/art/ux-craft-plan.md): one game, one lesson or
// a new battle, by what the player came to do, with its one filled action. A turn of the player's comes first, then a
// game of theirs waiting on someone else, then the next lesson, then a new battle. The host says all of it: whose
// turn it is (`/api/lobby/mine`), where the account stands with each lesson (`/api/tutorials`). Nothing is decided here.

import { gameKind, invitedState, tableState, waitingGames, versus } from './lobby.js';
import { sinceVisitLine, turnOf } from './players.js';
import { PAGE_ART } from './page-art.js';
import { pictureElement, sourceAttributes } from './picture.js';
import { writeArrival } from './arrival.js';

/**
 * The ground a game is on, by its round, on either player's turn (docs/art/scene-replacement-plan.md): the lobby's
 * duel in the mist for rounds 1 to 5 (not the battle's eclipse, on purpose), then the Great Rupture from round 6.
 * Each is declared for the home in docs/art/page-pictures.json.
 */
const DUEL = 'mist-duel';
const RUPTURE = 'great-rupture';
const GROUNDS = Object.freeze([[6, RUPTURE], [1, DUEL]]);
export function groundOf(round) {
  return GROUNDS.find(([from]) => (Number.isInteger(round) ? round : 1) >= from)?.[1] ?? DUEL;
}

/** A lesson stands on the battle's eclipse, the ground its first round is played on. */
const LESSON_GROUND = 'home-ground-eclipse';

/**
 * A lesson's creature, the first lesson's when the host names none. The first lesson's is one declaration with its
 * card in Learn to play, so a new account's home takes its file once (#985).
 */
const creatureOf = picture => picture || 'lesson-base';

/**
 * The width the ground covers (lobby.css): on a phone the whole screen's height, a 0.5625 portrait cut to it (475 px
 * on a 390 by 844 screen); 74% of the space in between; 950 px of the 1280 px column, at 1280 px and wider (#1070).
 */
export const GROUND_SIZES = '(min-width: 1280px) 950px, (min-width: 761px) 74vw, max(100vw, 56.25svh)';

// The lesson and the Great Rupture have paintings composed for a tall phone. A native picture changes source when the
// phone turns, without fetching both paintings on its first draw. A portrait tablet keeps the portrait: the
// landscape would be stretched to cover its tall box.
const LANDSCAPES = new Map([
  [LESSON_GROUND, 'battlefield-eclipse'],
  [RUPTURE, 'great-rupture-landscape'],
]);
export const LANDSCAPE_MEDIA = '(orientation: landscape)';

/** A landscape's width over its height, from its largest derived file (page-art.js), so a repaint carries over. */
function ratioOf(name) {
  const largest = PAGE_ART[name].sizes.at(-1);
  return largest.width / largest.height;
}

function featuredGround(doc, name) {
  const options = { page: 'table/home', class: 'featured-ground' };
  const wideName = LANDSCAPES.get(name);
  if (!wideName) return pictureElement(doc, name, { ...options, sizes: GROUND_SIZES });

  const ratio = ratioOf(wideName);
  const picture = doc.createElement('picture'), source = doc.createElement('source');
  source.setAttribute('media', LANDSCAPE_MEDIA);
  source.setAttribute('srcset', sourceAttributes(wideName, options).srcset);
  // object-fit: cover can make the painting wider than the box, especially the sky strip. The 320 px is the
  // featured space's least height on a short screen (lobby.css, `.home-page .featured`).
  const sizes = [
    `(max-height: 500px) and (min-width: 500px) max(min(72vw, 922px), ${Math.ceil(320 * ratio)}px)`,
    `(max-width: 760px) max(100vw, calc(max(440px, 100svh) * ${ratio}))`,
    `max(min(74vw, 950px), calc(clamp(450px, 77svh, 810px) * ${ratio}))`,
  ];
  source.setAttribute('sizes', sizes.join(', '));
  // The eclipse's portrait is cut taller than the screen, so the screen's width covers it; the Rupture's is the duel's
  // 9:16, which covers the screen's height first.
  picture.append(source, pictureElement(doc, name, { ...options, sizes: name === LESSON_GROUND ? 'max(100vw, 45svh, 198px)' : GROUND_SIZES }));
  return picture;
}

const held = entry => (entry?.seats ?? []).map(seat => seat.seat);
const capital = text => (text ? text[0].toUpperCase() + text.slice(1) : '');

/**
 * The lesson to continue: the one Learn to play marks as next (the first in the series' order not done), unless it
 * and every lesson after it were skipped.
 */
function nextLesson(lessons) {
  const left = (lessons ?? []).filter(one => !one.done);
  return left.some(one => !one.skipped) ? left.find(one => one.next) ?? left[0] : null;
}

/**
 * What the featured space shows. `previous` is the table it showed last: it stays while it is still a game at the
 * best rank on offer, so several turns at once do not swap under the player. `pinned` (its action has the focus, or a
 * finger on it) keeps it whatever arrives, while it is not over.
 */
export function featuredChoice({ mine = [], lessons = [], previous = null, pinned = false }) {
  const ongoing = mine.filter(entry => !entry.table.over);
  const turns = waitingGames(ongoing), others = ongoing.filter(entry => !turns.includes(entry));
  const kept = ongoing.find(entry => entry.table.id === previous);
  if (kept && pinned) return { kind: turns.includes(kept) ? 'turn' : 'waiting', entry: kept };
  // A person's turn, then a bot's (a bot always waits for its player, and would hide a person whose time runs), then
  // a game waiting on someone else: the game on show stays only within the first group that has one.
  const groups = [['turn', turns.filter(entry => gameKind(entry.table) === 'players')], ['turn', turns.filter(entry => gameKind(entry.table) === 'bots')], ['waiting', others]];
  for (const [kind, list] of groups) {
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

/** The eyebrow of a game: whose turn it is, or what the table waits for. */
function eyebrowOf(mine, turn, table) {
  if (mine) return 'Your turn';
  if (turn) return 'Their turn';
  return table.waiting ? 'Your table' : 'In play';
}

/**
 * The status of a game: what follows its eyebrow (the eyebrow says Your turn), whom a table not yet begun waits for
 * (anyone, or the friend its seat is kept for, as its card says), or that its round is being played.
 */
function statusOf(entry, turn) {
  if (turn) return capital(turn.text.startsWith('Your turn') ? turn.text.slice('Your turn'.length).replace(/^ · /, '') : turn.text);
  const invited = invitedState(entry.invited)?.tag;
  if (invited) return invited;
  return entry.table.waiting || entry.table.openSeats > 0 ? tableState(entry.table) : 'The round is being played';
}

const labelOf = (action, what) => `${action.replace(' →', '')}: ${what}`;

function gameView(choice) {
  const { table } = choice.entry, seats = held(choice.entry), mine = choice.kind === 'turn';
  const title = arrivalLine(table, seats), turn = turnOf(table, seats), status = statusOf(choice.entry, turn);
  const action = mine ? 'Play your turn →' : 'Open the game →';
  return { kind: choice.kind, id: table.id, announced: mine ? ['Your turn', status].filter(Boolean).join(' · ') : '', eyebrow: eyebrowOf(mine, turn, table), title, status,
    detail: capital(sinceVisitLine(choice.entry.recap) ?? ''), action, label: labelOf(action, `${title[0].toLowerCase()}${title.slice(1)}`),
    ground: groundOf(table.round), creature: null, link: null };
}

function lessonView({ lesson }, started) {
  const action = started || lesson.done ? 'Continue →' : 'Play →';
  return { kind: 'lesson', id: lesson.id, eyebrow: lesson.place, title: lesson.title, status: '', detail: lesson.goal ?? '', action,
    label: labelOf(action, `${lesson.title}, ${lesson.place.toLowerCase()}`), ground: LESSON_GROUND, creature: creatureOf(lesson.picture), link: lesson.link };
}

/** What the featured space says and shows for a choice. `started` is whether the account has done a lesson yet. */
export function featuredView(choice, { started = false } = {}) {
  if (choice.kind === 'turn' || choice.kind === 'waiting') return gameView(choice);
  if (choice.kind === 'lesson') return lessonView(choice, started);
  // A match started the quickest way, a bot answering at once; Multiplayer, just under it, opens every other way.
  return { kind: 'new', id: null, eyebrow: 'New battle · Beta', title: 'Choose your opponent', status: '', detail: '',
    action: 'Play a bot →', label: 'Play a bot', ground: DUEL, creature: null, link: null };
}

/**
 * Draws the featured space: `setGames(mine)` and `setLessons(cards)` as the host answers (null lessons for nobody
 * signed in), `setDisabled` when new tables cannot be made. It waits for both before it chooses, so it does not show
 * a new battle and then a lesson. Its elements are changed in place: the button the player may be about to press is
 * the same button, with the same focus, whatever it now says.
 * @param {{ enter: (entry: object) => void, openCreate: (asked?: object) => void, go: (link: string) => void, session?: Storage | null, ambience?: () => void, changed?: () => void, win?: { addEventListener: Function } }} actions
 */
export function mountFeatured(doc, { enter, openCreate, go, session = null, ambience = () => {}, changed = () => {}, win = globalThis }) {
  const el = id => doc.getElementById(id);
  const root = el('featured'), action = el('featured-action'), status = el('featured-status');
  root.dataset.featured = 'loading';
  root.setAttribute('aria-busy', 'true');
  // `lessons` is null where nobody can take one; `lessonsRead` says whether the host has answered yet.
  let games = null, lessons = null, lessonsRead = false, shown = null, drawnGround = null, drawnCreature = null, choice = null, opening = false, pressed = false, disabled = true, gesture = null;
  // The last round the host said for each table: one answer that could not read it in time keeps the title and the ground.
  const rounds = new Map();
  function remembered(list) {
    return list.map(entry => {
      const { id, round } = entry.table;
      if (Number.isInteger(round)) { rounds.set(id, round); return entry; }
      return rounds.has(id) && !entry.table.waiting ? { ...entry, table: { ...entry.table, round: rounds.get(id) } } : entry;
    });
  }
  const pinned = () => pressed || (doc.activeElement != null && doc.activeElement === action);

  function art(view) {
    if (view.ground === drawnGround && view.creature === drawnCreature) return;
    drawnGround = view.ground; drawnCreature = view.creature;
    // Decoration: the title says what the game or the lesson is. Each holds its room by its width and height.
    // The painting stays still. Only the mist and a lesson's creature move, under the motion preferences, paused behind a window.
    const pictures = [featuredGround(doc, view.ground)];
    if (view.ground === DUEL) {
      const mist = doc.createElement('span');
      mist.className = 'featured-mist motion-breath';
      pictures.push(mist);
    }
    if (view.creature) pictures.push(pictureElement(doc, view.creature, { page: 'table/home', class: 'featured-creature motion-breath', sizes: '150px' }));
    el('featured-art').replaceChildren(...pictures);
    ambience();
  }

  function draw() {
    // Under a finger the space holds still; what arrived is drawn once the press is handled or the finger leaves.
    if (games === null || !lessonsRead || opening || pressed) return;
    choice = featuredChoice({ mine: games, lessons: lessons ?? [], previous: shown?.id ?? null, pinned: pinned() });
    const view = featuredView(choice, { started: (lessons ?? []).some(one => one.done) });
    root.dataset.featured = view.kind;
    root.removeAttribute('aria-busy');
    el('featured-eyebrow').textContent = view.eyebrow;
    el('welcome').textContent = view.title;
    el('new-table').textContent = view.kind === 'new' ? 'Multiplayer' : '＋ New battle';
    el('featured-detail').textContent = view.detail;
    // Each number kept on one line with its unit.
    const shownStatus = view.status.replace(/(\d) /g, '$1\u00a0');
    if (status.textContent !== shownStatus) status.textContent = shownStatus;
    // A turn that arrives on the game on show: the eyebrow's words play the kit's beat for a value that changes, and
    // the region says the turn once (its eyebrow is not in it).
    const eyebrow = el('featured-eyebrow');
    if (shown?.id === view.id && shown.eyebrow !== view.eyebrow) { eyebrow.classList.remove('motion-changed'); eyebrow.getBoundingClientRect(); eyebrow.classList.add('motion-changed'); }
    // Said once a turn arrives while the home is open, on the game on show or on one that takes its place; not on
    // the first draw, which the heading and the button already say.
    const said = el('featured-said');
    const arrived = shown && view.kind === 'turn' && (shown.id !== view.id || shown.kind !== 'turn');
    if (said && (arrived || shown?.id !== view.id || shown?.announced !== view.announced)) said.textContent = arrived ? view.announced : '';
    status.dataset.empty = String(!view.status);
    action.hidden = false;
    action.textContent = view.action;
    action.setAttribute('aria-label', view.label);
    action.removeAttribute('aria-busy');
    // Play a bot makes a new table: not on a host that saves no games, as New battle is not.
    action.disabled = disabled && view.kind === 'new';
    art(view);
    const before = shown?.id ?? null;
    shown = view;
    // Told after the fact, so the lists under it can follow which table is on show.
    if (before !== view.id) changed();
  }

  /**
   * What a press opens: what the action showed when the finger came down (or when a key pressed it), if that is
   * still there. A lesson still listed, a game not over (as the host now describes it), a new battle the host
   * allows. Otherwise the press opens nothing, and the space shows what is there now.
   */
  function stillThere(pressedOn) {
    if (pressedOn?.kind === 'lesson') return (lessons ?? []).some(one => one.link === pressedOn.lesson.link) ? pressedOn : null;
    if (pressedOn?.kind === 'new') return disabled ? null : pressedOn;
    const entry = (games ?? []).find(one => one.table.id === pressedOn?.entry.table.id && !one.table.over);
    return entry ? { ...pressedOn, entry } : null;
  }
  action.addEventListener('click', () => {
    const pressedOn = gesture ?? choice;
    gesture = null; pressed = false;
    if (!pressedOn || opening) return;
    const target = stillThere(pressedOn);
    if (!target) { draw(); return; }
    if (target.kind === 'lesson') { go(target.lesson.link); draw(); return; }
    if (target.kind === 'new') { openCreate({ opponent: 'bot' }); draw(); return; }
    // The press is answered at once, before the battle's page arrives; the battle's first frame names the game.
    const title = arrivalLine(target.entry.table, held(target.entry));
    opening = true;
    action.textContent = 'Opening…';
    action.setAttribute('aria-label', `Opening: ${title[0].toLowerCase()}${title.slice(1)}`);
    action.setAttribute('aria-busy', 'true');
    writeArrival(session, target.entry.table.id, title, (target.entry.seats ?? []).map(seat => seat.token));
    try { enter(target.entry); } catch (error) { giveBack(); throw error; }
  });
  // The action is given back when the page could not leave, or comes back from the back-forward cache.
  function giveBack() { opening = false; draw(); }
  win.addEventListener?.('pageshow', event => { if (event.persisted) giveBack(); });
  // A finger on the action keeps what it pressed from pointerdown until the click; one that slides off lets it follow.
  action.addEventListener('pointerdown', () => { pressed = true; gesture = choice; });
  for (const kind of ['pointercancel', 'pointerleave']) action.addEventListener(kind, () => { pressed = false; gesture = null; draw(); });
  action.addEventListener('blur', () => draw());

  return {
    /**
     * Someone can play here: the duel is asked for now, before the lists are read, since a returning player's states
     * stand on it (the largest paint of the home). A guest's door never loads it. A later round's ground replaces it,
     * and so does a lesson's eclipse: an account with a lesson still to play pays for both, a cost accepted so that
     * the returning player's first paint does not wait for the lists.
     */
    playable() { if (drawnGround === null && root.dataset.featured === 'loading') art({ ground: groundOf(1), creature: null }); },
    /** The table on show, or null: the lobby leaves out of its Your turn band a lone turn already featured. */
    shown: () => shown?.id ?? null,
    /** The player's games, as the host lists them. */
    setGames(next) { games = remembered(next ?? []); draw(); },
    /** Whether new tables can be made here (`recording`): Play a bot is disabled when they cannot. */
    setDisabled(next) { disabled = Boolean(next); draw(); },
    /** The lessons as Learn to play draws them (lobby-tutorials.js), or null where nobody can take one. */
    setLessons(next) { lessons = next ?? null; lessonsRead = true; draw(); },
    /** Who is signed in changed: what was shown is not theirs. */
    reset() { games = null; lessons = null; lessonsRead = false; shown = null; opening = false; root.dataset.featured = 'loading'; root.setAttribute('aria-busy', 'true'); },
  };
}
