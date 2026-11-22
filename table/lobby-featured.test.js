import { test } from 'node:test';
import assert from 'node:assert/strict';
import { arrivalLine, featuredChoice, featuredView, groundOf, mountFeatured } from './lobby-featured.js';
import { ARRIVAL_KEY, arrivalFor, readArrival, writeArrival } from './arrival.js';

const me = { seat: 'player1', kind: 'person', name: 'Walker' };
const rook = { seat: 'player2', kind: 'person', name: 'Rook' };
const bot = { seat: 'player2', kind: 'bot', name: 'Hard' };
function game(id, { round = 1, mine = true, against = rook, waiting = false, over = false, answerBy = null, recap = null } = {}) {
  return { table: { id, format: '3v3', round, over, waiting, openSeats: against.kind === 'open' ? 1 : 0, answerBy,
    seats: [{ ...me, turn: mine }, { ...against, turn: !mine && against.kind !== 'bot' }] }, seats: [{ seat: 'player1', token: `${id}-token` }], recap };
}
const lesson = (order, { done = false, skipped = false } = {}) => ({ id: `t${order}`, title: `Lesson ${order}`, place: `Tutorial ${order} of 4`, goal: `Goal ${order}.`,
  done, skipped, next: false, tag: done ? 'Done' : skipped ? 'Skipped' : 'New', action: done ? 'Play again' : 'Play', link: `/battle?scenario=t${order}`, picture: ['lesson-base', 'lesson-brute', 'lesson-ironhide', 'lesson-colossus'][order - 1] });
const series = (...states) => states.map((state, index) => lesson(index + 1, { done: state === 'done', skipped: state === 'skipped' }));

test('the ground follows the round, as the battle lays its plates: the duel, then the breach from round 6, the sky from round 10', () => {
  assert.deepEqual([1, 5, 6, 9, 10, 14, undefined].map(groundOf), ['mist-duel', 'mist-duel', 'home-ground-breach', 'home-ground-breach', 'home-ground-sky', 'home-ground-sky', 'mist-duel']);
});

test('a game whose turn is the player\'s is featured first, a bot\'s included, before one waiting on someone else and before a lesson', () => {
  const games = [game('theirs', { mine: false }), game('bot', { against: bot })];

  assert.deepEqual(featuredChoice({ mine: games, lessons: series('new') }), { kind: 'turn', entry: games[1] });
});

test('with no turn of the player\'s, a game waiting on someone else is featured before a lesson', () => {
  const games = [game('theirs', { mine: false })];

  assert.deepEqual(featuredChoice({ mine: games, lessons: series('new') }), { kind: 'waiting', entry: games[0] });
});

test('a finished game is never featured', () => {
  assert.deepEqual(featuredChoice({ mine: [game('done', { over: true })], lessons: [] }), { kind: 'new' });
});

test('a new account is shown its first lesson, even one it skipped from its brief, and a returning learner the next one not done', () => {
  assert.equal(featuredChoice({ mine: [], lessons: series('skipped', 'new', 'new', 'new') }).lesson.id, 't1');
  assert.equal(featuredChoice({ mine: [], lessons: series('done', 'done', 'new', 'new') }).lesson.id, 't3');
});

test('a player who skipped every lesson left, or did them all, is offered a new battle', () => {
  assert.deepEqual(featuredChoice({ mine: [], lessons: series('done', 'skipped', 'skipped', 'skipped') }), { kind: 'new' });
  assert.deepEqual(featuredChoice({ mine: [], lessons: series('done', 'done', 'done', 'done') }), { kind: 'new' });
  assert.deepEqual(featuredChoice({ mine: [], lessons: [] }), { kind: 'new' });
});

test('a person\'s turn comes before a bot\'s: a bot always waits, and would hide a player whose time runs', () => {
  const games = [game('bot', { against: bot }), game('person')];

  assert.equal(featuredChoice({ mine: games, lessons: [] }).entry.table.id, 'person');
});

test('the featured game stays featured while it is still a turn of the player\'s, when several are', () => {
  const games = [game('first', { against: bot }), game('second')];

  assert.equal(featuredChoice({ mine: games, lessons: [], previous: 'second' }).entry.table.id, 'second');
});

test('a featured game waiting on someone else gives way when another game\'s turn arrives', () => {
  const games = [game('waiting', { mine: false }), game('arrived')];

  assert.equal(featuredChoice({ mine: games, lessons: [], previous: 'waiting' }).entry.table.id, 'arrived');
});

