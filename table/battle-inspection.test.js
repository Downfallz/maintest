import { test } from 'node:test';
import assert from 'node:assert/strict';
import { creatureInspection, creatureCues, spokenCues } from './battle-inspection.js';
const catalogue = { cards: [
  { id: 'strike', name: 'Strike', cost: 0, type: 'Offense', targeting: 'One enemy', effects: ['Damage 4'], casterEffects: ['Gain 1 energy'], critical: '20%', criticalThreshold: 17, criticalNote: 'Double damage' },
  { id: 'secret', name: 'Not known yet' },
], packages: [
  { id: 'root', name: 'Brute', level: 1, initiativeBonus: 2, spells: ['strike', 'secret'], tags: ['Resilient'] },
  { id: 'other', name: 'Unacquired', level: 1, initiativeBonus: 9, spells: ['strike'] },
  { id: 'cap', name: 'Shared passive', level: 4, initiativeBonus: 0, spells: [], passiveLines: ['For every creature of the side: defense +2'] },
] };
const one = { id: '1', label: 'Beast · 1', enemy: false, health: 9, maxHealth: 20, energy: 2, totalDefense: 4, currentInitiative: 7,
  acquiredTiers: ['root', 'cap'], knownSpells: ['strike'], conditions: [] };
test('ownership is explicit, separate from the known spellbook and private choices', () => {
  const model = creatureInspection({ ...one, evolutionChoices: [{ tier: 'other' }], intents: [{ spell: 'secret' }] }, [one], catalogue);
  assert.deepEqual(model.mutations.map(pack => pack.id), ['root', 'cap']);
  assert.deepEqual(model.spells.map(spell => spell.id), ['strike']);
  assert.deepEqual(model.spells[0].sources, ['Brute']);
  assert.deepEqual(model.mutations[1].passives, ['For every creature of the side: defense +2']);
  assert.equal(model.mutations[1].initiative, 0);
  assert.doesNotMatch(JSON.stringify(model), /Unacquired|Not known yet|secret|intents|evolutionChoices/);
});
test('full spell faces retain zero costs, targeting, caster effects, crit and catalogue qualifiers', () => {
  const spell = creatureInspection(one, [one], catalogue).spells[0];
  assert.equal(spell.cost, '0'); assert.match(spell.critical, /20%.*Precision only.*d20 17\+.*catalogue/);
  assert.deepEqual(spell.details, [{ role: 'target', text: 'One enemy' }, { role: 'effect', text: 'Damage 4' }, { role: 'caster', text: 'Gain 1 energy' }]);
  assert.equal(spell.criticalNote, 'Double damage'); assert.match(spell.crit.hint, /Precision only.*d20 17\+/); assert.equal(spell.crit.value, '20%');
});
test('defeated opponents and new, permanent, expiring conditions keep named sources and immunity', () => {
  const creature = { ...one, enemy: true, isAlive: false, isStunImmune: true, conditions: [
    { effect: { kind: 'Bleed', amountPerRound: 3 }, remainingRounds: 2, isFresh: true, source: { caster: 2, spell: 'strike' } },
    { effect: { kind: 'DefenseBuff', amount: 2 }, remainingRounds: null, isFresh: true },
    { effect: { kind: 'Regeneration', amountPerRound: 1 }, remainingRounds: 1 },
  ] };
  const model = creatureInspection(creature, [one, { id: '2', label: 'Beast · 2' }], catalogue);
  assert.equal(model.side, 'OPPONENT');
  assert.match(model.effects, /Defeated.*Immune to stun.*Bleed 3 · new · first countdown pending · from Beast · 2 · Strike/);
  assert.match(model.effects, /DefenseBuff 2 · permanent/); assert.match(model.effects, /Regeneration 1 · 1 round(?!s)/);
  assert.equal(creatureCues(creature), 'Immune to stun · Bleed 3 · DefenseBuff 2 · Regeneration 1');
});
test('missing snapshots or catalogue fields stay unavailable rather than borrowing current knowledge', () => {
  assert.equal(creatureInspection(null, [], catalogue), null);
  const missing = creatureInspection({ id: 3, acquiredTiers: ['lost'], knownSpells: ['missing'] }, [], null);
  assert.equal(missing.mutations[0].name, 'lost'); assert.equal(missing.spells[0].name, 'missing');
  assert.equal(missing.mutations[0].missing, true); assert.equal(missing.spells[0].missing, true);
  const legacy = creatureInspection({ id: 3 }, [], catalogue);
  assert.equal(legacy.mutationsRecorded, false); assert.equal(legacy.spellsRecorded, false);
  assert.equal(legacy.stats, '—/— HP · — energy · — defense · — initiative');
});
test('a condition with several rounds left counts them in the plural', () => {
  const model = creatureInspection({ ...one, conditions: [{ effect: { kind: 'Bleed', amountPerRound: 2 }, remainingRounds: 3 }] }, [one], catalogue);
  assert.match(model.effects, /Bleed 2 · 3 rounds/);
});
test('a stunned creature is said to be stunned once, whatever repeats it', () => {
  assert.equal(spokenCues('Stunned · Bleed 3', 'STUNNED'), 'Stunned. Bleed 3');
  assert.equal(spokenCues('Stunned', 'Stunned. Select target'), 'Stunned. Select target');
});
test('a creature with no cue and no tag adds nothing to what is spoken', () => {
  assert.equal(spokenCues('', ''), ''); assert.equal(spokenCues('', 'ACTING'), 'ACTING');
});
test('the inspection sheet carries each stat, each status and each condition apart, for tiles and cards', () => {
  const creature = { ...one, form: 'brute', isStunned: true, isStunImmune: false, conditions: [
    { effect: { kind: 'Bleed', amountPerRound: 3 }, remainingRounds: 2, source: { caster: 2, spell: 'strike' } }] };
  const model = creatureInspection(creature, [one, { id: '2', label: 'Beast · 2' }], catalogue);
  assert.deepEqual(model.sheet, { health: 9, maxHealth: 20, energy: 2, defense: 4, initiative: 7 });
  assert.deepEqual(model.statuses, [{ key: 'stun', text: 'Stunned' }]);
  assert.deepEqual(model.conditions, [{ kind: 'Bleed', name: 'Bleed 3', duration: '2 rounds', source: 'from Beast · 2 · Strike' }]);
  assert.equal(model.form, 'brute'); assert.equal(model.enemy, false);
});
test('a defeated, immune creature wears both, and an unknown stat stays unknown rather than zero', () => {
  const model = creatureInspection({ id: 4, isAlive: false, isStunImmune: true }, [], catalogue);
  assert.deepEqual(model.statuses, [{ key: 'defeated', text: 'Defeated' }, { key: 'immune', text: 'Immune to stun' }]);
  assert.equal(model.sheet.health, null); assert.equal(model.conditionsRecorded, false);
});
