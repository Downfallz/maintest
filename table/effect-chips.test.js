import { test } from 'node:test';
import assert from 'node:assert/strict';
import { endedChip, immuneChip, outcomeChip, tickChip } from './effect-chips.js';

test('a spell outcome is a chip of what moved and by how much, keeping its words', () => {
  assert.deepEqual(outcomeChip({ kind: 'DamageOutcome', amount: 7, critical: true, text: 'Damage 7!' }), { kind: 'damage', value: '−7', critical: true, text: 'Damage 7!' });
  assert.deepEqual(outcomeChip({ kind: 'HealOutcome', amount: 3, text: 'Heal 3' }), { kind: 'heal', value: '+3', text: 'Heal 3' });
  assert.equal(outcomeChip({ kind: 'EnergyOutcome', amount: 2, text: 'Energy 2 · caster' }).value, '+2');
  assert.deepEqual(outcomeChip({ kind: 'EnergyDrainOutcome', amount: 2, text: '' }), { kind: 'drain', value: '−2', text: '' });
});

test('a condition names itself and its amount, and an unknown outcome keeps its text', () => {
  assert.deepEqual(outcomeChip({ kind: 'ConditionOutcome', effect: { kind: 'DefenseBuff', amount: 2 }, text: 'Defense Buff 2' }),
    { kind: 'condition', condition: 'DefenseBuff', value: 'Defense Buff 2', text: 'Defense Buff 2' });
  assert.equal(outcomeChip({ kind: 'ConditionOutcome', effect: { kind: 'Bleed', amountPerRound: 2 }, text: '' }).value, 'Bleed 2');
  assert.deepEqual(outcomeChip({ kind: 'SummonOutcome', text: 'Summon' }), { kind: 'other', value: 'Summon', text: 'Summon' });
});

test('upkeep ticks and turnover ends speak the same chips', () => {
  assert.deepEqual(tickChip({ sign: '+', amount: 2, unit: 'energy' }), { kind: 'energy', value: '+2', text: '+2 EN' });
  assert.deepEqual(tickChip({ sign: '−', amount: 2, unit: 'HP' }), { kind: 'damage', value: '−2', text: '−2 HP' });
  assert.deepEqual(tickChip({ sign: '+', amount: 3, unit: 'HP' }), { kind: 'heal', value: '+3', text: '+3 HP' });
  assert.deepEqual(endedChip('DefenseBuff'), { kind: 'ends', condition: 'DefenseBuff', value: 'Defense Buff ends', text: 'Defense Buff ends' });
  assert.equal(immuneChip().kind, 'immune');
});