test('a featured game whose action has the focus or a finger stays, whatever arrives, while it is not over', () => {
  const games = [game('waiting', { mine: false }), game('arrived')];

  assert.deepEqual(featuredChoice({ mine: games, lessons: [], previous: 'waiting', pinned: true }), { kind: 'waiting', entry: games[0] });
  assert.equal(featuredChoice({ mine: [game('waiting', { over: true }), game('arrived')], lessons: [], previous: 'waiting', pinned: true }).entry.table.id, 'arrived');
});

test('a game\'s featured view names the round and whom it is against, whose turn it is, and the action that follows it', () => {
  const view = featuredView({ kind: 'turn', entry: game('g', { round: 7, recap: { casts: 2 } }) });

  assert.equal(view.title, 'Round 7 against Rook');
  assert.equal(view.eyebrow, 'Your turn');
  assert.equal(view.status, '', 'the eyebrow says it');
  assert.equal(view.announced, 'Your turn');
  assert.equal(view.detail, '2 casts since your last visit');
  assert.equal(view.action, 'Play your turn →');
  assert.equal(view.label, 'Play your turn: round 7 against Rook');
  assert.equal(view.ground, 'home-ground-breach');
  assert.equal(view.creature, null);
  assert.equal(view.id, 'g');
});

test('a turn with more to say keeps it in the status, each number on one line with its unit', () => {
  const doc = page();
  const featured = mountFeatured(doc, { enter: () => {}, openCreate: () => {}, go: () => {}, session: null, ambience: () => {} });
  const both = game('g', { answerBy: new Date(Date.now() + 71 * 3600000 + 600000).toISOString() });
  both.table.seats[1].turn = true;
  featured.setLessons([]); featured.setGames([both]);
  assert.equal(doc.ids['featured-status'].textContent, 'Both players choose · 2\u00a0d 23\u00a0h left');
});

test('a game waiting on someone else says whom, and opens the game', () => {
  const view = featuredView({ kind: 'waiting', entry: game('g', { mine: false }) });

  assert.equal(view.eyebrow, 'Their turn');
  assert.equal(view.status, 'Rook\'s turn');
  assert.equal(view.action, 'Open the game →');
});

test('a game waiting on another player stands on the Warped listening, whatever its round; a bot\'s round in play keeps the ground by round', () => {
  assert.equal(featuredView({ kind: 'waiting', entry: game('g', { mine: false, round: 7 }) }).ground, 'warped-listening');
  const open = game('o', { against: { seat: 'player2', kind: 'open', name: null }, waiting: true, mine: false });
  assert.equal(featuredView({ kind: 'waiting', entry: open }).ground, 'warped-listening', 'a seat not yet taken waits too');
  assert.equal(featuredView({ kind: 'waiting', entry: game('b', { mine: false, against: bot, round: 7 }) }).ground, 'home-ground-breach');
  assert.equal(featuredView({ kind: 'turn', entry: game('t', { round: 7 }) }).ground, 'home-ground-breach');
});

test('a bot is named as one, and a table still waiting for a player says so without a round', () => {
  assert.equal(featuredView({ kind: 'turn', entry: game('g', { against: bot }) }).title, 'Round 1 against Hard (bot)');
  const open = featuredView({ kind: 'waiting', entry: game('o', { against: { seat: 'player2', kind: 'open', name: null }, waiting: true, mine: false }) });
  assert.equal(open.title, 'Your 3v3 table');
  assert.equal(open.status, 'Waiting for a player');
  const kept = featuredView({ kind: 'waiting', entry: { ...game('k', { against: { seat: 'player2', kind: 'open', name: null }, waiting: true, mine: false }), invited: { username: 'Rook' } } });
  assert.equal(kept.status, 'Waiting for Rook…');
});

test('a lesson\'s featured view is its place, its title and goal, its creature on the eclipse, and Play or Continue', () => {
  const first = featuredView({ kind: 'lesson', lesson: lesson(1, { skipped: true }) });
  assert.deepEqual([first.eyebrow, first.title, first.detail, first.action, first.ground, first.creature, first.link],
    ['Tutorial 1 of 4', 'Lesson 1', 'Goal 1.', 'Play →', 'home-ground-eclipse', 'home-ancestral', '/battle?scenario=t1']);
  assert.equal(first.label, 'Play: Lesson 1, tutorial 1 of 4');
  assert.equal(featuredView({ kind: 'lesson', lesson: lesson(3) }, { started: true }).action, 'Continue →');
});

