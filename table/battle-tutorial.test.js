import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { CONTROLS, assistedQuestion, coachCard, lessonStep, tutorialBand, tutorialOf, withoutAssisted } from './battle-tutorial.js';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
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
  assert.deepEqual(tutorialBand(lesson()), { eyebrow: 'TUTORIAL 1 OF 4', title: 'Your first combat', detail: 'Step 1 of 3' });
  assert.equal(tutorialBand(lesson({ shown: 2 })).detail, 'Step 2 of 3');
  assert.equal(tutorialBand(lesson({ shown: 3, done: true })).detail, 'Tutorial complete');
  // A series the host counts short of this tutorial's place still reads as a whole.
  assert.equal(tutorialBand(lesson({ order: 3, of: 1 })).eyebrow, 'TUTORIAL 3 OF 3');
});

test('a question of a kind the host answers is not drawn: the view asks nothing', () => {
  const tempo = view(asked('Speed', 1));
  assert.equal(assistedQuestion(tempo), true);
  assert.deepEqual(withoutAssisted(tempo), { ...tempo, waitingFor: null, waitingAsked: null, waitingCreature: null });
  const action = view(asked('Action', 2));
  assert.equal(assistedQuestion(action), false);
  assert.equal(withoutAssisted(action), action);
});

test('once every question is handed back, the person is asked every kind', () => {
  const tempo = view(asked('Speed', 1), lesson({ assisted: [] }));
  assert.equal(assistedQuestion(tempo), false);
  assert.equal(withoutAssisted(tempo), tempo);
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
  const card = coachCard(view(asked('Action', 2)));
  assert.equal(card.key, 'step:0');
  assert.equal(card.eyebrow, 'STEP 1 OF 3');
  assert.deepEqual(card.lines, ['Line for 2.', 'A second line.']);
  assert.deepEqual(card.cards.map(one => one.title), ['Health']);
  assert.equal(card.creature, '4');
  assert.equal(card.control, null);
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
  assert.equal(done.eyebrow, 'TUTORIAL COMPLETE');
  assert.match(done.lines[0], /Your first combat/);
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
