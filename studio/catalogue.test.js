import { test } from 'node:test';
import assert from 'node:assert/strict';
import { active, packageFamilies, packageParents, packagesTeaching, spellMatches, effectText, targetText } from './catalogue.js';

const pack = (id, level, prerequisites = [], spells = []) => ({ id, name: id, enabled: true, document: { level, prerequisites, spells } });
const spell = { id: 'spark:v1', name: 'Spark', document: { spellType: 'Offensive', effects: [{ kind: 'Damage' }] } };

test('families follow authored prerequisites across aliases and multiple parents, without looping', () => {
  const catalogue = { aliases: { root: 'root:v1' }, tiers: [pack('root:v1', 1), pack('other', 1), pack('child', 2, ['root']), pack('hybrid', 3, ['child', 'other']), pack('cycle-a', 2, ['cycle-b']), pack('cycle-b', 3, ['cycle-a'])] };
  const families = packageFamilies(catalogue);
  assert.deepEqual(families.map(f => f.packages.map(p => p.id)), [['root:v1', 'child', 'hybrid'], ['other', 'hybrid']]);
  assert.deepEqual(packageParents(catalogue.tiers[3], catalogue).map(p => p.id), ['child', 'other']);
});

test('disabled and unreadable content cannot become a playable family', () => {
  const tiers = [pack('root', 1), { ...pack('disabled', 1), enabled: false }, { ...pack('broken', 1), problem: 'bad JSON' }];
  assert.deepEqual(active(tiers).map(p => p.id), ['root']);
  assert.equal(packageFamilies({ tiers }).length, 1);
  assert.deepEqual(packageFamilies({}), []);
});

test('a missing prerequisite stays visible rather than becoming an open package', () => {
  assert.deepEqual(packageParents(pack('child', 2, ['missing']), { tiers: [] }), [{ id: 'missing', name: 'missing', missing: true }]);
});

test('spell search combines words, effects and package names while respecting the type', () => {
  const catalogue = { aliases: { spark: 'spark:v1' }, tiers: [pack('Occultist', 1, [], ['spark'])] };
  assert.equal(packagesTeaching(spell, catalogue)[0].id, 'Occultist');
  assert.equal(spellMatches(spell, ' OCCULTIST damage ', 'All', catalogue), true);
  assert.equal(spellMatches(spell, 'spark missing', 'All', catalogue), false);
  assert.equal(spellMatches(spell, 'spark', 'Defensive', catalogue), false);
});

test('effect copy preserves permanent and temporary values without inventing mechanics', () => {
  assert.equal(effectText({ kind: 'DefenseBuff', amount: 2, permanent: true }), 'Gain 2 defense permanently');
  assert.equal(effectText({ kind: 'Bleed', amountPerRound: 3, durationRounds: 2 }), 'Deal 3 damage each round for 2 rounds');
  assert.equal(effectText({ kind: 'Stun', durationRounds: 1 }), 'Stun for 1 round');
  assert.equal(effectText({ kind: 'Stun' }), 'Stun for 1 round');
  assert.equal(effectText({ kind: 'FutureEffect' }), 'FutureEffect');
});

test('target copy distinguishes self, single and bounded multiple targets', () => {
  assert.equal(targetText({ origin: 'Self', scope: 'SingleTarget' }), 'Self');
  assert.equal(targetText({ origin: 'Enemy', scope: 'SingleTarget' }), '1 enemy');
  assert.equal(targetText({ origin: 'Ally', scope: 'Multi', maxTargets: 3 }), 'Up to 3 allies');
});