test('a new battle is the duel in the mist, and a bot to play at once', () => {
  const view = featuredView({ kind: 'new' });

  assert.deepEqual([view.title, view.action, view.ground, view.creature], ['Choose your opponent', 'Play a bot →', 'mist-duel', null]);
});

test('the arrival line is the featured game\'s, and the battle reads it once for that table only', () => {
  const kept = new Map(), session = { getItem: key => kept.get(key) ?? null, setItem: (key, value) => kept.set(key, value), removeItem: key => kept.delete(key) };
  const entry = game('g', { round: 3 });
  assert.equal(arrivalLine(entry.table, ['player1']), 'Round 3 against Rook');

  writeArrival(session, 'g', arrivalLine(entry.table, ['player1']), ['g-token']);
  assert.ok(kept.has(ARRIVAL_KEY));
  assert.equal(arrivalFor(session, '?player1=g-token'), 'Round 3 against Rook', 'known by the address before the table is');
  assert.equal(arrivalFor(session, '?player1=other'), null);
  assert.equal(readArrival(session, 'other'), null, 'another table is not this one');
  assert.equal(readArrival(session, 'g'), 'Round 3 against Rook');
  assert.equal(readArrival(session, 'g'), null, 'read once');
  assert.equal(readArrival(null, 'g'), null);
  assert.equal(readArrival({ getItem: () => { throw new Error('blocked'); } }, 'g'), null);
});

// The few elements the featured space is drawn with.
function page() {
  const made = tag => ({ tag, children: [], dataset: {}, hidden: false, disabled: false, textContent: '', className: '', listeners: {}, attributes: {},
    classList: { set: new Set(), add(name) { this.set.add(name); }, remove(name) { this.set.delete(name); }, toggle(name, on) { if (on) this.set.add(name); else this.set.delete(name); }, contains(name) { return this.set.has(name); } },
    setAttribute(name, value) { this.attributes[name] = String(value); }, removeAttribute(name) { delete this.attributes[name]; }, getAttribute(name) { return this.attributes[name] ?? null; },
    append(...more) { this.children.push(...more); }, replaceChildren(...more) { this.children = more; },
    addEventListener(kind, heard) { this.listeners[kind] = heard; }, getBoundingClientRect() { return {}; } });
  const ids = Object.fromEntries(['featured', 'featured-art', 'featured-eyebrow', 'welcome', 'featured-status', 'featured-said', 'featured-detail', 'featured-action', 'new-table'].map(id => [id, made('div')]));
  const doc = { ids, getElementById: id => ids[id] ?? null, createElement: made, activeElement: null };
  return doc;
}

test('the featured space waits for both the games and the lessons, then fills in place, the same button each time', () => {
  const doc = page(), entered = [], kept = new Map();
  const session = { getItem: key => kept.get(key) ?? null, setItem: (key, value) => kept.set(key, value), removeItem: key => kept.delete(key) };
  const featured = mountFeatured(doc, { enter: entry => entered.push(entry.table.id), openCreate: () => {}, go: () => {}, session, ambience: () => {} });

  featured.setGames([game('g', { mine: false })]);
  assert.equal(doc.ids.featured.dataset.featured, 'loading');
  featured.setLessons([]);
  assert.equal(doc.ids.featured.dataset.featured, 'waiting');
  const button = doc.ids['featured-action'];
  assert.equal(button.textContent, 'Open the game →');

  featured.setGames([game('g')]);
  assert.equal(featured.shown(), 'g');
  assert.equal(doc.ids['featured-action'], button);
  assert.equal(button.textContent, 'Play your turn →');
  assert.equal(doc.ids['featured-status'].textContent, '');
  assert.equal(doc.ids['featured-eyebrow'].textContent, 'Your turn');
  assert.ok(doc.ids['featured-eyebrow'].classList.contains('motion-changed'), 'the eyebrow plays the number\'s beat');
  assert.equal(doc.ids['featured-said'].textContent, 'Your turn', 'said once');

  button.listeners.click();
  assert.deepEqual(entered, ['g']);
  assert.equal(button.textContent, 'Opening…');
  assert.equal(button.attributes['aria-busy'], 'true');
  assert.equal(arrivalFor(session, '?player1=g-token'), 'Round 1 against Rook', 'the battle\'s first frame is handed the game\'s line');
  assert.equal(readArrival(session, 'g'), 'Round 1 against Rook');
});

