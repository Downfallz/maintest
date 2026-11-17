import test from 'node:test';
import assert from 'node:assert/strict';
import { SPEEDS, decisionLabel, phaseLabel, stageLabel, stageNumeral, stepLabel, tempoLabel } from './terms.js';

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

test('a level with no numeral reads as written', () => {
  assert.equal(stageNumeral(0), '0');
  assert.equal(stageLabel(undefined), 'Stage ?');
});
