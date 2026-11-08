import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { resolveEvolution } from './battle-evolution.js';
import { purchaseLog } from './battle-chronology.js';
import { createEvolutionRegistry, EVOLUTION_REGISTRY } from './battle-evolution-art.js';

// The packages as the catalogue serves them: level and prerequisites from the content, the family it draws.
const art = JSON.parse(readFileSync(new URL('../data/presentation/mutation-art.json', import.meta.url), 'utf8'));
const tiersRoot = new URL('../data/Tiers/', import.meta.url);
const packages = new Map(readdirSync(tiersRoot).filter(name => name.endsWith('.json'))
  .map(name => JSON.parse(readFileSync(new URL(name, tiersRoot), 'utf8')))
  .map(tier => [tier.id, { id: tier.id, level: tier.level, prerequisites: tier.prerequisites ?? [], anyOf: tier.anyOf ?? [], visual: art[tier.id] }]));

const T = name => `tier:${name}:v1`;
// A creature that bought `names` in that order, one Evolution sub-phase each, as the feed records it.
function bought(names, { creature = 1 } = {}) {
  const log = purchaseLog();
  log.receive(names.map((name, index) => ({ sequence: 10 * (index + 1), event: { kind: 'PurchasesRevealed', choices: [{ creature, tier: T(name) }] } })));
  // The set the board serves, in an order that is deliberately not the purchase order.
  return { creature: { id: String(creature), acquiredTiers: names.map(T).sort() }, history: { log } };
}
const resolve = (names, registry) => { const { creature, history } = bought(names); return resolveEvolution(creature, packages, history, registry); };
const withHybrids = pairs => createEvolutionRegistry(pairs.map(([dominant, hybrid]) => ({ id: `${dominant}-${hybrid}`, dominantTierId: T(dominant), hybridTierId: T(hybrid),
  family: 'hybrid', stage: 3, file: `evolutions/hybrids/${dominant}-${hybrid}.webp`, bounds: [0, 0, 10, 10], ground: [5, 10] })));

