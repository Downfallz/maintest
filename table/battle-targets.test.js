import test from 'node:test';
import assert from 'node:assert/strict';
import { battleTargetPreview as preview, battleTargetState as state, battleTargetTag as tag } from './battle-targets.js';
const legal = { candidates: [1, 2, 4, 5], minTargets: 1, maxTargets: 2 };
const damage = amount => ({ kind: 'DamageOutcome', amount });
function model() {
  return { opened: true, waitingFor: 'Action', chosen: 'bolt', picked: [4], emptyCast: false,
    target: { spell: 'bolt', legalTargets: legal }, reference: { tempo: 'Standard',
      creatures: [1, 2, 4, 5].map(id => ({ id, name: `Creature ${id}` })),
      guidance: [{ spell: 'bolt', cost: 2, energyAfterCost: 3, standardCriticalChance: .25, quickCriticalChance: 0,
        targets: [1, 2, 4, 5].map(target => ({ target, plain: [damage(3)], critical: [damage(6)] })),
        casterEffects: [{ kind: 'HealOutcome', amount: 4, onCaster: true }] }] } };
}
test('the host cost and remaining energy are exact even for an unaffordable hidden plan', () => {
  const m = model(); m.waitingFor = 'Intent'; m.target = null;
  Object.assign(m.reference.guidance[0], { cost: 0, energyAfterCost: -2 });
  const p = preview(m); assert.equal(p.cost, '0 energy → -2 after cost');
  assert.deepEqual(p.rows, []); assert.match(p.note, /hidden spell resolves/);
});
test('only selected legal targets are described and identical effects group without repeating caster effects', () => {
  const m = model(); m.picked = [2, 4, 99];
  assert.deepEqual(preview(m).rows, [{ name: 'Creature 2, Creature 4', plain: 'Damage 3', critical: 'Damage 6' }]);
  assert.equal(preview(m).caster, 'On caster, once: Heal 4 · caster');
});
for (const id of [1, 2, 4]) test(`the host preview identifies selected self, ally or enemy ${id}`, () => {
  const m = model(); m.picked = [id]; assert.equal(preview(m).rows[0].name, `Creature ${id}`);
});
test('no selected targets means no target damage estimate', () => {
  const m = model(); m.picked = []; assert.deepEqual(preview(m).rows, []); assert.match(preview(m).note, /Select a legal target/);
});
test('zero critical chance at the chosen tempo suppresses impossible critical outcomes', () => {
  const m = model(); m.reference.guidance[0].chosenSpeed = 'Quick';
  assert.equal(preview(m).critical, '0% critical at this tempo'); assert.equal(preview(m).rows[0].critical, '');
});
test('a missing chance stays unknown instead of borrowing the other tempo', () => {
  const m = model(); m.reference.tempo = null; assert.match(preview(m).critical, /unavailable/);
});
test('missing guidance never falls back to another spell or a stale estimate', () => {
  const m = model(); m.reference.guidance[0].spell = 'other';
  assert.match(preview(m).reason, /Preview unavailable for this question/); assert.deepEqual(preview(m).rows, []);
});
test('a missing outcome differs from an explicit empty outcome', () => {
  const m = model(); m.reference.guidance[0].targets = [{ target: 4, plain: [], critical: null }];
  assert.equal(preview(m).rows[0].plain, 'No effect on this target'); assert.equal(preview(m).rows[0].critical, 'Preview unavailable');
});
test('a host refusal hides hypothetical effects without changing the offer', () => {
  const m = model(); m.reference.guidance[0].unavailableReason = 'Cannot cast while stunned';
  const p = preview(m); assert.equal(p.reason, 'Cannot cast while stunned'); assert.deepEqual(p.rows, []); assert.equal(p.caster, '');
  assert.equal(state(m, 4).selectable, true);
});
test('a targetless cast shows the caster effects the host supplies', () => {
  const m = model(); m.target.legalTargets = { candidates: [], minTargets: 0, maxTargets: 0 }; m.picked = [];
  assert.match(preview(m).note, /No targets required/); assert.match(preview(m).caster, /Heal 4/);
});
test('a targetless cast tells an explicit empty caster outcome from a missing one', () => {
  const m = model(); m.target.legalTargets = { candidates: [], minTargets: 0, maxTargets: 0 }; m.picked = [];
  m.reference.guidance[0].casterEffects = []; assert.equal(preview(m).caster, 'No effect on caster.');
  delete m.reference.guidance[0].casterEffects; assert.equal(preview(m).caster, 'Caster preview unavailable.');
});
test('an empty cast cannot advertise effects on any target or caster', () => {
  const m = model(); m.emptyCast = true;
  assert.deepEqual(preview(m).rows, []); assert.equal(preview(m).caster, ''); assert.match(preview(m).note, /may fizzle/);
  assert.equal(state(m, 4).selectable, false);
});
test('selection states distinguish legal, selected, unavailable and a full multi-target selection', () => {
  const m = model(); assert.equal(state(m, 4).label, '✓ Selected'); assert.equal(state(m, 2).label, '+ Select target');
  assert.equal(state(m, 99).label, '— Unavailable'); m.picked.push(5);
  assert.equal(state(m, 2).label, '— Selection full'); assert.equal(state(m, 4).selectable, true);
});
test('a single-target selection allows a direct replacement', () => {
  const m = model(); m.target.legalTargets = { ...legal, maxTargets: 1 };
  assert.equal(state(m, 2).label, '+ Change target'); assert.equal(state(m, 2).selectable, true);
});
test('closing the asking removes every preview and target state', () => {
  const m = model(); m.opened = false; assert.equal(preview(m), null); assert.equal(state(m, 4), null);
});
test('a stunned target keeps its stun on the tag behind the selection mark', () => {
  const m = model(), stunned = { isAlive: true, isStunned: true };
  assert.deepEqual(tag(state(m, 2), stunned), { tag: '+ Stunned', spoken: 'Stunned. Select target' });
  assert.deepEqual(tag(state(m, 4), stunned), { tag: '✓ Stunned', spoken: 'Stunned. Selected' });
});
test('an unstunned target reads its selection state and speaks it without the mark', () => {
  const m = model(), ready = { isAlive: true, isStunned: false };
  assert.deepEqual(tag(state(m, 2), ready), { tag: '+ Select target', spoken: 'Select target' });
  assert.deepEqual(tag(state(m, 99), ready), { tag: '— Unavailable', spoken: 'Unavailable' });
});
test('a defeated creature reads unavailable on its tag and says it is defeated', () => {
  const m = model(); assert.deepEqual(tag(state(m, 99), { isAlive: false, isStunned: true }), { tag: '— Unavailable', spoken: 'Defeated. Unavailable' });
});
test('no target state means no target tag', () => { assert.equal(tag(null, { isAlive: true }), null); });
