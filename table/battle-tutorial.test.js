import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { conceptsOf } from './concepts.js';
import { CONTROLS, assistedQuestion, coachCard, coachHint, holdsTheNextRound, leavePath, lessonOverText, leavingSkips, finishedSeries, lessonStep, nextLesson, pastTheLesson, progressPath, tutorialBand, tutorialBrief, tutorialOf, withoutAssisted } from './battle-tutorial.js';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const concepts = conceptsOf(JSON.parse(readFileSync(join(root, 'table/rules.json'), 'utf8')));
const step = (creature, concepts, pointsAt, kind = 'Action', round = 2) =>
  ({ at: { kind, round, creature }, says: [`Line for ${creature}.`, 'A second line.'], concepts, pointsAt });
const lesson = (extra = {}) => ({ order: 1, of: 4, title: 'Your first combat', teaches: ['Action'], assisted: ['Evolution', 'Speed', 'TieOrder', 'Intent', 'Target'],
  steps: [step(2, ['health'], { creature: 4 }), step(3, ['energy'], { control: 'spells' }), step(null, ['defense', 'health'], null, 'Action', 3)], shown: 0, done: false, ...extra });
const view = (question = {}, tutorial = lesson(), round = 2) => ({ over: false, board: { roundNumber: round }, practice: { scenario: 'sc-repo-tutorial-your-first-combat', title: 'Your first combat', round: 2, tutorial },
  waitingFor: null, waitingAsked: null, waitingCreature: null, ...question });
const asked = (kind, creature, n = 7) => ({ waitingFor: kind, waitingAsked: n, waitingCreature: creature });

test('a game and a plain practice have no lesson', () => {
  assert.equal(tutorialOf({ board: {} }), null);
  assert.equal(tutorialOf({ practice: { scenario: 'sc-1', title: 'Bleed', round: 4 } }), null);
  assert.equal(tutorialOf({ practice: { tutorial: { order: 1, steps: [] } } }), null);
  assert.equal(tutorialOf(view()).title, 'Your first combat');
});

test('the band says Tutorial n of the series where a practice says Practice, and how far the person is', () => {
  assert.deepEqual(tutorialBand(lesson()), { eyebrow: 'TUTORIAL 1 OF 4', title: 'Your first combat', detail: 'Step 1 of 3', restart: 'Restart the round' });
  assert.equal(tutorialBand(lesson({ shown: 2 })).detail, 'Step 2 of 3');
  // Done, it is played again from its cut, not on.
  assert.deepEqual(tutorialBand(lesson({ shown: 3, done: true })), { eyebrow: 'TUTORIAL 1 OF 4', title: 'Your first combat', detail: 'Tutorial complete', restart: 'Play again' });
  // A series the host counts short of this tutorial's place still reads as a whole.
  assert.equal(tutorialBand(lesson({ order: 3, of: 1 })).eyebrow, 'TUTORIAL 3 OF 3');
});

test('a question of a kind the host answers is not drawn: the view asks nothing', () => {
  const tempo = view(asked('Speed', 1));
  assert.equal(assistedQuestion(tempo), true);
  assert.deepEqual(withoutAssisted(tempo), { ...tempo, waitingFor: null, waitingAsked: null, waitingCreature: null, options: {} });
  // What the question offered goes with it: a mutation roster or tie groups read off the options draw nothing.
  const growth = { ...view(asked('Evolution', null)), options: { evolution: { remainingPicks: 2, creatures: [{ creature: 1, availableTiers: ['root'] }] } } };
  assert.deepEqual(withoutAssisted(growth).options, {});
  const action = view(asked('Action', 2));
  assert.equal(assistedQuestion(action), false);
  assert.equal(withoutAssisted(action), action);
});

test('once every question is handed back, the person is asked every kind', () => {
  const tempo = view(asked('Speed', 1), lesson({ assisted: [] }));
  assert.equal(assistedQuestion(tempo), false);
  assert.equal(withoutAssisted(tempo), tempo);
});