test('a creature that bought nothing shows the ancestral Stage 0', () => {
  const shown = resolve([]);
  assert.equal(shown.stage, 0); assert.equal(shown.artworkId, 'base-creature'); assert.equal(shown.dominantTierId, null);
  assert.equal(shown.chronology, 'complete');
});
test('with Stage 1 only, the latest Stage 1 bought shows', () => {
  assert.equal(resolve(['brute', 'prowler', 'occultist']).artworkId, 'warped-base');
  assert.equal(resolve(['occultist', 'prowler']).artworkId, 'predator-base');
});
test('a Stage 2 shows over every Stage 1', () => {
  const shown = resolve(['brute', 'berserker', 'prowler']);
  assert.equal(shown.stage, 2); assert.equal(shown.artworkId, 'brute-stage2-frenzied');
});
test('among the families holding a Stage 2, the one with the most mutations bought shows its latest Stage 2', () => {
  // Brute holds three (Brute, Ironhide, Frenzied), Predator two (Predator, Blighted): Frenzied, Brute's latest.
  const shown = resolve(['brute', 'prowler', 'plague_doctor', 'ironbound', 'berserker']);
  assert.equal(shown.family, 'brute'); assert.equal(shown.artworkId, 'brute-stage2-frenzied');
});
test('a tie between families goes to the one whose latest Stage 2 is the most recent', () => {
  assert.equal(resolve(['brute', 'prowler', 'ironbound', 'plague_doctor']).artworkId, 'predator-stage2-blighted');
  assert.equal(resolve(['brute', 'prowler', 'plague_doctor', 'ironbound']).artworkId, 'brute-stage2-ironhide');
});
test('the first Stage 3 bought becomes the creature\'s identity', () => {
  const shown = resolve(['occultist', 'necromancer', 'lich']);
  assert.equal(shown.stage, 3); assert.equal(shown.dominantTierId, T('lich')); assert.equal(shown.artworkId, 'warped-stage3-revenant');
});
test('a second Stage 3 never replaces the first, whichever family has more', () => {
  const shown = resolve(['occultist', 'necromancer', 'lich', 'prowler', 'assassin', 'deathstalker', 'parasite']);
  assert.equal(shown.dominantTierId, T('lich')); assert.equal(shown.artworkId, 'warped-stage3-revenant'); assert.equal(shown.hybridTierId, null);
});
test('another Stage 2 of the identity\'s own family selects its drawn hybrid; the Stage 2 that led to it does not', () => {
  const registry = withHybrids([['spiritcaller', 'shaman'], ['spiritcaller', 'necromancer']]);
  assert.equal(resolve(['occultist', 'shaman', 'spiritcaller'], registry).hybridTierId, null);
  const shown = resolve(['occultist', 'shaman', 'spiritcaller', 'necromancer'], registry);
  assert.equal(shown.hybridTierId, T('necromancer')); assert.equal(shown.artworkId, 'spiritcaller-necromancer');
  assert.equal(shown.dominantTierId, T('spiritcaller'));
});
test('a Stage 1 of another family selects its drawn hybrid', () => {
  const shown = resolve(['occultist', 'shaman', 'spiritcaller', 'prowler'], withHybrids([['spiritcaller', 'prowler']]));
  assert.equal(shown.hybridTierId, T('prowler')); assert.equal(shown.artworkId, 'spiritcaller-prowler');
});
test('a Stage 2 of another family selects its drawn hybrid', () => {
  const shown = resolve(['occultist', 'shaman', 'spiritcaller', 'prowler', 'plague_doctor'], withHybrids([['spiritcaller', 'plague_doctor']]));
  assert.equal(shown.hybridTierId, T('plague_doctor'));
});
test('hybrids rank own-family Stage 2, then another family\'s Stage 2, then another family\'s Stage 1', () => {
  const all = withHybrids([['spiritcaller', 'necromancer'], ['spiritcaller', 'plague_doctor'], ['spiritcaller', 'prowler']]);
  const names = ['occultist', 'shaman', 'spiritcaller', 'prowler', 'plague_doctor', 'necromancer'];
  assert.equal(resolve(names, all).hybridTierId, T('necromancer'));
  assert.equal(resolve(names, withHybrids([['spiritcaller', 'plague_doctor'], ['spiritcaller', 'prowler']])).hybridTierId, T('plague_doctor'));
  assert.equal(resolve(names, withHybrids([['spiritcaller', 'prowler']])).hybridTierId, T('prowler'));
});
test('of several hybrids in one rank, the latest mutation bought wins', () => {
  const registry = withHybrids([['spiritcaller', 'plague_doctor'], ['spiritcaller', 'marauder']]);
  assert.equal(resolve(['occultist', 'shaman', 'spiritcaller', 'prowler', 'plague_doctor', 'brute', 'marauder'], registry).hybridTierId, T('marauder'));
  assert.equal(resolve(['occultist', 'shaman', 'spiritcaller', 'brute', 'marauder', 'prowler', 'plague_doctor'], registry).hybridTierId, T('plague_doctor'));
});
test('a hybrid nobody drew is skipped for the next one, and with none the Stage 3 shows, never another identity', () => {
  const names = ['occultist', 'shaman', 'spiritcaller', 'prowler', 'plague_doctor', 'necromancer'];
  assert.equal(resolve(names, withHybrids([['spiritcaller', 'prowler']])).artworkId, 'spiritcaller-prowler');
  const none = resolve(names);
  assert.equal(none.hybridTierId, null); assert.equal(none.artworkId, 'warped-stage3-transcendent'); assert.equal(none.dominantTierId, T('spiritcaller'));
});
test('a capstone adds its effect and never changes the image', () => {
  const without = resolve(['brute', 'berserker', 'ravager']), withCapstone = resolve(['brute', 'berserker', 'ravager', 'titan']);
  assert.equal(withCapstone.artworkId, without.artworkId); assert.deepEqual(withCapstone.capstoneEffectIds, ['capstone-titan']);
  assert.deepEqual(without.capstoneEffectIds, []);
});
test('several capstones compose in their fixed layer order, whatever the order they were bought in', () => {
  const one = resolve(['occultist', 'necromancer', 'lich', 'archmage', 'brute', 'berserker', 'ravager', 'titan']);
  const other = resolve(['brute', 'berserker', 'ravager', 'titan', 'occultist', 'necromancer', 'lich', 'archmage']);
  assert.deepEqual(one.capstoneEffectIds, ['capstone-titan', 'capstone-archmage']); assert.deepEqual(other.capstoneEffectIds, one.capstoneEffectIds);
  assert.equal(one.dominantTierId, T('lich')); assert.equal(other.dominantTierId, T('ravager'));
});
test('a recorded frame resolves with the purchases known up to its own sequence', () => {
  // Revenant at 30, Deathstalker at 60: a frame between them, and one before either, show what was known then.
  const { creature, history } = bought(['occultist', 'necromancer', 'lich', 'prowler', 'assassin', 'deathstalker']);
  const at = (through, tiers) => resolveEvolution({ ...creature, acquiredTiers: tiers.map(T) }, packages, { ...history, through });
  assert.equal(at(30, ['occultist', 'necromancer', 'lich']).artworkId, 'warped-stage3-revenant');
  assert.equal(at(20, ['occultist', 'necromancer']).artworkId, 'warped-stage2-necrotic');
  assert.equal(at(60, ['occultist', 'necromancer', 'lich', 'prowler', 'assassin', 'deathstalker']).dominantTierId, T('lich'));
  assert.deepEqual(at(60, ['occultist', 'necromancer', 'lich', 'prowler', 'assassin', 'deathstalker']), at(60, ['deathstalker', 'assassin', 'prowler', 'lich', 'necromancer', 'occultist']));
});
test('without a purchase log the order is not guessed from the set: a fixed rule decides and says so', () => {
  const creature = { id: '1', acquiredTiers: [T('deathstalker'), T('lich'), T('prowler'), T('occultist'), T('assassin'), T('necromancer')] };
  const shown = resolveEvolution(creature, packages, null);
  assert.equal(shown.chronology, 'none');
  // Stage, then the family order, then id: the Predator Stage 3 counts first among the unrecorded.
  assert.equal(shown.dominantTierId, T('deathstalker'));
  assert.deepEqual(resolveEvolution({ ...creature, acquiredTiers: [...creature.acquiredTiers].reverse() }, packages, null), shown);
});
test('purchases the log never saw count as older than every recorded one', () => {
  const log = purchaseLog();
  log.receive([{ sequence: 50, event: { kind: 'PurchasesRevealed', choices: [{ creature: 1, tier: T('prowler') }] } }]);
  const shown = resolveEvolution({ id: '1', acquiredTiers: [T('brute'), T('prowler')] }, packages, { log });
  assert.equal(shown.chronology, 'partial'); assert.equal(shown.artworkId, 'predator-base');
});
test('a package with no drawn family, or a catalogue that serves none, leaves the ancestral creature', () => {
  assert.equal(resolveEvolution({ id: '1', acquiredTiers: [T('brute')] }, new Map(), null).artworkId, 'base-creature');
  assert.equal(resolveEvolution(null, packages, null).artworkId, 'base-creature');
});
test('the default registry has no hybrid yet, so every Stage 3 shows its own image', () => {
  assert.equal(EVOLUTION_REGISTRY.hybrid(T('spiritcaller'), T('necromancer')), null);
});
