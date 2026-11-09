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
  return { creature: { id: String(creature), acquiredTiers: names.map(T).sort((a, b) => a.localeCompare(b)) }, history: { log } };
}
const resolve = (names, registry) => { const { creature, history } = bought(names); return resolveEvolution(creature, packages, history, registry); };
// Hybrids are registered by the two illustrations they combine, as the catalogue's packages name them.
const artworkOf = name => packages.get(T(name)).visual.artwork;
// No hybrid drawn at all: what the resolver does with the Stage 3's identity alone.
const undrawn = createEvolutionRegistry([]);
const withHybrids = pairs => createEvolutionRegistry(pairs.map(([dominant, hybrid]) => ({ id: `${dominant}-${hybrid}`, dominant: artworkOf(dominant), hybrid: artworkOf(hybrid),
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
  const shown = resolve(['occultist', 'necromancer', 'lich', 'prowler', 'assassin', 'deathstalker', 'parasite'], undrawn);
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
  const none = resolve(names, undrawn);
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
test('a recorded frame resolves the purchases it owned, in the order they were made', () => {
  // Revenant at 30, Deathstalker at 60: a frame owning only the first ones shows what it owned then.
  const { creature, history } = bought(['occultist', 'necromancer', 'lich', 'prowler', 'assassin', 'deathstalker']);
  const at = tiers => resolveEvolution({ ...creature, acquiredTiers: tiers.map(T) }, packages, history);
  assert.equal(at(['occultist', 'necromancer', 'lich']).artworkId, 'warped-stage3-revenant');
  assert.equal(at(['occultist', 'necromancer']).artworkId, 'warped-stage2-necrotic');
  assert.equal(at(['occultist', 'necromancer', 'lich', 'prowler', 'assassin', 'deathstalker']).dominantTierId, T('lich'));
  assert.deepEqual(at(['occultist', 'necromancer', 'lich', 'prowler', 'assassin', 'deathstalker']), at(['deathstalker', 'assassin', 'prowler', 'lich', 'necromancer', 'occultist']));
});
test('a frame borrowed by an earlier interlude keeps its purchases in their order, never as older than the rest', () => {
  // An upkeep interlude at sequence 65 borrows the round's first combat frame, which already owns Deathstalker,
  // revealed at 70: Revenant, bought first, stays the identity.
  const log = purchaseLog();
  log.receive([{ sequence: 30, event: { kind: 'PurchasesRevealed', choices: [T('occultist'), T('necromancer'), T('lich')].map(tier => ({ creature: 1, tier })) } },
    { sequence: 70, event: { kind: 'PurchasesRevealed', choices: [T('prowler'), T('assassin'), T('deathstalker')].map(tier => ({ creature: 1, tier })) } }]);
  const frame = { id: '1', acquiredTiers: ['occultist', 'necromancer', 'lich', 'prowler', 'assassin', 'deathstalker'].map(T) };
  const shown = resolveEvolution(frame, packages, { log });
  assert.equal(shown.dominantTierId, T('lich')); assert.equal(shown.chronology, 'complete');
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
test('the default registry draws hybrids for the Predator and Warped Stage 3s only: a Brute Stage 3 shows its own image', () => {
  assert.equal(EVOLUTION_REGISTRY.hybrid('brute-stage3-ravager', 'predator-stage2-open-maw', 'predator'), null);
  assert.equal(resolve(['brute', 'berserker', 'ravager', 'prowler', 'parasite', 'occultist', 'necromancer']).artworkId, 'brute-stage3-ravager');
});

// The approved Predator hybrids (ADR 0152, docs/art/predator-hybrid-aaa-intake.md), each reached by a real path: the
// Stage 1 and the Stage 2 that lead to the Stage 3, then the mutations that lend their traits.
const soulreaver = ['prowler', 'parasite', 'soulreaver'], deathstalker = ['prowler', 'assassin', 'deathstalker'];
const blightweaver = ['prowler', 'plague_doctor', 'blightweaver'];
const warped = ['occultist', 'necromancer'], brute = ['brute', 'berserker'];
test('each Predator Stage 3 with one influence shows the portrait approved for it', () => {
  const approved = [
    [[...soulreaver, 'plague_doctor'], 'predator-soulreaver-01'], [[...soulreaver, ...warped], 'predator-soulreaver-02'],
    [[...soulreaver, ...brute], 'predator-soulreaver-03'],
    [[...deathstalker, 'plague_doctor'], 'predator-deathstalker-05'], [[...deathstalker, 'parasite'], 'predator-deathstalker-05'],
    [[...deathstalker, ...warped], 'predator-deathstalker-06'], [[...deathstalker, ...brute], 'predator-deathstalker-07'],
    [[...blightweaver, 'parasite'], 'predator-blightweaver-09'], [[...blightweaver, ...brute], 'predator-blightweaver-10'],
    [[...blightweaver, ...warped], 'predator-blightweaver-11'],
  ];
  for (const [names, id] of approved) {
    const shown = resolve(names);
    assert.equal(shown.artworkId, id, names.join(' ')); assert.equal(shown.artworkFile, `evolutions/hybrids/${id}.webp`);
    assert.equal(shown.stage, 3); assert.equal(shown.dominantTierId, T(names[2])); assert.equal(shown.ultimate, false);
  }
  // A Stage 1 of another family lends its family's traits too, as the ranks of ADR 0151 already say.
  assert.equal(resolve([...soulreaver, 'occultist']).artworkId, 'predator-soulreaver-02');
});
test('the Stage 2 that led to a Predator Stage 3, or a line no portrait was drawn for, leaves its own image', () => {
  assert.equal(resolve(soulreaver).artworkId, 'predator-stage3-soulreaver');
  assert.equal(resolve([...soulreaver, 'assassin']).artworkId, 'predator-stage3-soulreaver');
  assert.equal(resolve([...blightweaver, 'assassin']).artworkId, 'predator-stage3-blightweaver');
  assert.equal(resolve([...blightweaver, 'assassin']).hybridTierId, null);
});
test('the Ultimate shows once the creature holds a Stage 2 carrying each other influence drawn for its Stage 3', () => {
  const ultimates = [
    [[...soulreaver, 'plague_doctor', ...warped, ...brute], 'predator-soulreaver-04', ['plague_doctor', 'berserker', 'necromancer']],
    [[...deathstalker, ...brute, 'parasite', ...warped], 'predator-deathstalker-08', ['parasite', 'necromancer', 'berserker']],
    [[...blightweaver, ...warped, ...brute, 'parasite'], 'predator-blightweaver-12', ['parasite', 'berserker', 'necromancer']],
  ];
  for (const [names, id, lenders] of ultimates) {
    const shown = resolve(names);
    assert.equal(shown.artworkId, id); assert.equal(shown.ultimate, true); assert.equal(shown.dominantTierId, T(names[2]));
    assert.deepEqual(shown.hybridTierIds, lenders.map(T));
  }
});
test('the Ultimate takes a Stage 2 of every influence: a Stage 1 only lends its traits to a single hybrid', () => {
  const shown = resolve([...soulreaver, 'plague_doctor', 'occultist', ...brute]);
  assert.equal(shown.ultimate, false); assert.equal(shown.artworkId, 'predator-soulreaver-01');
});
test('two influences and no Ultimate show the first by rank, as no double hybrid is drawn', () => {
  // Another Predator line ranks first, whatever was bought after it.
  assert.equal(resolve([...soulreaver, 'plague_doctor', ...warped]).artworkId, 'predator-soulreaver-01');
  // Between two other families, the latest Stage 2 bought.
  assert.equal(resolve([...deathstalker, ...warped, ...brute]).artworkId, 'predator-deathstalker-07');
  assert.equal(resolve([...deathstalker, ...brute, ...warped]).artworkId, 'predator-deathstalker-06');
});
test('the hybrid follows the mutations held, not the board\'s order: with no recorded order it is the same every time', () => {
  const names = [...deathstalker, ...brute, 'parasite'];
  const creature = { id: '1', acquiredTiers: names.map(T).sort((a, b) => a.localeCompare(b)) };
  const once = resolveEvolution(creature, packages, null), again = resolveEvolution({ ...creature, acquiredTiers: [...creature.acquiredTiers].reverse() }, packages, null);
  assert.equal(once.artworkId, 'predator-deathstalker-05'); assert.equal(again.artworkId, once.artworkId); assert.equal(once.chronology, 'none');
});
test('a package whose illustration is not registered keeps its identity and draws no image of its own', () => {
  const unknown = new Map(packages); unknown.set(T('lich'), { ...packages.get(T('lich')), visual: { ...packages.get(T('lich')).visual, artwork: 'not-drawn-yet' } });
  const { creature, history } = bought(['occultist', 'necromancer', 'lich']);
  const shown = resolveEvolution(creature, unknown, history);
  assert.equal(shown.dominantTierId, T('lich')); assert.equal(shown.artworkId, null);
});

// The approved Warped hybrids (ADR 0152, docs/art/warped-hybrid-aaa-intake.md), reached by real paths: the Warped
// Stage 1 and the Stage 2 that lead to each Stage 3, then the mutations that lend their traits.
const cataclysm = ['occultist', 'elementalist', 'harbinger'], revenant = ['occultist', 'necromancer', 'lich'];
const transcendent = ['occultist', 'shaman', 'spiritcaller'];
const predatorLine = ['prowler', 'parasite'];
test('each Warped Stage 3 with one influence shows the portrait approved for it, at Stage 1 or Stage 2', () => {
  const approved = [
    [[...cataclysm, 'necromancer'], 'warped-cataclysm-sibling'], [[...cataclysm, 'shaman'], 'warped-cataclysm-sibling'],
    [[...cataclysm, 'prowler'], 'warped-cataclysm-predator'], [[...cataclysm, ...predatorLine], 'warped-cataclysm-predator'],
    [[...cataclysm, 'brute'], 'warped-cataclysm-brute'], [[...cataclysm, ...brute], 'warped-cataclysm-brute'],
    [[...revenant, 'elementalist'], 'warped-revenant-sibling'], [[...revenant, 'shaman'], 'warped-revenant-sibling'],
    [[...revenant, 'prowler'], 'warped-revenant-predator'], [[...revenant, ...predatorLine], 'warped-revenant-predator'],
    [[...revenant, 'brute'], 'warped-revenant-brute'], [[...revenant, ...brute], 'warped-revenant-brute'],
    [[...transcendent, 'elementalist'], 'warped-transcendent-sibling'], [[...transcendent, 'necromancer'], 'warped-transcendent-sibling'],
    [[...transcendent, 'prowler'], 'warped-transcendent-predator'], [[...transcendent, ...predatorLine], 'warped-transcendent-predator'],
    [[...transcendent, 'brute'], 'warped-transcendent-brute'], [[...transcendent, ...brute], 'warped-transcendent-brute'],
  ];
  for (const [names, id] of approved) {
    const shown = resolve(names);
    assert.equal(shown.artworkId, id, names.join(' ')); assert.equal(shown.artworkFile, `evolutions/hybrids/${id}.webp`);
    assert.equal(shown.stage, 3); assert.equal(shown.dominantTierId, T(names[2])); assert.equal(shown.ultimate, false);
    assert.equal(shown.hybridTierId, T(names.at(-1)), names.join(' '));
  }
});
test('the Stage 2 that led to a Warped Stage 3 lends nothing: its own Stage 3 shows', () => {
  for (const path of [cataclysm, revenant, transcendent]) {
    const shown = resolve(path);
    assert.equal(shown.artworkId, packages.get(T(path[2])).visual.artwork); assert.equal(shown.hybridTierId, null);
  }
});
test('each Warped Ultimate shows once a Stage 2 carries every other influence drawn for its Stage 3', () => {
  const ultimates = [
    [[...cataclysm, 'shaman', ...brute, ...predatorLine], 'warped-cataclysm-ultimate', ['shaman', 'parasite', 'berserker']],
    [[...revenant, ...predatorLine, 'elementalist', ...brute], 'warped-revenant-ultimate', ['elementalist', 'berserker', 'parasite']],
    [[...transcendent, ...brute, 'necromancer', ...predatorLine], 'warped-transcendent-ultimate', ['necromancer', 'parasite', 'berserker']],
  ];
  // Lenders in ADR 0151's ranks: the sibling line first, then other families' Stage 2, the latest bought first.
  for (const [names, id, lenders] of ultimates) {
    const shown = resolve(names);
    assert.equal(shown.artworkId, id); assert.equal(shown.ultimate, true); assert.equal(shown.dominantTierId, T(names[2]));
    assert.deepEqual(shown.hybridTierIds, lenders.map(T));
  }
});
test('a Warped Ultimate needs a Stage 2 of every influence: a Stage 1 of a family, or the leading line, never completes it', () => {
  // Predator held at Stage 1 only: the other two influences rank first, the sibling line before all.
  assert.equal(resolve([...revenant, 'shaman', ...brute, 'prowler']).artworkId, 'warped-revenant-sibling');
  assert.equal(resolve([...revenant, 'shaman', ...brute, 'prowler']).ultimate, false);
  // Brute held at Stage 1 only.
  assert.equal(resolve([...cataclysm, 'necromancer', ...predatorLine, 'brute']).artworkId, 'warped-cataclysm-sibling');
  // Only the line that led to Transcendent stands for the Warped influence: no sibling, so no Ultimate.
  const shown = resolve([...transcendent, ...predatorLine, ...brute]);
  assert.equal(shown.ultimate, false); assert.equal(shown.artworkId, 'warped-transcendent-brute');
});
test('two Warped influences and no Ultimate show the first by rank, the latest bought within a rank', () => {
  // A sibling Warped line outranks another family's Stage 2, whatever was bought later.
  assert.equal(resolve([...revenant, 'shaman', ...predatorLine]).artworkId, 'warped-revenant-sibling');
  assert.equal(resolve([...revenant, ...predatorLine, 'elementalist']).artworkId, 'warped-revenant-sibling');
  // Between two other families' Stage 2, the latest bought.
  assert.equal(resolve([...cataclysm, ...predatorLine, ...brute]).artworkId, 'warped-cataclysm-brute');
  assert.equal(resolve([...cataclysm, ...brute, ...predatorLine]).artworkId, 'warped-cataclysm-predator');
  // Another family's Stage 2 outranks another family's Stage 1, even an older one.
  assert.equal(resolve([...transcendent, ...brute, 'prowler']).artworkId, 'warped-transcendent-brute');
  // Between two Stage 1s of other families, the latest bought.
  assert.equal(resolve([...transcendent, 'brute', 'prowler']).artworkId, 'warped-transcendent-predator');
  assert.equal(resolve([...transcendent, 'prowler', 'brute']).artworkId, 'warped-transcendent-brute');
});
test('a Warped hybrid with no recorded order is chosen by a fixed rule, the same every time', () => {
  const names = [...cataclysm, 'brute', 'berserker', 'prowler', 'parasite'];
  const creature = { id: '1', acquiredTiers: names.map(T).sort((a, b) => a.localeCompare(b)) };
  const once = resolveEvolution(creature, packages, null);
  const again = resolveEvolution({ ...creature, acquiredTiers: [...creature.acquiredTiers].reverse() }, packages, null);
  // The fixed rule orders unrecorded purchases by stage, then family (brute, predator, warped): Parasite counts as the latest.
  assert.equal(once.chronology, 'none'); assert.equal(once.artworkId, 'warped-cataclysm-predator'); assert.equal(again.artworkId, once.artworkId);
});
test('a recorded Warped frame shows the hybrid of the mutations it owned, not of later purchases', () => {
  const { history } = bought([...revenant, ...predatorLine, ...brute]);
  // The frame before the Brute line was bought: the log knows later purchases, the frame does not own them.
  const before = resolveEvolution({ id: '1', acquiredTiers: [...revenant, ...predatorLine].map(T) }, packages, history);
  const after = resolveEvolution({ id: '1', acquiredTiers: [...revenant, ...predatorLine, ...brute].map(T) }, packages, history);
  assert.equal(before.artworkId, 'warped-revenant-predator'); assert.equal(after.artworkId, 'warped-revenant-brute');
  assert.equal(before.chronology, 'complete');
});
test('a Warped Stage 3 bought second never takes the identity of the first, nor lends it a sibling portrait', () => {
  // Revenant first, Cataclysm second: Revenant stays, and Stormborn (bought on the way to Cataclysm) is its sibling.
  const shown = resolve([...revenant, 'elementalist', 'harbinger']);
  assert.equal(shown.dominantTierId, T('lich')); assert.equal(shown.artworkId, 'warped-revenant-sibling');
});
