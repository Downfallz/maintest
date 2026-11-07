import { test } from 'node:test';
import assert from 'node:assert/strict';
import { battleSpellbook } from './battle-spellbook.js';

// Card types and classes as the host serves them (SpellType and CreatureClass names), never invented ones.
const model = overrides => ({ known: ['free', 'ward', 'costly', 'unknown'], spells: ['free', 'costly'], short: [], reference: { energy: 2 }, ...overrides });
const catalogue = { cards: [
  { id: 'costly', name: 'Strike', type: 'Offensive', creatureClass: 'Brawler', cost: 5, critical: '20%', criticalThreshold: 17, criticalNote: 'Precision only' },
  { id: 'ward', name: 'Ward', type: 'Defensive', creatureClass: 'Scoundrel', cost: 2, critical: '10%', effects: ['Protect'] },
  { id: 'free', name: 'Focus', type: 'Passive', creatureClass: 'Creature', cost: 0, critical: '0%', targeting: 'Self', casterEffects: ['Gain energy'] },
] };
const rows = (m = model(), cat = catalogue) => battleSpellbook(m, cat).flatMap(group => group.spells);
const row = (id, m, cat) => rows(m, cat).find(one => one.id === id);

test('groups follow the catalogue\'s order of types, with spells it does not carry last', () => {
  assert.deepEqual(battleSpellbook(model(), catalogue).map(one => one.type), ['Offensive', 'Defensive', 'Passive', 'Other spells']);
});
test('a free spell shows a zero cost rather than none', () => {
  assert.equal(row('free').cost, '0');
});
test('a spell that never crits shows no critical badge', () => {
  assert.equal(row('free').crit, null);
});
test('the catalogue critical names its tempo and the face of the d20', () => {
  assert.deepEqual(row('costly').crit, { value: '20%', hint: 'Precision only · d20 17+ · catalogue' });
});
test('full details list the target, the effects and the caster effects in card order', () => {
  assert.deepEqual(row('free').details, [{ role: 'target', text: 'Self' }, { role: 'caster', text: 'Gain energy' }]);
});
test('a card carries its creature class and that class\'s accent colour', () => {
  assert.equal(row('ward').creatureClass, 'Scoundrel'); assert.ok(row('ward').colour);
});
test('a spell the host does not offer cannot be chosen and says why', () => {
  const data = row('ward', model({ reference: { guidance: [{ spell: 'ward', unavailableReason: 'Stunned this turn' }] } }));
  assert.equal(data.offered, false); assert.equal(data.availability, 'Stunned this turn');
});
test('a catalogue cost cannot revoke what the host offers', () => {
  assert.equal(row('costly').offered, true);
});
test('an offered plan states the shortfall the host computed', () => {
  const m = model({ short: ['costly'], reference: { energy: 2, guidance: [{ spell: 'costly', energyAfterCost: -4 }] } });
  assert.equal(row('costly', m).availability, 'Plan · 4 energy short · may fizzle');
});
test('without the host\'s guide the shortfall is the catalogue cost against the board\'s energy', () => {
  assert.equal(row('costly', model({ short: ['costly'] })).availability, 'Plan · 3 energy short · may fizzle');
});
test('with neither a guide nor a cost the plan is a warning, not a guessed number', () => {
  assert.equal(row('costly', model({ short: ['costly'], reference: {} }), null).availability, 'Plan · Not enough energy yet · may fizzle');
});
test('the current guide\'s critical chance replaces the catalogue\'s, for the chosen tempo', () => {
  const m = model({ reference: { guidance: [{ spell: 'costly', chosenSpeed: 'Standard', quickCriticalChance: 0, standardCriticalChance: .5 }] } });
  assert.deepEqual(row('costly', m).crit, { value: '50%', hint: 'at this tempo' });
});
test('a current critical chance of zero hides the badge, even when the catalogue has one', () => {
  const m = model({ reference: { guidance: [{ spell: 'costly', chosenSpeed: 'Quick', quickCriticalChance: 0, standardCriticalChance: .5 }] } });
  assert.equal(row('costly', m).crit, null);
});
test('the host\'s guide gives the cost of an offered spell the catalogue does not carry', () => {
  const m = model({ spells: ['unknown'], reference: { guidance: [{ spell: 'unknown', cost: 3 }] } });
  assert.equal(row('unknown', m).cost, '3');
});
test('without a catalogue a spell keeps its raw identity and stays playable', () => {
  const data = row('free', model(), null);
  assert.equal(data.name, 'free'); assert.equal(data.offered, true); assert.equal(data.missing, true);
});
test('without a catalogue no cost, critical or effect is invented', () => {
  const data = row('free', model(), null);
  assert.equal(data.cost, ''); assert.equal(data.crit, null); assert.deepEqual(data.details, []);
});
test('a change of offers keeps the known spells where they were and adds new offers after them', () => {
  const before = rows(); const after = rows(model({ spells: ['costly', 'free', 'new'], short: ['costly'] }));
  assert.deepEqual(after.slice(0, 4).map(one => one.id), before.map(one => one.id)); assert.equal(after[4].id, 'new');
});
