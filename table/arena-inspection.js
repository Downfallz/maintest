import { badges, chipText, laneOf } from './board.js';
import { cardCost, cardDetails, cardStats, cardsById } from './card.js';
import { passiveOf, tagsOf } from './mat.js';

const capital = value => value.charAt(0).toUpperCase() + value.slice(1);
const number = value => Number.isFinite(value) ? String(value) : '—';

function durationText(lane) {
  if (lane === null) return 'permanent';
  if (lane === 'new') return 'new · first countdown pending';
  return lane === 1 ? '1 round' : `${lane} rounds`;
}

// Only the displayed public creature supplies ownership and known spells. Neither private planning
// choices nor the catalogue's ability to teach a spell can make it acquired/known here.
export function creatureInspection(creature, roster, catalogue) {
  if (!creature) return null;
  const cards = cardsById(catalogue), packs = new Map((catalogue?.packages ?? []).map(pack => [pack.id, pack]));
  const mutations = [...new Set(creature.acquiredTiers ?? [])].map(id => {
    const pack = packs.get(id);
    return { id, name: pack?.name ?? id, stage: pack?.level ?? null,
      initiative: Number.isFinite(pack?.initiativeBonus) ? pack.initiativeBonus : null,
      tags: tagsOf(pack), passives: passiveOf(pack), missing: !pack };
  });
  const spells = [...new Set(creature.knownSpells ?? [])].map(id => {
    const card = cards.get(id);
    return { id, name: card?.name ?? id, cost: cardCost(card), type: card?.type ?? '',
      critical: cardStats(card).map(stat => `${stat.value} crit · ${stat.hint} · catalogue`).join(' / '),
      details: cardDetails(card), criticalNote: card?.criticalNote ?? '', missing: !card,
      sources: mutations.filter(pack => packs.get(pack.id)?.spells?.includes(id)).map(pack => pack.name) };
  });
  const conditions = (creature.conditions ?? []).map(condition => {
    const lane = laneOf(condition), source = condition.source;
    const caster = source?.caster == null ? '' : roster.find(one => String(one.id) === String(source.caster))?.label ?? `Creature ${source.caster}`;
    const spell = source?.spell == null ? '' : cards.get(source.spell)?.name ?? source.spell;
    return [chipText(condition) || 'Condition', durationText(lane), caster ? `from ${caster}` : '', spell].filter(Boolean).join(' · ');
  });
  const status = [creature.isAlive === false ? 'Defeated' : '', ...badges(creature, []).map(capital)].filter(Boolean);
  return { id: String(creature.id), name: creature.label ?? creature.name ?? `Creature ${creature.id}`,
    side: creature.enemy ? 'OPPONENT' : 'YOUR TEAM', description: creature.description ?? '',
    stats: `${number(creature.health)}/${number(creature.maxHealth)} HP · ${number(creature.energy)} energy · ${number(creature.totalDefense)} defense · ${number(creature.currentInitiative)} initiative`,
    effects: [...status, ...conditions].join(' / ') || (Array.isArray(creature.conditions) ? 'No ongoing effects' : 'Conditions not recorded'),
    mutations, mutationsRecorded: Array.isArray(creature.acquiredTiers), spells, spellsRecorded: Array.isArray(creature.knownSpells) };
}

// Keep the field compact. The full durations and named sources remain in the inspection.
export function creatureCues(creature) {
  return [...badges(creature, []).map(capital), ...new Set((creature.conditions ?? []).map(chipText).filter(Boolean))].join(' · ');
}

// What a screen reader hears after the stats: the cues, then the tag or the target state, each said once and
// none left empty. The tag repeats a cue in capitals (STUNNED), and the target state says the stun again.
export function spokenCues(...parts) {
  const said = new Map();
  for (const part of parts.flatMap(one => String(one ?? '').split(/\. | · /))) {
    const words = part.trim();
    if (words && !said.has(words.toLowerCase())) said.set(words.toLowerCase(), words);
  }
  return [...said.values()].join('. ');
}
