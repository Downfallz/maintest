// The coarse value of a spell, read the way `check-knobs` reads it (learning/src/downfall_learning/knobs.py:
// `cast_value`, `_caster_value`, `_rounds_a_cast`), with no DOM in it (ADR 0024).
//
// It is a comparison between spells and nothing more: no board, no defense, no cap at a target's health, no kill
// term (the largest weight in the game) and no threat behind a defensive effect. A `Multi` spell is priced for
// every target it is allowed, which overstates it late in a match. Played matches and the balance objective
// (data/balance/README.md) are what decide whether content is balanced; this number only says roughly where a
// spell sits next to the ones offered beside it. The Python reading is the reference: a change to one is a
// change to both, and `value.test.js` pins the numbers it prints for the authored content.

/** What a critical hit multiplies damage and a direct heal by, as `RuleSet.Default` sets it. */
export const CRITICAL_MULTIPLIER = 2;
/** What a permanent condition is worth in rounds, as `ActionScorer.PermanentConditionRounds` prices it. */
export const PERMANENT_CONDITION_ROUNDS = 3;
/** The energy a creature gains at each upkeep, as `RuleSet.Default` sets it. */
export const ENERGY_PER_ROUND = 2;

const HARMFUL = new Set(['Damage', 'Bleed', 'Stun', 'InitiativeDebuff', 'DefenseDebuff', 'EnergyDrain']);
const FRIENDLY = new Set(['Ally', 'Self']);
const number = value => (typeof value === 'number' && Number.isFinite(value) ? value : 0);
const list = value => (Array.isArray(value) ? value.filter(item => item && typeof item === 'object') : []);

/** One effect in damage-equivalents, unsigned and for a single target. */
export function effectValue(effect, weights, critFactor = 1) {
  const w = name => number(weights?.[name]);
  const amount = number(effect.amount);
  const perRound = number(effect.amountPerRound);
  const rounds = effect.permanent ? PERMANENT_CONDITION_ROUNDS : number(effect.durationRounds);
  switch (effect.kind) {
    case 'Damage': return w('damage') * amount * critFactor;
    case 'Heal': return w('heal') * amount * critFactor;
    case 'EnergyGain': case 'EnergyDrain': return w('energy') * amount;
    case 'Bleed': return w('bleed') * perRound * rounds;
    case 'Regeneration': return w('heal') * perRound * rounds;
    case 'EnergyRegeneration': return w('energy') * perRound * rounds;
    case 'Stun': return w('stun') * rounds;
    case 'DefenseBuff': case 'DefenseDebuff': return w('defense') * amount * rounds;
    case 'InitiativeBuff': case 'InitiativeDebuff': return w('initiative') * amount * rounds;
    default: return 0;
  }
}

export function maxTargets(document) {
  const count = Math.trunc(number(document?.targeting?.maxTargets));
  return Math.max(1, count || 1);
}

/**
 * One cast: the target half for every target allowed, the caster half once. A harmful kind aimed at a friend or
 * at the caster is a price and is subtracted; the critical roll reaches the targets and not the caster.
 */
export function castValue(document, weights) {
  const critFactor = 1 + number(document?.criticalChance) * (CRITICAL_MULTIPLIER - 1);
  const friendly = FRIENDLY.has(String(document?.targeting?.origin));
  const target = list(document?.effects).reduce((total, effect) =>
    total + (friendly && HARMFUL.has(effect.kind) ? -1 : 1) * effectValue(effect, weights, critFactor), 0);
  const caster = list(document?.casterEffects).reduce((total, effect) =>
    total + (HARMFUL.has(effect.kind) ? -1 : 1) * effectValue(effect, weights), 0);
  return target * maxTargets(document) + caster;
}

/** How many rounds of income one cast takes to pay for; a creature acts once a round however cheap the spell. */
export function roundsPerCast(document) {
  return Math.max(1, Math.max(0, Math.trunc(number(document?.energyCost))) / ENERGY_PER_ROUND);
}

/** The value a round, which is what two spells at different prices are compared on. */
export function roundValue(document, weights) {
  return castValue(document, weights) / roundsPerCast(document);
}

/** An attack is only compared with attacks, a spell that deals no damage only with others that deal none. */
export function isAttack(document) {
  return list(document?.effects).some(effect => effect.kind === 'Damage');
}

function median(values) {
  const sorted = values.toSorted((a, b) => a - b);
  const middle = Math.floor(sorted.length / 2);
  return sorted.length % 2 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
}

/**
 * Where a spell sits in its group: every other spell of the same tier (its shallowest package, 0 for the starting
 * kit and for a spell nothing teaches) on the same side of the attack line. `level` is null when the spell is not
 * in `levels`, which leaves it with no group to be read against.
 */
export function standing(spell, spells, levels, weights) {
  const level = levels.get(spell.id);
  const attack = isAttack(spell.document);
  const value = roundValue(spell.document, weights);
  if (level === undefined) return { value, cast: castValue(spell.document, weights), attack, level: null, peers: 0 };
  const peers = spells.filter(other => levels.get(other.id) === level && isAttack(other.document) === attack)
    .map(other => roundValue(other.document, weights));
  const low = Math.min(...peers), high = Math.max(...peers);
  return {
    value, cast: castValue(spell.document, weights), attack, level, peers: peers.length,
    low, high, median: median(peers),
    place: peers.length < 2 ? 'alone' : value <= low ? 'lowest' : value >= high ? 'highest' : 'within',
  };
}