test('once the tutorial is done no question is the person\'s: the lesson is not played on', () => {
  for (const kind of ['Action', 'Speed', 'Evolution']) {
    const after = view(asked(kind, 1), lesson({ shown: 3, done: true }), 3);
    assert.equal(pastTheLesson(after), true, kind);
    assert.deepEqual(withoutAssisted(after), { ...after, waitingFor: null, waitingAsked: null, waitingCreature: null, options: {} });
  }
  // Handed back or not: the host answers nothing past the lesson, and the page draws nothing either.
  assert.equal(withoutAssisted(view(asked('Speed', 1), lesson({ assisted: [], done: true }), 3)).waitingFor, null);
  assert.equal(pastTheLesson(view({}, lesson({ done: true }))), false);
  assert.equal(pastTheLesson(view(asked('Action', 2))), false);
});

test('once the tutorial is done the next round opens nothing by itself over the end card (#771)', () => {
  // Its upkeep or its turn order would hold Play again and Leave behind a window: only the end card remains.
  assert.equal(holdsTheNextRound(view({}, lesson({ done: true }), 3)), true);
  assert.equal(holdsTheNextRound(view(asked('Speed', 1), lesson({ shown: 3, done: true }), 3)), true);
  // Before the end, and outside a tutorial, the round reads as in any game.
  assert.equal(holdsTheNextRound(view(asked('Action', 2))), false);
  assert.equal(holdsTheNextRound({ board: {} }), false);
  assert.equal(holdsTheNextRound(null), false);
});

test('once the tutorial is done the dock says so, in place of waiting for a decision', () => {
  assert.equal(lessonOverText(view({}, lesson({ done: true }))), 'Tutorial complete.');
  assert.equal(lessonOverText(view()), null);
  assert.equal(lessonOverText({ board: {} }), null);
});

test('a game\'s question is never taken for an assisted one', () => {
  const game = { board: { roundNumber: 1 }, ...asked('Speed', 1) };
  assert.equal(withoutAssisted(game), game);
});

test('a step shows at the question it names: its kind, its round and its creature', () => {
  assert.equal(lessonStep(view(asked('Action', 2))).index, 0);
  assert.equal(lessonStep(view(asked('Action', 3))).index, 1);
  assert.equal(lessonStep(view(asked('Action', 1))), null);
  // A step that names no creature shows at any creature's question of its kind and round.
  assert.equal(lessonStep(view(asked('Action', 1), lesson(), 3)).index, 2);
  assert.equal(lessonStep(view(asked('Action', 2), lesson(), 4)), null);
});

test('no step shows without a question, at an assisted one, or once the match is over', () => {
  assert.equal(lessonStep(view()), null);
  assert.equal(lessonStep(view(asked('Speed', 2))), null);
  assert.equal(lessonStep({ ...view(asked('Action', 2)), over: true }), null);
});

test('the coach card says the step, its lines, its concept cards and the creature it points at', () => {
  const card = coachCard(view(asked('Action', 2)), { concepts });
  assert.equal(card.key, 'step:0');
  assert.equal(card.eyebrow, 'STEP 1 OF 3');
  assert.deepEqual(card.lines, ['Line for 2.', 'A second line.']);
  assert.deepEqual(card.cards.map(one => one.title), ['Health']);
  assert.equal(card.creature, '4');
  assert.equal(card.control, null);
  assert.deepEqual(coachCard(view(asked('Action', 1), lesson(), 3), { concepts }).cards.map(one => one.id), ['defense', 'health']);
});

// The cards come from the rulebook the page loads (rulebook-plan.md R4): before it is read, or when it cannot be,
// the step still shows, its lines and what it points at, without them.
test('a step shows without its cards until the rulebook is read', () => {
  const before = coachCard(view(asked('Action', 2)));
  assert.deepEqual(before.cards, []);
  // It still says it names a card, so the question's sheet waits for the book.
  assert.equal(before.names, 1);
  assert.equal(coachCard(view(asked('Action', 1), lesson(), 3)).names, 2);
  assert.deepEqual(before.lines, ['Line for 2.', 'A second line.']);
  assert.equal(before.creature, '4');
});

