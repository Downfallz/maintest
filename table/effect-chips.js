// One vocabulary for every effect shown on a creature: a spell's outcome at impact, an upkeep tick, a condition
// ending in the aftermath. A chip says what moved (its kind, which picks the icon and colour), by how much, and
// keeps the engine's own words as its text for screen readers, History and the specs.
const words = value => String(value ?? '').replace(/([a-z])([A-Z])/g, '$1 $2');
const signed = (sign, amount) => `${sign}${amount}`;

/** A recorded spell outcome (feed.js recapAction effect) as a chip. */
export function outcomeChip(effect) {
  const amount = Number.isInteger(effect?.amount) ? effect.amount : effect?.effect?.amount ?? effect?.effect?.amountPerRound;
  const text = effect?.text ?? '';
  switch (effect?.kind) {
    case 'DamageOutcome': return { kind: 'damage', value: signed('−', amount ?? 0), critical: effect.critical === true, text };
    case 'HealOutcome': return { kind: 'heal', value: signed('+', amount ?? 0), text };
    case 'EnergyOutcome': return amount < 0 ? { kind: 'drain', value: signed('−', -amount), text } : { kind: 'energy', value: signed('+', amount ?? 0), text };
    case 'EnergyDrainOutcome': return { kind: 'drain', value: signed('−', Math.abs(amount ?? 0)), text };
    case 'ConditionOutcome': {
      const condition = effect.effect?.kind ?? null;
      return { kind: 'condition', condition, value: [words(condition ?? 'Condition'), Number.isInteger(amount) ? amount : ''].filter(Boolean).join(' '), text };
    }
    default: return { kind: 'other', value: text || 'Effect', text };
  }
}

/** An upkeep tick (feed.js roundEnergyGrants / upkeepTicks row) as a chip. */
export function tickChip(row) {
  const text = `${row.sign}${row.amount} ${row.unit === 'energy' ? 'EN' : 'HP'}`;
  if (row.unit === 'energy') return { kind: row.sign === '−' ? 'drain' : 'energy', value: signed(row.sign, row.amount), text };
  return { kind: row.sign === '−' ? 'damage' : 'heal', value: signed(row.sign, row.amount), text };
}

/** A condition that ended in the aftermath, or a stun immunity gained, as a chip. */
export function endedChip(condition) {
  return { kind: 'ends', condition, value: `${words(condition ?? 'Condition')} ends`, text: `${words(condition ?? 'Condition')} ends` };
}
export function immuneChip() {
  return { kind: 'immune', value: 'Immune to stun', text: 'Immune to stun' };
}