test('with nothing to resume or learn, the featured action plays a bot, and New battle is the quiet one under it', () => {
  const doc = page(), asked = [];
  const featured = mountFeatured(doc, { enter: () => {}, openCreate: one => asked.push(one), go: () => {}, session: null, ambience: () => {} });

  featured.setGames([]); featured.setLessons([]); featured.setDisabled(false);
  assert.equal(doc.ids.featured.dataset.featured, 'new');
  assert.equal(doc.ids['featured-action'].textContent, 'Play a bot →');
  assert.equal(doc.ids['featured-status'].dataset.empty, 'true', 'its line kept, unseen');
  doc.ids['featured-action'].listeners.click();
  assert.deepEqual(asked, [{ opponent: 'bot' }]);
});

test('the ground is drawn again only when it changes, so a repeated update leaves the picture settled', () => {
  const doc = page();
  const featured = mountFeatured(doc, { enter: () => {}, openCreate: () => {}, go: () => {}, session: null, ambience: () => {} });
  featured.setLessons([]);
  featured.setGames([game('g', { round: 2 })]);
  const drawn = doc.ids['featured-art'].children;
  featured.setGames([game('g', { round: 3 })]);
  assert.equal(doc.ids['featured-art'].children, drawn);
  featured.setGames([game('g', { round: 6 })]);
  assert.notEqual(doc.ids['featured-art'].children, drawn);
});

test('once someone can play, the duel is asked for before the lists are read, and kept when the choice stands on it', () => {
  const doc = page();
  const featured = mountFeatured(doc, { enter: () => {}, openCreate: () => {}, go: () => {}, session: null, ambience: () => {} });
  featured.playable();
  const drawn = doc.ids['featured-art'].children;
  assert.match(drawn[0].attributes.src, /mist-duel-/);
  assert.equal(doc.ids.featured.dataset.featured, 'loading');
  featured.setGames([game('g', { round: 2 })]); featured.setLessons([]);
  assert.equal(doc.ids['featured-art'].children, drawn, 'the same ground, not drawn again');
});

test('a lesson\'s action opens it', () => {
  const doc = page(), went = [];
  const featured = mountFeatured(doc, { enter: () => {}, openCreate: () => {}, go: link => went.push(link), session: null, ambience: () => {} });
  featured.setGames([]); featured.setLessons(series('new', 'new'));
  doc.ids['featured-action'].listeners.click();
  assert.deepEqual(went, ['/battle?scenario=t1']);
});

// What the reviewers found (UX9's review): each failed before its fix.
function mounted(options = {}) {
  const doc = page(), listeners = {};
  const win = { addEventListener: (kind, heard) => { listeners[kind] = heard; } };
  const featured = mountFeatured(doc, { enter: () => {}, openCreate: () => {}, go: () => {}, session: null, ambience: () => {}, win, ...options });
  return { doc, featured, listeners };
}

test('a page brought back from the back-forward cache gives the featured action back, no longer opening', () => {
  const { doc, featured, listeners } = mounted();
  featured.setLessons([]); featured.setGames([game('g')]);
  doc.ids['featured-action'].listeners.click();
  assert.equal(doc.ids['featured-action'].textContent, 'Opening…');

  listeners.pageshow({ persisted: true });
  assert.equal(doc.ids['featured-action'].textContent, 'Play your turn →');
  assert.equal(doc.ids['featured-action'].attributes['aria-busy'], undefined);
  featured.setGames([game('g', { mine: false })]);
  assert.equal(doc.ids['featured-action'].textContent, 'Open the game →', 'it follows the host again');
});

test('an action that could not open its game is given back', () => {
  const { doc, featured } = mounted({ enter: () => { throw new Error('refused'); } });
  featured.setLessons([]); featured.setGames([game('g')]);
  assert.throws(() => doc.ids['featured-action'].listeners.click());
  assert.equal(doc.ids['featured-action'].textContent, 'Play your turn →');
});

test('while it opens, the action is named by what it does now', () => {
  const { doc, featured } = mounted();
  featured.setLessons([]); featured.setGames([game('g', { round: 3 })]);
  doc.ids['featured-action'].listeners.click();
  assert.equal(doc.ids['featured-action'].attributes['aria-label'], 'Opening: round 3 against Rook');
});

test('a finger lifted keeps the game it pressed until the click is handled, whatever arrived meanwhile', () => {
  const entered = [];
  const { doc, featured } = mounted({ enter: entry => entered.push(entry.table.id) });
  featured.setLessons([]); featured.setGames([game('waits', { mine: false })]);
  const action = doc.ids['featured-action'];
  action.listeners.pointerdown();
  featured.setGames([game('waits', { mine: false }), game('arrived')]);
  action.listeners.pointerup?.();
  action.listeners.click();
  assert.deepEqual(entered, ['waits']);
});

