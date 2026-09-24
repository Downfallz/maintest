// The codex reads authored packages. Class trees never decide the progression shown here.
export const active = items => (items ?? []).filter(item => item.enabled !== false && !item.problem);
export const resolve = (id, catalogue) => catalogue?.aliases?.[id] ?? id;
export const named = (items, id, catalogue) => (items ?? []).find(item => item.id === resolve(id, catalogue));

export function packageFamilies(catalogue) {
  const packages = active(catalogue?.tiers);
  const roots = packages.filter(item => !(item.document?.prerequisites ?? []).length);
  return roots.map((root, index) => ({ root, tone: index % 3, packages: descendants(root.id, packages, catalogue) }));
}

function descendants(id, packages, catalogue) {
  const found = new Set([id]);
  let changed = true;
  while (changed) {
    changed = false;
    for (const item of packages) {
      if (!found.has(item.id) && (item.document?.prerequisites ?? []).some(parent => found.has(resolve(parent, catalogue)))) {
        found.add(item.id);
        changed = true;
      }
    }
  }
  return packages.filter(item => found.has(item.id)).sort((a, b) => a.document.level - b.document.level || a.name.localeCompare(b.name));
}

export function packageParents(item, catalogue) {
  return (item.document?.prerequisites ?? []).map(id => named(catalogue.tiers, id, catalogue) ?? { id, name: id, missing: true });
}

export function packagesTeaching(spell, catalogue) {
  return active(catalogue.tiers).filter(item => (item.document.spells ?? []).some(id => resolve(id, catalogue) === spell.id));
}

export function spellMatches(item, query, type, catalogue) {
  if (type !== 'All' && item.document?.spellType !== type) return false;
  const search = [item.name, item.id, item.document?.creatureClass,
    ...[...(item.document?.effects ?? []), ...(item.document?.casterEffects ?? [])].map(effect => effect.kind),
    ...packagesTeaching(item, catalogue).map(pack => pack.name)].join(' ').toLowerCase();
  return query.toLowerCase().trim().split(/\s+/).every(word => search.includes(word));
}

export function effectText(effect) {
  const amount = effect.amount ?? effect.amountPerRound ?? 0;
  const duration = effect.permanent ? 'permanently' : `for ${effect.durationRounds ?? 1} round${(effect.durationRounds ?? 1) === 1 ? '' : 's'}`;
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
    case 'DefenseDebuff': return `Reduce defense by ${amount} ${duration}`;
    case 'InitiativeBuff': return `Gain ${amount} initiative ${duration}`;
    case 'InitiativeDebuff': return `Reduce initiative by ${amount} ${duration}`;
    default: return effect.kind ?? 'Unknown effect';
  }
}

export function targetText(target) {
  if (!target) return 'See targeting in editor';
  if (target.origin === 'Self') return 'Self';
  const names = { Enemy: ['enemy', 'enemies'], Ally: ['ally', 'allies'], Any: ['creature', 'creatures'] };
  const [one, many] = names[target.origin] ?? ['target', 'targets'];
  if (target.scope === 'SingleTarget') return `1 ${one}`;
  return target.maxTargets ? `Up to ${target.maxTargets} ${many}` : `Multiple ${many}`;
}
