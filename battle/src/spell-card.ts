import { conditionKey, iconElement } from './condition-icons';
import type { SpellCatalogue } from './combat';

type Card = NonNullable<SpellCatalogue['cards']>[number];
type Cue = { kind: string; glyph?: string; harmful: boolean };
/** A crit chance as a chip shows it: the chance, and what it holds for (a tempo, a d20 face, this turn). */
export type CritChip = { value: string; hint?: string } | null;
export interface SpellCardOptions {
  compact?: boolean;
  // Inside a button, where only phrasing content belongs: the same card, built of spans.
  inline?: boolean;
  // What the host says this cast costs and its crit chance now, over the catalogue's own.
  cost?: string | number | null;
  crit?: CritChip;
}

// One spell as a card, everywhere a spell is shown in full: the mutation tree, a mutation's preview, the spell
// choice and the creature inspection to come. Its words are the host's, line for line; each effect line carries the
// icon of the stat it moves, from the kind the host marks it with (CardMark) and the catalogue's cue for that kind,
// so the page never reads the words to draw it. Cost and crit chance sit in the head as chips.
export function spellCard(card: Card | undefined, id: string, effects: Cue[] = [], { compact = false, inline = false, cost, crit }: SpellCardOptions = {}) {
  const make = (block: string, text = '', cls = '') => { const el = document.createElement(inline ? 'span' : block); if (text) el.textContent = text; if (cls) el.className = cls; return el; };
  const article = make('article', '', `spell-card${compact ? ' compact' : ''}${inline ? ' inline' : ''}`);
  // Inside a button, the button names the spell: one element per spell answers [data-spell].
  if (!inline) article.dataset.spell = id;
  const head = make('header', '', 'spell-card-head');
  head.append(make('strong', card?.name ?? id, 'spell-card-name'));
  const chips = make('span', '', 'spell-card-chips');
  const price = cost !== undefined ? cost : card?.cost;
  if (price !== null && price !== undefined && price !== '') {
    const chip = make('span', '', 'spell-chip spell-chip-cost'); chip.title = 'Energy cost';
    chip.append(iconElement('energy-gain'), make('span', String(price), 'spell-cost-value'), make('span', ' energy', 'spell-card-sr'));
    chips.append(chip);
  }
  const chance = crit !== undefined ? crit
    : Number.parseFloat(card?.critical ?? '') > 0 ? { value: card!.critical!, hint: card?.criticalThreshold ? `d20 ${card.criticalThreshold}+` : '' } : null;
  if (chance) {
    const chip = make('span', '', 'spell-chip spell-chip-crit'); chip.title = card?.criticalNote || 'Crit chance';
    chip.append(make('span', '✦', 'spell-crit-mark'), document.createTextNode(chance.value), make('span', ' crit', 'spell-card-sr'));
    if (chance.hint && !inline) chip.append(make('small', chance.hint));
    chips.append(chip);
  }
  head.append(chips); article.append(head);
  if (!card) { article.append(make('p', 'Spell details unavailable.', 'spell-card-line')); return article; }
  if (card.targeting) article.append(make('p', card.targeting, 'spell-card-target'));
  const lines = [...(card.effects ?? []).map(text => ({ text, caster: false })), ...(card.casterEffects ?? []).map(text => ({ text, caster: true }))];
  const list = make('ul', '', 'spell-card-effects');
  lines.forEach((line, index) => {
    const mark = card.marks?.[index];
    const item = make('li', '', 'spell-card-effect'); if (line.caster) item.dataset.caster = 'true';
    const key = mark ? conditionKey(mark.kind, effects) : 'other';
    item.dataset.cue = key; item.append(iconElement(key), make('span', line.text));
    list.append(item);
  });
  if (lines.length) article.append(list);
  if (!compact && card.criticalNote && chance) article.append(make('p', card.criticalNote, 'spell-card-note'));
  return article;
}