test('on a host that saves no games, the featured Play a bot is disabled, as New battle is', () => {
  const { doc, featured } = mounted();
  featured.setLessons([]); featured.setGames([]);
  featured.setDisabled(true);
  assert.equal(doc.ids['featured-action'].disabled, true);
  featured.setGames([game('g')]);
  assert.equal(doc.ids['featured-action'].disabled, false, 'a game to resume is not a new table');
});

test('a turn that brings another game to the featured space is said too', () => {
  const { doc, featured } = mounted();
  featured.setLessons([]); featured.setGames([game('waits', { mine: false })]);
  featured.setGames([game('waits', { mine: false }), game('arrived')]);
  assert.equal(doc.ids['featured-said'].textContent, 'Your turn');
});

test('the featured lesson is the one Learn to play marks as next, whatever order the host lists them in', () => {
  const cards = [lesson(3), { ...lesson(2), next: true }, lesson(1, { done: true })];
  assert.equal(featuredChoice({ mine: [], lessons: cards }).lesson.id, 't2');
});

test('a round the host could not read in time keeps the last one known for that table', () => {
  const { doc, featured } = mounted();
  featured.setLessons([]); featured.setGames([game('g', { round: 7 })]);
  const later = game('g'); later.table.round = null;
  featured.setGames([later]);
  assert.equal(doc.ids.welcome.textContent, 'Round 7 against Rook');
  assert.match(doc.ids['featured-art'].children[0].attributes.src, /home-ground-breach-/);
});

test('a cue left unread goes stale within the minute', () => {
  const kept = new Map(), session = { getItem: key => kept.get(key) ?? null, setItem: (key, value) => kept.set(key, value), removeItem: key => kept.delete(key) };
  writeArrival(session, 'g', 'Round 3 against Rook', ['g-token'], 1000);
  assert.equal(arrivalFor(session, '?player1=g-token', 1000 + 59000), 'Round 3 against Rook');
  assert.equal(arrivalFor(session, '?player1=g-token', 1000 + 61000), null);
  assert.equal(readArrival(session, 'g', 1000 + 61000), null);
});

// The owner's review of PR #982: a press opens what it was pressed on, a person's turn outranks a bot's kept one,
// and creation waits for the host. Each failed before its fix.
test('a press opens what it was pressed on: a lesson pressed stays the lesson when a turn arrives before the click', () => {
  const went = [], entered = [];
  const { doc, featured } = mounted({ go: link => went.push(link), enter: entry => entered.push(entry.table.id) });
  featured.setLessons(series('new', 'new')); featured.setGames([]);
  const action = doc.ids['featured-action'];
  action.listeners.pointerdown();
  featured.setGames([game('ready')]);
  action.listeners.click();
  assert.deepEqual(went, ['/battle?scenario=t1']);
  assert.deepEqual(entered, []);
  assert.equal(action.textContent, 'Play your turn →', 'then the space follows the turn');
});

test('a press on a game that ended before the click opens nothing, and the space shows what is now there', () => {
  const entered = [];
  const { doc, featured } = mounted({ enter: entry => entered.push(entry.table.id) });
  featured.setLessons([]); featured.setGames([game('a')]);
  const action = doc.ids['featured-action'];
  action.listeners.pointerdown();
  featured.setGames([game('a', { over: true }), game('b')]);
  action.listeners.click();
  assert.deepEqual(entered, []);
  assert.equal(action.attributes['aria-label'], 'Play your turn: round 1 against Rook');
  assert.equal(featured.shown(), 'b');
});

test('a person\'s turn that arrives outranks the bot\'s turn on show', () => {
  const { featured } = mounted();
  featured.setLessons([]);
  featured.setGames([game('bot', { against: bot }), game('friend', { mine: false })]);
  assert.equal(featured.shown(), 'bot');
  featured.setGames([game('bot', { against: bot }), game('friend')]);
  assert.equal(featured.shown(), 'friend');
});

test('creation waits for the host: Play a bot is disabled until it says whether games are saved', () => {
  const { doc, featured } = mounted();
  featured.setLessons([]); featured.setGames([]);
  assert.equal(doc.ids['featured-action'].disabled, true, 'before the host answers');
  featured.setDisabled(false);
  assert.equal(doc.ids['featured-action'].disabled, false);
});
