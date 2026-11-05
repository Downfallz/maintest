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
  // The card's own stat says which tempo crits and the face of the d20 the table rolls.
  const stat = cardStats(card)[0];
  return stat ? `${stat.value} crit · ${stat.hint} · catalogue` : '';
}

// The host's guide knows the cost of an offered spell the catalogue does not carry.
const cost = (card, guide) => Number.isInteger(guide?.cost) ? String(guide.cost) : cardCost(card);

const OTHER = 'Other spells';
const palettes = new WeakMap();
function paletteOf(catalogue, cards) {
  if (!catalogue) return talentPalette(catalogue, cards);
  if (!palettes.has(catalogue)) palettes.set(catalogue, talentPalette(catalogue, cards));
  return palettes.get(catalogue);
}
// Groups follow the catalogue's own order of types, so every creature reads them the same way; unknown last.
function typeRank(catalogue) {
  const order = [...new Set((catalogue?.cards ?? []).map(card => card.type).filter(Boolean))];
  return type => type === OTHER ? Infinity : order.indexOf(type);
}

// Grouping and accents explain the served cards; only the question's offers authorize selection.
// Keep known-spell order independent of availability so a poll cannot move a card under the pointer.
export function arenaSpellbook(model, catalogue) {
  const cards = cardsById(catalogue), palette = paletteOf(catalogue, cards), groups = new Map();
  for (const id of new Set([...model.known, ...model.spells])) {
    const card = cards.get(id), guide = model.reference?.guidance?.find(one => one.spell === id);
    const type = card?.type || OTHER;
    if (!groups.has(type)) groups.set(type, []);
    groups.get(type).push({ id, name: card?.name ?? id, type, creatureClass: card?.creatureClass ?? '',
      colour: card?.creatureClass ? classColour(card.creatureClass, palette) : null,
      cost: cost(card, guide), critical: critical(card, guide), offered: model.spells.includes(id),
      availability: availability(id, model, card, guide),
      details: cardDetails(card), criticalNote: card?.criticalNote ?? '', missing: !card });
  }
  const rank = typeRank(catalogue);
  return [...groups].sort(([a], [b]) => rank(a) - rank(b)).map(([type, spells]) => ({ type, spells }));
}