test('a step that points at a control names its element, and an unknown control points at nothing', () => {
  assert.equal(coachCard(view(asked('Action', 3))).control, 'combat-open');
  const odd = lesson(); odd.steps[1].pointsAt = { control: 'constructor' };
  assert.equal(coachCard(view(asked('Action', 3), odd)).control, null);
});

test('the coach card is held while results are read, and absent between questions', () => {
  assert.equal(coachCard(view(asked('Action', 2)), { held: true }), null);
  assert.equal(coachCard(view()), null);
  assert.equal(coachCard({ board: { roundNumber: 2 }, ...asked('Action', 2) }), null);
});

test('a tutorial the host says is done ends on its own card, and a later step still shows', () => {
  const done = coachCard(view({}, lesson({ shown: 3, done: true })));
  assert.equal(done.key, 'done');
  assert.equal(done.eyebrow, 'TUTORIAL 1 OF 4 · COMPLETE');
  assert.equal(done.lines[0], 'You finished Your first combat.');
  assert.equal(done.lines.length, 1);
  assert.equal(coachCard(view(asked('Action', 2), lesson({ done: true }))).key, 'step:0');
});

test('the controls a step may point at are the ones the host holds a lesson to', () => {
  const source = readFileSync(join(root, 'src/DownfallArena.Application/Matches/Scenarios/LessonControls.cs'), 'utf8');
  const listed = /Names \{ get; \} =\s*\[([^\]]+)\]/.exec(source)?.[1].match(/"([a-z]+)"/g).map(id => id.slice(1, -1));
  assert.deepEqual(Object.keys(CONTROLS), listed);
});

test('every control names an element of the battle page', () => {
  const page = readFileSync(join(root, 'table/battle.html'), 'utf8');
  for (const id of Object.values(CONTROLS)) assert.ok(page.includes(`id="${id}"`), id);
});

test('a tutorial\'s first card offers Start and Skip the tutorial, and says where it stays', () => {
  const brief = tutorialBrief(lesson());
  assert.equal(brief.begin, 'Start');
  assert.equal(brief.skip, 'Skip the tutorial');
  assert.match(brief.note, /Learn to play/);
});

test('a tutorial\'s first card says its round in a few words, under the brief\'s budget (UX8)', () => {
  const brief = tutorialBrief(lesson(), 2);
  assert.equal(brief.start, 'Round 2, against a bot.');
  assert.equal(brief.note, 'Nothing played here counts. Play it again any time from Learn to play.');
  const words = [brief.start, brief.note, brief.begin, brief.skip].join(' ').split(/\s+/).length;
  assert.ok(words <= 22, `${words} words the page writes`);
  // A round the host does not say is not written as a dash.
  assert.equal(tutorialBrief(lesson()).start, 'Against a bot.');
});

test('the series is finished only when the last lesson is done and the host lists every other one done', () => {
  const others = state => [1, 2, 3].map(order => ({ order, state }));
  assert.equal(finishedSeries({ tutorials: [...others('done'), { order: 4, state: 'new' }] }, lesson({ order: 4 })), true);
  assert.equal(finishedSeries({ tutorials: [...others('done').slice(0, 2), { order: 3, state: 'skipped' }, { order: 4 }] }, lesson({ order: 4 })), false);
  assert.equal(finishedSeries(null, lesson({ order: 4 })), false);
  assert.equal(finishedSeries({ tutorials: others('done') }, lesson({ order: 3 })), false);
  assert.equal(finishedSeries({ tutorials: others('done') }, lesson({ order: 4, of: undefined })), false);
});

