import { active, resolve } from './catalogue.js';

// These groups describe authored effects, not their value in a particular combat situation.
export const EFFECT_GROUPS = [
  { id: 'damage', name: 'Damage', kinds: ['Damage'] },
  { id: 'bleed', name: 'Bleed', kinds: ['Bleed'] },
  { id: 'healing', name: 'Healing', kinds: ['Heal', 'Regeneration'] },
  { id: 'defense', name: 'Defense', kinds: ['DefenseBuff', 'DefenseDebuff'] },
  { id: 'initiative', name: 'Initiative', kinds: ['InitiativeBuff', 'InitiativeDebuff'] },
  { id: 'energy', name: 'Energy', kinds: ['EnergyGain', 'EnergyDrain', 'EnergyRegeneration'] },
  { id: 'control', name: 'Control', kinds: ['Stun'] },
];

export function energyCost(item) {
  const cost = item.document?.energyCost;
  return Number.isInteger(cost) && cost >= 0 ? cost : null;
}

export function effectGroups(item, side = 'target') {
  const effects = side === 'caster' ? item.document?.casterEffects : item.document?.effects;
  const groups = new Set();
  for (const effect of effects ?? []) {
    groups.add(EFFECT_GROUPS.find(group => group.kinds.includes(effect.kind))?.id ?? 'other');
  }
  return groups;
}

function startingSpells(catalogue) {
  return active(catalogue.creatures).flatMap(item => item.document?.startingSpellIds ?? []);
}

// Packages form a union of taught spells. This is a content comparison, not a legal build or an unlock plan.
export function scopedSpells(catalogue, ui = {}) {
  const spells = [...new Map(active(catalogue.spells).map(item => [item.id, item])).values()];
  if (!ui.scope || ui.scope === 'all') return spells;
  let ids = [];
  if (ui.scope === 'starting' || ui.includeStarting) ids.push(...startingSpells(catalogue));
  if (ui.scope === 'packages') {
    ids.push(...active(catalogue.tiers).filter(item => (ui.packages ?? []).includes(item.id))
      .flatMap(item => item.document?.spells ?? []));
  }
  const selected = new Set(ids.map(id => resolve(id, catalogue)));
  return spells.filter(item => selected.has(item.id));
}

export function strategyOverview(catalogue, ui = {}) {
  const scoped = scopedSpells(catalogue, ui);
  const counts = new Map();
  for (const item of scoped) {
    const cost = energyCost(item);
    counts.set(cost, (counts.get(cost) ?? 0) + 1);
  }
  const costs = [...counts].sort(([a], [b]) => (a ?? Infinity) - (b ?? Infinity));
  const atCost = scoped.filter(item => ui.cost === undefined || ui.cost === 'all' || energyCost(item) === ui.cost);
  const facets = EFFECT_GROUPS.map(group => ({ ...group, count: atCost.filter(item => effectGroups(item, ui.side).has(group.id)).length }));
  const other = atCost.filter(item => effectGroups(item, ui.side).has('other')).length;
  if (other) facets.push({ id: 'other', name: 'Other effects', count: other });
  const matches = atCost.filter(item => !ui.effect || effectGroups(item, ui.side).has(ui.effect))
    .sort((a, b) => (energyCost(a) ?? Infinity) - (energyCost(b) ?? Infinity) || a.name.localeCompare(b.name));
  return { total: scoped.length, costs, atCost: atCost.length, facets, matches };
}

// Keep duration, permanence and caster effects explicit; never add unlike effects into one power score.
export function compactEffect(effect) {
  const names = { Damage: 'Damage', Heal: 'Heal', EnergyGain: 'Energy +', EnergyDrain: 'Energy −',
    DefenseBuff: 'Defense +', DefenseDebuff: 'Defense −', InitiativeBuff: 'Initiative +', InitiativeDebuff: 'Initiative −' };
  if (effect.kind === 'Stun') return `Stun ${effect.durationRounds ?? 1}r`;
  const recurring = { Bleed: 'Bleed', Regeneration: 'Heal', EnergyRegeneration: 'Energy +' };
  if (recurring[effect.kind]) return `${recurring[effect.kind]} ${effect.amountPerRound ?? '?'} / round · ${duration(effect)}`;
  if (!names[effect.kind]) return effect.kind ?? 'Unknown effect';
  const lasting = ['DefenseBuff', 'DefenseDebuff', 'InitiativeBuff', 'InitiativeDebuff'].includes(effect.kind);
  return `${names[effect.kind]} ${effect.amount ?? '?'}${lasting ? ` · ${duration(effect)}` : ''}`;
}

function duration(effect) {
  return effect.permanent ? 'permanent' : `${effect.durationRounds ?? 1}r`;
}
