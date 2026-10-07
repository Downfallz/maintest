import { test } from 'node:test';
import assert from 'node:assert/strict';
import { active, originText, packageFamilies, packageParents, packageRequirements, packagesTeaching, requirementText, spellInTier, spellMatches, spellOrigins, effectText, targetText, criticalText, percentText } from './catalogue.js';

const pack = (id, level, prerequisites = [], spells = []) => ({ id, name: id, enabled: true, document: { level, prerequisites, spells } });
const spell = { id: 'spark:v1', name: 'Spark', document: { spellType: 'Offensive', effects: [{ kind: 'Damage' }], casterEffects: [{ kind: 'Bleed' }] } };

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
  assert.equal(spellMatches(spell, 'bleed', 'All', catalogue), true);
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

test('a spell card shows its critical bonus signed and to the hundredth, and nothing without one', () => {
  assert.equal(criticalText({ criticalChance: 0.28 }), '+28% crit');
  assert.equal(criticalText({ criticalChance: 0.283 }), '+28.3% crit');
  assert.equal(criticalText({ criticalChance: 0 }), '');
  assert.equal(criticalText({}), '');
  assert.equal(percentText(0.767), '76.7%');
});

test('a spell names every package that teaches it, lowest tier first, and the starting kit it belongs to', () => {
  const catalogue = {
    aliases: { spark: 'spark:v1' },
    tiers: [pack('Warlock', 3, [], ['spark']), pack('Occultist', 1, [], ['spark']), { ...pack('Gone', 1, [], ['spark']), enabled: false }],
    creatures: [{ id: 'imp', enabled: true, document: { startingSpellIds: ['spark'] } }],
  };
  const origins = spellOrigins(spell, catalogue);
  assert.deepEqual(origins.packages.map(p => [p.name, p.level]), [['Occultist', 1], ['Warlock', 3]]);
  assert.equal(origins.starting, true);
  assert.equal(originText(origins), 'Tier 1 · Occultist / Tier 3 · Warlock / Starting kit');
  assert.equal(originText(spellOrigins(spell, { tiers: [] })), 'Not taught by any package');
});

test('the tier filter keeps a spell taught at that level or, for the kit, one a creature starts with', () => {
  const catalogue = { tiers: [pack('Occultist', 2, [], ['spark:v1'])], creatures: [{ id: 'off', enabled: false, document: { startingSpellIds: ['spark:v1'] } }] };
  assert.equal(spellInTier(spell, 'All', catalogue), true);
  assert.equal(spellInTier(spell, '2', catalogue), true);
  assert.equal(spellInTier(spell, '1', catalogue), false);
  assert.equal(spellInTier(spell, 'Starting', catalogue), false);
  assert.equal(spellMatches(spell, 'tier 2', 'All', catalogue), true);
});

// ADR 0100: a capstone names no all-of prerequisite, only an any-of list of its family's closers. Read through
// the all-of list alone it would be a family root of its own, with nothing above it.
test('a capstone opened by any of a family closers joins that family rather than starting one', () => {
  const capstone = { ...pack('titan', 4, [], []), document: { level: 4, prerequisites: [], anyOf: ['ravager', 'warmonger:v1'], spells: [], passive: { stunImmunity: true } } };
  const catalogue = { aliases: { 'warmonger': 'warmonger:v1' }, tiers: [
    pack('brute', 1), pack('berserker', 2, ['brute']), pack('marauder', 2, ['brute']),
    pack('ravager', 3, ['berserker']), pack('warmonger:v1', 3, ['marauder']), capstone,
  ] };

  const families = packageFamilies(catalogue);
  assert.deepEqual(families.map(f => f.root.id), ['brute']);
  assert.equal(families[0].packages.at(-1).id, 'titan');
  assert.deepEqual(packageParents(capstone, catalogue).map(p => p.id), ['ravager', 'warmonger:v1']);
  assert.deepEqual(packageRequirements(capstone, catalogue).allOf, []);
  assert.deepEqual(packageRequirements(capstone, catalogue).anyOf.map(p => p.id), ['ravager', 'warmonger:v1']);
});

test('a requirement line says which packages are all needed and which one of is enough', () => {
  const catalogue = { tiers: [pack('a', 1), pack('b', 1), pack('c', 2), pack('d', 2)] };
  assert.equal(requirementText(pack('opener', 1), catalogue), '');
  assert.equal(requirementText(pack('both', 2, ['a', 'b']), catalogue), 'Requires a + b');
  assert.equal(requirementText({ id: 'cap', document: { prerequisites: [], anyOf: ['c', 'd'] } }, catalogue), 'Requires one of c / d');
  assert.equal(requirementText({ id: 'mixed', document: { prerequisites: ['a'], anyOf: ['c', 'd'] } }, catalogue), 'Requires a and one of c / d');
  assert.equal(requirementText({ id: 'torn', document: { prerequisites: 'a', anyOf: 3 } }, catalogue), '');
});

test('a damage buff reads as more damage on every hit, for its duration', () => {
  assert.equal(effectText({ kind: 'DamageBuff', amount: 2, durationRounds: 2 }), 'Deal 2 more damage on every hit for 2 rounds');
  assert.equal(effectText({ kind: 'DamageBuff', amount: 1, permanent: true }), 'Deal 1 more damage on every hit permanently');
});
