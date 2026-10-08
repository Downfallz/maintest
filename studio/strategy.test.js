import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { EFFECT_GROUPS, energyCost, effectGroups, scopedSpells, strategyOverview, compactEffect } from './strategy.js';

const spell = (id, cost, effects = [], casterEffects = []) => ({ id, name: id, document: { energyCost: cost, effects, casterEffects } });
const pack = (id, spells) => ({ id, name: id, document: { spells } });
const fixture = () => ({
  aliases: { strike: 'strike:v1' },
  spells: [spell('strike:v1', 1, [{ kind: 'Damage', amount: 2 }]), spell('guard', 1, [{ kind: 'DefenseBuff', amount: 1 }, { kind: 'DefenseBuff', amount: 2 }]),
    spell('sacrifice', 3, [{ kind: 'Damage', amount: 10 }], [{ kind: 'Damage', amount: 4 }]), spell('momentum', 0, [{ kind: 'Damage', amount: 2 }], [{ kind: 'EnergyGain', amount: 2 }]),
    spell('future', undefined, [{ kind: 'NewEffect' }])],
  tiers: [pack('A', ['strike', 'guard']), pack('B', ['strike:v1', 'sacrifice']), { ...pack('disabled', ['momentum']), enabled: false }],
  creatures: [{ document: { startingSpellIds: ['strike', 'momentum'] } }],
});

test('cost distribution preserves zero and unknown costs and counts spells rather than effects', () => {
  const data = strategyOverview(fixture());
  assert.deepEqual(data.costs, [[0, 1], [1, 2], [3, 1], [null, 1]]);
  assert.equal(data.total, 5);
  assert.equal(data.facets.find(group => group.id === 'defense').count, 1);
  assert.equal(energyCost(spell('bad', -1)), null);
  assert.equal(energyCost(spell('bad', '1')), null);
  assert.equal(energyCost(spell('bad', 1.5)), null);
});

test('package union resolves aliases, deduplicates shared spells and only adds starters when requested', () => {
  const catalogue = fixture();
  assert.deepEqual(scopedSpells(catalogue, { scope: 'packages', packages: ['A', 'B', 'disabled'] }).map(item => item.id), ['strike:v1', 'guard', 'sacrifice']);
  assert.equal(scopedSpells(catalogue, { scope: 'packages', packages: ['A', 'B'], includeStarting: true }).length, 4);
  assert.deepEqual(scopedSpells(catalogue, { scope: 'starting' }).map(item => item.id), ['strike:v1', 'momentum']);
  assert.deepEqual(scopedSpells(catalogue, { scope: 'packages', packages: ['missing'] }), []);
  catalogue.tiers[1].document.prerequisites = ['A'];
  assert.deepEqual(scopedSpells(catalogue, { scope: 'packages', packages: ['B'] }).map(item => item.id), ['strike:v1', 'sacrifice']);
});

test('disabled, unreadable and duplicate spell entries do not inflate the overview', () => {
  const catalogue = fixture();
  catalogue.spells.push({ ...spell('off', 2), enabled: false }, { ...spell('bad', 2), problem: 'Invalid content' }, catalogue.spells[0]);
  catalogue.creatures.push({ enabled: false, document: { startingSpellIds: ['guard'] } });
  assert.equal(strategyOverview(catalogue).total, 5);
  assert.equal(scopedSpells(catalogue, { scope: 'starting' }).length, 2);
});

test('cost and stat filters intersect while stat counts retain the whole selected cost', () => {
  const data = strategyOverview(fixture(), { cost: 1, effect: 'damage' });
  assert.deepEqual(data.matches.map(item => item.id), ['strike:v1']);
  assert.equal(data.atCost, 2);
  assert.equal(data.facets.find(group => group.id === 'defense').count, 1);
  assert.equal(data.facets.find(group => group.id === 'healing').count, 0);
  assert.equal(strategyOverview(fixture(), { cost: 1, effect: 'healing' }).matches.length, 0);
  assert.deepEqual(strategyOverview(fixture(), { cost: null }).matches.map(item => item.id), ['future']);
});

test('caster costs stay separate from target effects and future effect kinds stay discoverable', () => {
  const catalogue = fixture();
  assert.equal(strategyOverview(catalogue, { cost: 0, effect: 'energy' }).matches.length, 0);
  assert.deepEqual(strategyOverview(catalogue, { cost: 0, effect: 'energy', side: 'caster' }).matches.map(item => item.id), ['momentum']);
  assert.deepEqual(strategyOverview(catalogue, { side: 'caster', effect: 'damage' }).matches.map(item => item.id), ['sacrifice']);
  assert.deepEqual([...effectGroups(catalogue.spells[4])], ['other']);
  assert.equal(strategyOverview(catalogue).facets.at(-1).count, 1);
  assert.equal(strategyOverview({}).total, 0);
  assert.equal(strategyOverview({}).facets.length, EFFECT_GROUPS.length);
});

test('compact values preserve direction, ticks, duration, permanence and missing amounts', () => {
  assert.equal(compactEffect({ kind: 'DefenseDebuff', amount: 2, permanent: true }), 'Defense − 2 · permanent');
  assert.equal(compactEffect({ kind: 'Bleed', amountPerRound: 3, durationRounds: 2 }), 'Bleed 3 / round · 2r');
  assert.equal(compactEffect({ kind: 'Stun', durationRounds: 2 }), 'Stun 2r');
  assert.equal(compactEffect({ kind: 'EnergyGain', amount: 2 }), 'Energy + 2');
  assert.equal(compactEffect({ kind: 'Damage' }), 'Damage ?');
  assert.equal(compactEffect({ kind: 'FutureEffect' }), 'FutureEffect');
});

// ADR 0101: a damage buff is a lasting effect like a defense buff, and it belongs with damage when filtering.
test('a damage buff reads as a lasting bonus to every hit and is filed under damage', () => {
  assert.equal(compactEffect({ kind: 'DamageBuff', amount: 2, durationRounds: 3 }), 'Damage per hit + 2 · 3r');
  assert.equal(compactEffect({ kind: 'DamageBuff', amount: 1, permanent: true }), 'Damage per hit + 1 · permanent');
  assert.deepEqual([...effectGroups({ document: { effects: [{ kind: 'DamageBuff', amount: 1 }] } })], ['damage']);
});

test('the shipped catalogue has a complete, nonduplicated cost distribution with every effect classified', () => {
  const root = new URL('../data/Spells/', import.meta.url);
  const spells = readdirSync(root, { recursive: true }).filter(path => path.endsWith('.json')).map(path => {
    const document = JSON.parse(readFileSync(new URL(path, root), 'utf8'));
    return { id: document.id, name: document.name, enabled: document.enabled, document };
  });
  const data = strategyOverview({ spells });
  assert.equal(data.costs.reduce((sum, [, count]) => sum + count, 0), spells.filter(item => item.enabled !== false).length);
  assert.equal(data.facets.some(group => group.id === 'other'), false);
  assert.equal(data.costs.some(([cost]) => cost === null), false);
});