test('the end card says how the board stands, read from the host\'s board', () => {
  const alive = (n, isAlive = true) => ({ id: n, isAlive });
  const done = coachCard({ ...view({}, lesson({ shown: 3, done: true })), board: { roundNumber: 2, allies: [alive(1), alive(2), alive(3, false)], enemies: [alive(4, false), alive(5), alive(6)] } });
  assert.deepEqual(done.lines, ['You finished Your first combat.', 'Your team: 2 of 3 standing. Opponent: 2 of 3.']);
});

test('the end card names the next lesson the host lists, and none after the last', () => {
  const series = { tutorials: [{ order: 1, title: 'Your first combat', link: '/battle?scenario=a' }, { order: 2, title: 'Who acts first', link: '/battle?scenario=b' }] };
  assert.deepEqual(nextLesson(series, lesson()), { label: 'Next: Who acts first', link: '/battle?scenario=b' });
  // The series not read, or the next one not listed: its place, and Learn to play, which lists it.
  assert.deepEqual(nextLesson(null, lesson()), { label: 'Next: tutorial 2 of 4', link: '/#learn-to-play' });
  assert.deepEqual(nextLesson({ tutorials: [{ order: 2, title: 'X', link: 'https://elsewhere.example/' }] }, lesson()), { label: 'Next: tutorial 2 of 4', link: '/#learn-to-play' });
  assert.equal(nextLesson(series, lesson({ order: 4 })), null);
  // A link the browser would read as another site is not followed.
  assert.equal(nextLesson({ tutorials: [{ order: 2, title: 'X', link: '/\\elsewhere.example/' }] }, lesson()).link, '/#learn-to-play');
});

test('a hint names the control the step asks for, by its own words, and nothing without one', () => {
  assert.equal(coachHint('Choose spell'), 'Next: Choose spell.');
  assert.equal(coachHint('  Choose tempos  '), 'Next: Choose tempos.');
  assert.equal(coachHint('Mutation tree ↗'), 'Next: Mutation tree.');
  assert.equal(coachHint(''), null);
  assert.equal(coachHint(null), null);
});

test('a tutorial that is done is not skipped: its card offers no Skip, and leaving it is only leaving', () => {
  assert.equal(tutorialBrief(lesson({ done: true })).skip, null);
  assert.equal(leavingSkips(view({}, lesson({ done: true }))), false);
});

test('leaving a tutorial whose match is over is not skipping it, even before the host says it done (#912)', () => {
  assert.equal(leavingSkips({ ...view(), over: true }), false);
});

test('leaving a tutorial before its end is skipping it; leaving a game or a plain practice is not', () => {
  assert.equal(leavingSkips(view()), true);
  assert.equal(leavingSkips({ board: {} }), false);
  assert.equal(leavingSkips({ practice: { scenario: 'sc-1', title: 'Bleed', round: 4 } }), false);
});

test('leaving a tutorial, done or not, lets go of its practice; a game or a plain practice is left where it is', () => {
  assert.equal(leavePath(view()), '/api/scenarios/sc-repo-tutorial-your-first-combat/practice');
  assert.equal(leavePath(view({}, lesson({ done: true }))), '/api/scenarios/sc-repo-tutorial-your-first-combat/practice');
  assert.equal(leavePath({ board: {} }), null);
  assert.equal(leavePath({ practice: { scenario: 'sc-1', title: 'Bleed', round: 4 } }), null);
});

test('the account\'s progress is said for the view\'s tutorial, skipped or done, and for nothing else', () => {
  assert.equal(progressPath(view(), 'skip'), '/api/tutorials/sc-repo-tutorial-your-first-combat/skip');
  assert.equal(progressPath(view(), 'done'), '/api/tutorials/sc-repo-tutorial-your-first-combat/done');
  assert.equal(progressPath(view(), 'forget'), null);
  assert.equal(progressPath({ practice: { scenario: 'sc-1' } }, 'skip'), null);
});
