import { badges, chipText, laneOf } from './board.js';
import { cardCost, cardDetails, cardStats, cardsById } from './card.js';
import { classColour, passiveOf, tagsOf, talentPalette } from './mat.js';

const capital = value => value.charAt(0).toUpperCase() + value.slice(1);
const STATUS_KEYS = { stunned: 'stun', 'immune to stun': 'immune' };
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
  // Attacks first, as the spell choice orders them: the host's order of spell types, the catalogue's after it.
  const types = [...new Set([...(catalogue?.spellTypes ?? []), ...(catalogue?.cards ?? []).map(card => card.type).filter(Boolean)])];
  const rank = id => { const at = types.indexOf(cards.get(id)?.type); return at < 0 ? Infinity : at; };
  const palette = talentPalette(catalogue, cards);
  const spells = [...new Set(creature.knownSpells ?? [])].sort((a, b) => rank(a) - rank(b)).map(id => {
    const card = cards.get(id);
    return { id, name: card?.name ?? id, cost: cardCost(card), type: card?.type ?? '',
      colour: card?.creatureClass ? classColour(card.creatureClass, palette) : null,
      critical: cardStats(card).map(stat => `${stat.value} crit · ${stat.hint} · catalogue`).join(' / '),
      crit: cardStats(card).map(stat => ({ value: stat.value, hint: stat.hint }))[0] ?? null,
      details: cardDetails(card), criticalNote: card?.criticalNote ?? '', missing: !card,
      sources: mutations.filter(pack => packs.get(pack.id)?.spells?.includes(id)).map(pack => pack.name) };
  });
  // Each condition as its card shows it: the engine's words for it, how long it lasts and who put it there. `kind`
  // is the host's effect kind, which the page turns into an icon through the catalogue's cue for it.
  const conditionCards = (creature.conditions ?? []).map(condition => {
    const lane = laneOf(condition), source = condition.source;
    const caster = source?.caster == null ? '' : roster.find(one => String(one.id) === String(source.caster))?.label ?? `Creature ${source.caster}`;
    const spell = source?.spell == null ? '' : cards.get(source.spell)?.name ?? source.spell;
    return { kind: condition.effect?.kind ?? null, name: chipText(condition) || 'Condition', duration: durationText(lane),
      source: [caster ? `from ${caster}` : '', spell].filter(Boolean).join(' · ') };
  });
  const conditions = conditionCards.map(one => [one.name, one.duration, one.source].filter(Boolean).join(' · '));
  // What stands on the creature besides its conditions: defeat, a stun or its immunity, and its passives.
  const statuses = [...(creature.isAlive === false ? [{ key: 'defeated', text: 'Defeated' }] : []),
    ...badges(creature, []).map(badge => ({ key: STATUS_KEYS[badge] ?? (/^damage/.test(badge) ? 'damage-gain' : /^energy/.test(badge) ? 'energy-gain' : /defense/.test(badge) ? 'defense-loss' : 'other'), text: capital(badge) }))];
  const status = statuses.map(one => one.text);
  const value = one => Number.isFinite(one) ? one : null;
  return { id: String(creature.id), name: creature.label ?? creature.name ?? `Creature ${creature.id}`,
    side: creature.enemy ? 'OPPONENT' : 'YOUR TEAM', enemy: creature.enemy === true, form: creature.form ?? null, artworkFile: creature.evolution?.artworkFile ?? null, description: creature.description ?? '',
    stats: `${number(creature.health)}/${number(creature.maxHealth)} HP · ${number(creature.energy)} energy · ${number(creature.totalDefense)} defense · ${number(creature.currentInitiative)} initiative`,
    sheet: { health: value(creature.health), maxHealth: value(creature.maxHealth), energy: value(creature.energy), defense: value(creature.totalDefense), initiative: value(creature.currentInitiative) },
    effects: [...status, ...conditions].join(' / ') || (Array.isArray(creature.conditions) ? 'No ongoing effects' : 'Conditions not recorded'),
    statuses, conditions: conditionCards, conditionsRecorded: Array.isArray(creature.conditions),
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
