import test from 'node:test';
import assert from 'node:assert/strict';
import { SPEEDS, decisionLabel, phaseLabel, stageClimb, stageLabel, stageNumeral, stageSpoken, stepLabel, tempoLabel } from './terms.js';

test('a served speed reads as its tempo, and the chooser still sends the engine values in play order', () => {
  assert.equal(tempoLabel('Quick'), 'Instinct');
  assert.equal(tempoLabel('Standard'), 'Precision');
  assert.deepEqual(SPEEDS, ['Quick', 'Standard']);
});

test('an unknown speed keeps its own word rather than vanishing', () => {
  assert.equal(tempoLabel('Slow'), 'Slow');
  assert.equal(tempoLabel(undefined), '');
});

test('the four round phases read Upkeep, Command, Combat and Aftermath', () => {
  assert.deepEqual(['StartOfRound', 'Planning', 'Combat', 'EndOfRound'].map(phaseLabel), ['Upkeep', 'Command', 'Combat', 'Aftermath']);
});

test('the speed question reads as tempo, and every other decision keeps its name', () => {
  assert.equal(decisionLabel('Speed'), 'Tempo');
  assert.equal(decisionLabel('TieOrder'), 'TieOrder');
});

test('a sub-phase reads as its words, with the speed step as the tempo', () => {
  assert.equal(stepLabel('Speed'), 'Tempo');
  assert.equal(stepLabel('TieOrder'), 'Tie Order');
  assert.equal(stepLabel(undefined), '');
});

test('a level reads as its stage, in Roman numerals', () => {
  assert.equal(stageLabel(2), 'Stage II');
  assert.deepEqual([1, 2, 3, 4].map(stageNumeral), ['I', 'II', 'III', 'IV']);
});

// #769: a screen reader may read "Stage I" as the pronoun I, so what is spoken keeps the number.
test('a stage is spoken with its number, not its numeral', () => {
  assert.equal(stageSpoken(1), 'Stage 1');
  assert.deepEqual([1, 2, 3, 4].map(stageSpoken), ['Stage 1', 'Stage 2', 'Stage 3', 'Stage 4']);
  assert.equal(stageSpoken(undefined), 'Stage unavailable', 'a listener hears no question mark');
});

test('the climb through the four Stages reads in the same numerals', () => {
  assert.equal(stageClimb(), 'Stage I → Stage II → Stage III → Stage IV');
});

test('a level with no numeral reads as written', () => {
  assert.equal(stageNumeral(0), '0');
  assert.equal(stageLabel(undefined), 'Stage ?');
});
