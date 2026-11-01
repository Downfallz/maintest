import test from 'node:test';
import assert from 'node:assert/strict';
import { SPEEDS, decisionLabel, phaseLabel, stageLabel, stepLabel, tempoLabel } from './terms.js';

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

test('a level reads as its stage', () => {
  assert.equal(stageLabel(2), 'Stage 2');
});
