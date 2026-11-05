import { test } from 'node:test';
import assert from 'node:assert/strict';
import { arenaSpellbook } from './arena-spellbook.js';
const model = overrides => ({ known: ['free', 'ward', 'costly', 'unknown'], spells: ['free', 'costly'], short: [], reference: { energy: 2 }, ...overrides });
const catalogue = { cards: [
  { id: 'free', name: 'Focus', type: 'Support', creatureClass: 'Root', cost: 0, critical: '0%', targeting: 'Self', casterEffects: ['Gain energy'] },
  { id: 'ward', name: 'Ward', type: 'Defense', creatureClass: 'Guardian', cost: 2, critical: '10%', effects: ['Protect'] },
  { id: 'costly', name: 'Strike', type: 'Offense', creatureClass: 'Brute', cost: 5, critical: '20%', criticalNote: 'Precision only' },
] };
const rows = (m = model(), cat = catalogue) => arenaSpellbook(m, cat).flatMap(group => group.spells);
test('served types group multiclass cards, preserve zero cost and omit zero critical badges', () => {
  const groups = arenaSpellbook(model(), catalogue);
  assert.deepEqual(groups.map(one => one.type), ['Support', 'Defense', 'Offense', 'Other spells']);
  assert.equal(groups[0].spells[0].cost, '0'); assert.equal(groups[0].spells[0].critical, '');
  assert.deepEqual(groups[0].spells[0].details, [{ role: 'target', text: 'Self' }, { role: 'caster', text: 'Gain energy' }]);
  assert.equal(groups[1].spells[0].family, 'Guardian'); assert.ok(groups[1].spells[0].colour);
});
test('only host offers authorize cards, and host refusal reasons remain readable', () => {
  const data = rows(model({ reference: { guidance: [{ spell: 'ward', unavailableReason: 'Stunned this turn' }] } }));
  assert.equal(data[1].offered, false); assert.equal(data[1].availability, 'Stunned this turn');
  assert.equal(data[2].offered, true); // Catalogue cost cannot revoke an offered spell.
});
test('hidden intent keeps the offered energy plan explicit, using the current guide first', () => {
  const m = model({ short: ['costly'], reference: { energy: 2, guidance: [{ spell: 'costly', energyAfterCost: -4 }] } });
  assert.equal(rows(m)[2].availability, 'Plan · 4 energy short · may fizzle');
  assert.equal(rows(model({ short: ['costly'] }))[2].availability, 'Plan · 3 energy short · may fizzle');
  assert.equal(rows(model({ short: ['costly'], reference: {} }), null)[2].availability, 'Plan · Not enough energy yet · may fizzle');
});
test('current critical guidance replaces catalogue chance, including a real zero', () => {
  const m = model({ reference: { guidance: [{ spell: 'costly', chosenSpeed: 'Quick', quickCriticalChance: 0, standardCriticalChance: .5 }] } });
  assert.equal(rows(m)[2].critical, '');
  m.reference.guidance[0].chosenSpeed = 'Standard'; assert.equal(rows(m)[2].critical, '50% crit · current');
  assert.equal(rows()[2].critical, '20% Precision crit · catalogue');
});
test('missing catalogue keeps raw identities playable without inventing costs or effects', () => {
  const data = rows(model(), null);
  assert.equal(data[0].id, 'free'); assert.equal(data[0].name, 'free'); assert.equal(data[0].offered, true);
  assert.equal(data[0].cost, ''); assert.equal(data[0].critical, ''); assert.deepEqual(data[0].details, []); assert.equal(data[0].missing, true);
});
test('changing availability preserves reference order and never borrows another creatures spells', () => {
  const before = rows(); const after = rows(model({ spells: ['costly', 'free', 'new'], short: ['costly'] }));
  assert.deepEqual(after.slice(0, 4).map(one => one.id), before.map(one => one.id));
  assert.equal(after[4].id, 'new'); assert.equal(after.some(one => one.id === 'enemy-secret'), false);
});
