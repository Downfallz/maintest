// The codex reads authored packages. Class trees never decide the progression shown here.
export const active = items => (items ?? []).filter(item => item.enabled !== false && !item.problem);
export const resolve = (id, catalogue) => catalogue?.aliases?.[id] ?? id;
export const named = (items, id, catalogue) => (items ?? []).find(item => item.id === resolve(id, catalogue));

// Every package a pick has to own first: the all-of list, then the any-of one a capstone is opened by (ADR 0101).
const listOf = value => (Array.isArray(value) ? value : []);
const required = item => [...listOf(item.document?.prerequisites), ...listOf(item.document?.anyOf)];

export function packageFamilies(catalogue) {
  const packages = active(catalogue?.tiers);
  const roots = packages.filter(item => !required(item).length);
  return roots.map((root, index) => ({ root, tone: index % 3, packages: descendants(root.id, packages, catalogue) }));
}

// A capstone is in every family one of its any-of packages is: the list is a family's closers, so in the
// authored content that is one family, and naming two only puts it in both.
function descendants(id, packages, catalogue) {
  const found = new Set([id]);
  let changed = true;
  while (changed) {
    changed = false;
    for (const item of packages) {
      if (!found.has(item.id) && required(item).some(parent => found.has(resolve(parent, catalogue)))) {
        found.add(item.id);
        changed = true;
      }
    }
  }
  return packages.filter(item => found.has(item.id)).sort((a, b) => a.document.level - b.document.level || a.name.localeCompare(b.name));
}

const parentOf = (id, catalogue) => named(catalogue.tiers, id, catalogue) ?? { id, name: id, missing: true };

/** Every package this one is opened from, either list: what a family and "what comes next" follow. */
export function packageParents(item, catalogue) {
  return required(item).map(id => parentOf(id, catalogue));
}

/** The two lists apart: every one of `allOf` has to be owned, and any one of `anyOf` is enough. */
export function packageRequirements(item, catalogue) {
  return {
    allOf: listOf(item.document?.prerequisites).map(id => parentOf(id, catalogue)),
    anyOf: listOf(item.document?.anyOf).map(id => parentOf(id, catalogue)),
  };
}

/** What a package needs, in one line, or the empty string for an opener. */
export function requirementText(item, catalogue) {
  const { allOf, anyOf } = packageRequirements(item, catalogue);
  const all = allOf.map(parent => parent.name).join(' + ');
  const any = anyOf.map(parent => parent.name).join(' / ');
  if (all && any) return `Requires ${all} and one of ${any}`;
  if (any) return `Requires one of ${any}`;
  return all ? `Requires ${all}` : '';
}

export function packagesTeaching(spell, catalogue) {
  return active(catalogue.tiers).filter(item => (item.document.spells ?? []).some(id => resolve(id, catalogue) === spell.id));
}

/**
 * Where a spell comes from while browsing: every enabled package that teaches it, lowest tier first, and
 * whether an enabled creature already starts with it. A spell can be both, and one with neither is content
 * nobody can play.
 */
export function spellOrigins(spell, catalogue) {
  const packages = packagesTeaching(spell, catalogue)
    .map(pack => ({ id: pack.id, name: pack.name, level: pack.document.level }))
    .sort((a, b) => a.level - b.level || a.name.localeCompare(b.name));
  const starting = active(catalogue?.creatures).some(creature =>
    (creature.document?.startingSpellIds ?? []).some(id => resolve(id, catalogue) === spell.id));
  return { packages, starting };
}

export function originText({ packages, starting }) {
  const parts = packages.map(pack => `Tier ${pack.level} · ${pack.name}`);
  if (starting) parts.push('Starting kit');
  return parts.join(' / ') || 'Not taught by any package';
}

/**
 * The tier a spell is read in: its shallowest package, 0 when a creature starts with it. A spell with neither has
 * no tier and is left out, the same grouping `check-knobs` reads (ADR 0058).
 */
export function spellLevels(catalogue) {
  const levels = new Map();
  for (const spell of active(catalogue?.spells)) {
    const { packages, starting } = spellOrigins(spell, catalogue);
    if (starting) levels.set(spell.id, 0);
    else if (packages.length) levels.set(spell.id, packages[0].level);
  }
  return levels;
}

/** The tier filter: `All`, a level, or `Starting` for the kit a creature begins with. */
export function spellInTier(item, tier, catalogue) {
  if (tier === undefined || tier === 'All') return true;
  const { packages, starting } = spellOrigins(item, catalogue);
  return tier === 'Starting' ? starting : packages.some(pack => pack.level === Number(tier));
}

export function spellMatches(item, query, type, catalogue) {
  if (type !== 'All' && item.document?.spellType !== type) return false;
  const search = [item.name, item.id, item.document?.creatureClass,
    ...[...(item.document?.effects ?? []), ...(item.document?.casterEffects ?? [])].map(effect => effect.kind),
    ...packagesTeaching(item, catalogue).flatMap(pack => [pack.name, `tier ${pack.document.level}`])].join(' ').toLowerCase();
  return query.toLowerCase().trim().split(/\s+/).every(word => search.includes(word));
}

export function effectText(effect) {
  const amount = effect.amount ?? effect.amountPerRound ?? 0;
  const rounds = effect.durationRounds ?? 1;
  const unit = rounds === 1 ? 'round' : 'rounds';
  const duration = effect.permanent ? 'permanently' : `for ${rounds} ${unit}`;
  switch (effect.kind) {
    case 'Damage': return `Deal ${amount} damage`;
    case 'Heal': return `Restore ${amount} HP`;
    case 'EnergyGain': return `Gain ${amount} energy`;
    case 'EnergyDrain': return `Drain ${amount} energy`;
    case 'Bleed': return `Deal ${amount} damage each round ${duration}`;
    case 'Regeneration': return `Restore ${amount} HP each round ${duration}`;
    case 'EnergyRegeneration': return `Gain ${amount} energy each round ${duration}`;
    case 'Stun': return `Stun ${duration}`;
    case 'DefenseBuff': return `Gain ${amount} defense ${duration}`;
    case 'DamageBuff': return `Deal ${amount} more damage on every hit ${duration}`;
    case 'DefenseDebuff': return `Reduce defense by ${amount} ${duration}`;
    case 'InitiativeBuff': return `Gain ${amount} initiative ${duration}`;
    case 'InitiativeDebuff': return `Reduce initiative by ${amount} ${duration}`;
    default: return effect.kind ?? 'Unknown effect';
  }
}

/** A chance as a percentage, to the hundredth at most: 0.283 reads 28.3%, never 28.299999999999997%. */
export function percentText(chance) {
  return `${Number(((Number(chance) || 0) * 100).toFixed(2))}%`;
}

/**
 * A spell's critical chance as its card says it, or nothing when it has none. A bonus, so it is signed: the
 * spell adds it to the critical chance of whoever casts it, and a card reading "28% crit" would claim the
 * whole chance.
 */
export function criticalText(document) {
  const chance = Number(document?.criticalChance) || 0;
  return chance > 0 ? `+${percentText(chance)} crit` : '';
}

export function targetText(target) {
  if (!target) return 'See targeting in editor';
  if (target.origin === 'Self') return 'Self';
  const names = { Enemy: ['enemy', 'enemies'], Ally: ['ally', 'allies'], Any: ['creature', 'creatures'] };
  const [one, many] = names[target.origin] ?? ['target', 'targets'];
  if (target.scope === 'SingleTarget') return `1 ${one}`;
  return target.maxTargets ? `Up to ${target.maxTargets} ${many}` : `Multiple ${many}`;
}
