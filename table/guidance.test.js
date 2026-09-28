import { test } from 'node:test';
import assert from 'node:assert/strict';
import { effectText, groupTargetLines, spellSummary, targetLines } from './guidance.js';

test('summary uses host amounts, order and the chosen speed', () => {
  const guide = { cost: 7, energyAfterCost: 13, turn: 4, chosenSpeed: 'Quick', quickCriticalChance: 0, standardCriticalChance: .64 };
  assert.equal(spellSummary(guide), 'Turn 4 · 7 energy → 13 after cost');
  assert.match(spellSummary({ ...guide, chosenSpeed: 'Standard' }), /64% critical/);
  assert.equal(spellSummary({ unavailableReason: 'A stunned creature cannot act.' }), 'A stunned creature cannot act.');
});

test('target selection filters engine outcomes without summing or multiplying them', () => {
  const damage = amount => ({ kind: 'DamageOutcome', amount });
  const guide = { targets: [
    { target: 8, plain: [damage(3)], critical: [damage(9)] },
    { target: 9, plain: [], critical: [] },
  ] };
  assert.equal(targetLines(guide).length, 2);
  assert.deepEqual(targetLines(guide, [8]), [{ target: 8, plain: 'Damage 3', critical: 'Damage 9' }]);
  assert.equal(targetLines(guide, [9])[0].plain, 'No effect on this target');
  assert.deepEqual(targetLines(guide, [77]), []);
});

test('lasting effects keep the engine duration and the caster marker', () => {
  assert.equal(effectText({ kind: 'ConditionOutcome', effect: { kind: 'Stun', duration: { rounds: 2 } } }), 'Stun · 2 rounds');
  assert.equal(effectText({ kind: 'HealOutcome', amount: 4, onCaster: true }), 'Heal 4 · caster');
});

test('candidates the spell treats alike share one line, in the host order', () => {
  const lines = [
    { target: 4, plain: 'Damage 3', critical: 'Damage 6' },
    { target: 5, plain: 'Damage 2', critical: 'Damage 4' },
    { target: 6, plain: 'Damage 3', critical: 'Damage 6' },
  ];
  assert.deepEqual(groupTargetLines(lines), [
    { targets: [4, 6], plain: 'Damage 3', critical: 'Damage 6' },
    { targets: [5], plain: 'Damage 2', critical: 'Damage 4' },
  ]);
  assert.deepEqual(groupTargetLines([]), []);
});
