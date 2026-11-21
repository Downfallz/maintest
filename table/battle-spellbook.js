import { cardCost, cardStats, cardsById } from './card.js';
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

// The crit chance as the card's chip shows it, and what it holds for: this turn's tempo, from the host's guide, or
// the card's own stat, which says which tempo crits and the face of the d20 the table rolls.
// `face` is what the chip keeps on a card in its column: the d20's face, which only the catalogue's chance has.
// At Instinct a spell never crits: the chip stays, `idle`, with the chance it would have at Precision, the host's
// or else the card's own (UX12 P8). The page computes no chance.
function critical(card, guide) {
  const stat = cardStats(card)[0];
  const catalogued = hint => stat ? { value: stat.value, hint, face: card.criticalThreshold ? `d20 ${card.criticalThreshold}+` : '' } : null;
  if (guide?.chosenSpeed === 'Quick') {
    const value = guide.standardCriticalChance;
    if (Number.isFinite(value)) return value > 0 ? { value: chance(value), hint: 'at Precision', idle: true } : null;
    const own = catalogued('at Precision · catalogue');
    return own ? { ...own, idle: true } : null;
  }
  const value = guide?.standardCriticalChance;
  if (Number.isFinite(value)) return value > 0 ? { value: chance(value), hint: 'at this tempo' } : null;
  return catalogued(`${stat?.hint} · catalogue`);
}

/** A spell's portrait in the spellbook: 24 px on a screen under 660 px tall in portrait, 40 px on a taller one
 * (docs/art/journeys/windows.md). Both widths are declared for the battle page (docs/art/page-pictures.json). */
export const SPELLBOOK_PORTRAIT_SIZES = '(orientation: portrait) and (max-height: 660px) 24px, 40px';

/** The chosen spell's card and its crit chance, read in full above the foot before it is declared (AU9). */
export function chosenSpell(model, catalogue, id) {
  if (!id) return null;
  const card = cardsById(catalogue).get(id), guide = model.reference?.guidance?.find(one => one.spell === id);
  return card ? { card, crit: critical(card, guide) } : null;
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
// Groups follow the order the host gives its spell types in (the domain's own: attacks first, then defences), then
// any other type in the catalogue's order, so every creature reads them the same way; unknown last. The page names
// no type itself.
function typeRank(catalogue) {
  const order = [...new Set([...(catalogue?.spellTypes ?? []), ...(catalogue?.cards ?? []).map(card => card.type).filter(Boolean)])];
  return type => type === OTHER ? Infinity : order.indexOf(type);
}

// Grouping and accents explain the served cards; only the question's offers authorize selection.
// Keep known-spell order independent of availability so a poll cannot move a card under the pointer.
export function battleSpellbook(model, catalogue) {
  const cards = cardsById(catalogue), palette = paletteOf(catalogue, cards), groups = new Map();
  for (const id of new Set([...model.known, ...model.spells])) {
    const card = cards.get(id), guide = model.reference?.guidance?.find(one => one.spell === id);
    const type = card?.type || OTHER;
    if (!groups.has(type)) groups.set(type, []);
    groups.get(type).push({ id, name: card?.name ?? id, type, creatureClass: card?.creatureClass ?? '',
      colour: card?.creatureClass ? classColour(card.creatureClass, palette) : null,
      cost: cost(card, guide), crit: critical(card, guide), offered: model.spells.includes(id),
      availability: availability(id, model, card, guide),
      missing: !card });
  }
  const rank = typeRank(catalogue);
  return [...groups].sort(([a], [b]) => rank(a) - rank(b)).map(([type, spells]) => ({ type, spells }));
}
