import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { SPELL_ART, spellVisual } from './arena-spell-art.js';
import { actionCue, createArenaPlayback } from './arena-playback.js';

const cast = (spell, effects = [], extra = {}) => ({ actor: { id: 1 }, action: { spell, targets: [2, 3] }, status: 'Resolved', effects, ...extra });
const effect = (id, tone) => ({ target: { id }, tone, text: 'Confirmed result' });

test('every authored spell has an explicit visual and retired or invented IDs do not enter the vocabulary', () => {
  const root = new URL('../data/Spells/', import.meta.url);
  const spells = readdirSync(root, { recursive: true }).filter(path => path.endsWith('.json'))
    .map(path => JSON.parse(readFileSync(new URL(path, root), 'utf8')).id);
  assert.deepEqual(Object.keys(SPELL_ART).sort(), spells.sort());
});
test('stable IDs choose the approved fiction despite misleading historical IDs and translated names', () => {
  for (const [id, family, motif] of [
    ['tornado', 'physical', 'wave'], ['extort', 'physical', 'bite'], ['soul_devourer', 'biological', 'bite'],
    ['summon_minions', 'necromantic', 'spirits'], ['bone_ward', 'necromantic', 'bones'],
    ['toxic_waves', 'astral', 'void'], ['noxious_cure', 'biological', 'spores'],
  ]) {
    const action = cast(`spell:${id}:v1`, [], { spell: 'Renamed or translated' });
    assert.equal(spellVisual(action).family, family); assert.equal(spellVisual(action).motif, motif);
  }
});
test('unknown IDs fall back to neutral and never borrow a profile from a display label', () => {
  for (const id of [undefined, 'spell:lightning_bolt:v2', 'Shock', 'toString', '__proto__']) {
    assert.equal(spellVisual(cast(id, [], { spell: 'Shock' })).family, 'neutral');
  }
});
test('recipients come only from applied outcomes, are deduplicated, and self-cost is not a contact target', () => {
  const cue = actionCue(cast('spell:summon_minions:v1', [effect(1, 'harm'), effect(2, 'condition'), effect(2, 'harm')]));
  assert.deepEqual(cue.recipients, ['1', '2']); assert.deepEqual(cue.contact, ['2']);
  assert.equal(cue.visual.delivery, 'cast'); assert.equal(cue.visual.family, 'necromantic');
});
test('failed spells never launch their signature or touch targets even if a malformed record contains effects', () => {
  const cue = actionCue(cast('spell:throwing_star:v1', [effect(2, 'harm')], { status: 'Fizzled' }));
  assert.equal(cue.visual.motif, 'fizzle'); assert.deepEqual(cue.effects, []);
  assert.deepEqual(cue.recipients, []); assert.deepEqual(cue.contact, []); assert.deepEqual(cue.struck, []);
});
test('empty outcomes do not fabricate recipients from declarations or the spell profile', () => {
  const cue = actionCue(cast('spell:tornado:v1'));
  assert.deepEqual(cue.recipients, []); assert.deepEqual(cue.contact, []);
});
test('the live queue carries the wire spell ID through renamed catalogue labels and anchors to a real recipient', () => {
  const before = [1, 2, 3].map(id => ({ id, health: 20, maxHealth: 20, energy: 5, isAlive: true }));
  const view = { board: { allies: before.slice(0, 1), enemies: before.slice(1), roundNumber: 2 }, feed: [] };
  const queue = createArenaPlayback({ onChange() {}, schedule() {} });
  const cards = new Map([['spell:bone_ward:v1', { name: 'Localized Bonewall' }]]);
  queue.receive(view, cards);
  queue.receive({ ...view, feed: [{ sequence: 1, event: { kind: 'CombatActionResolved', roundId: 2,
    resolution: { action: { actor: 1, spell: 'spell:bone_ward:v1', targets: [2, 3] } },
    appliedOutcomes: [{ kind: 'DamageOutcome', target: 1, amount: 2 }, { kind: 'ConditionOutcome', target: 2, effect: { kind: 'DefenseBuff', amount: 2 } }],
    frame: { before, after: before } } }] }, cards);
  assert.match(queue.model().label, /Localized Bonewall/);
  assert.equal(queue.model().cue.visual.motif, 'bones'); assert.equal(queue.model().frame.target, '2');
  assert.deepEqual(queue.model().cue.recipients, ['1', '2']);
});
