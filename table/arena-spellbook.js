import { cardCost, cardDetails, cardStats, cardsById } from './card.js';
import { classColour, talentPalette } from './mat.js';
import { energyShortfall, guidedShortfall } from './hand.js';
import { chance } from './guidance.js';

function availability(id, model, card, guide) {
  if (!model.spells.includes(id)) return guide?.unavailableReason || 'Not offered this turn';
  if (!model.short.includes(id)) return 'Available';
  const short = guidedShortfall(guide) ?? energyShortfall(card?.cost, model.reference?.energy);
  const amount = short === null ? 'Not enough energy yet' : `${short} energy short`;
  return `Plan · ${amount} · may fizzle`;
}

function critical(card, guide) {
  const value = guide?.chosenSpeed === 'Quick' ? guide.quickCriticalChance : guide?.standardCriticalChance;
  if (Number.isFinite(value)) return value > 0 ? `${chance(value)} crit · current` : '';
  const stat = cardStats(card)[0];
  return stat ? `${stat.value} Precision crit · catalogue` : '';
}

// Grouping and accents explain the served cards; only the question's offers authorize selection.
// Keep known-spell order independent of availability so a poll cannot move a card under the pointer.
export function arenaSpellbook(model, catalogue) {
  const cards = cardsById(catalogue), palette = talentPalette(catalogue, cards), groups = new Map();
  for (const id of new Set([...model.known, ...model.spells])) {
    const card = cards.get(id), guide = model.reference?.guidance?.find(one => one.spell === id);
    const type = card?.type || 'Other spells';
    if (!groups.has(type)) groups.set(type, []);
    groups.get(type).push({ id, name: card?.name ?? id, type, family: card?.creatureClass ?? '',
      colour: card?.creatureClass ? classColour(card.creatureClass, palette) : null,
      cost: cardCost(card), critical: critical(card, guide), offered: model.spells.includes(id),
      availability: availability(id, model, card, guide),
      details: cardDetails(card), criticalNote: card?.criticalNote ?? '', missing: !card });
  }
  return [...groups].map(([type, spells]) => ({ type, spells }));
}
