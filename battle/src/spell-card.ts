import { spellPortrait } from './spell-portrait';
import { conditionKey, iconElement } from './condition-icons';
import type { SpellCatalogue } from './combat';

type Card = NonNullable<SpellCatalogue['cards']>[number];
type Cue = { kind: string; glyph?: string; harmful: boolean };
/** A crit chance as a chip shows it: the chance, and what it holds for (a tempo, a d20 face, this turn). */
// `idle` is a chance that does not hold this turn: at Instinct, the chance at Precision, drawn grey (UX12 P8).
export type CritChip = { value: string; hint?: string; face?: string; idle?: boolean } | null;
export interface SpellCardOptions {
  compact?: boolean;
  // Inside a button, where only phrasing content belongs: the same card, built of spans.
  inline?: boolean;
  // What the host says this cast costs and its crit chance now, over the catalogue's own.
  cost?: string | number | null;
  crit?: CritChip;
  // A face in a column of the spellbook (docs/art/journeys/windows.md): the chips on their own row under the
  // targeting, each effect line as its icon and the host's numbers, the words kept for a screen reader, no crit
  // note. `portraitSizes` is the widths the screen draws the portrait at.
  brief?: boolean;
  portraitSizes?: string;
}

type Make = (block: string, text?: string, cls?: string) => HTMLElement;
const maker = (inline: boolean): Make => (block, text = '', cls = '') => {
  const el = document.createElement(inline ? 'span' : block);
  if (text) el.textContent = text;
  if (cls) el.className = cls;
  return el;
};

// The cost and crit chips: what the host says this cast costs and its chance now, over the catalogue's own.
function chipsOf(make: Make, card: Card | undefined, { inline = false, brief = false, cost, crit }: SpellCardOptions) {
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
    const chip = make('span', '', `spell-chip spell-chip-crit${chance.idle ? ' is-idle' : ''}`);
    chip.title = chance.idle ? 'Crit chance at Precision · Instinct cannot crit' : card?.criticalNote || 'Crit chance';
    chip.append(make('span', '✦', 'spell-crit-mark'), document.createTextNode(chance.value), make('span', chance.idle ? ' crit at Precision' : ' crit', 'spell-card-sr'));
    if (chance.hint && !inline) chip.append(make('small', chance.hint));
    else if (brief && chance.face) chip.append(make('small', chance.face));
    chips.append(chip);
  }
  return { chips, chance };
}

// Each effect line with the icon of the stat it moves. On a brief face, the host's numbers beside the icon of a
// stat, the words kept for a screen reader; a line without either keeps its words in sight.
function effectList(make: Make, card: Card, effects: Cue[], brief: boolean) {
  const lines = [...(card.effects ?? []).map(text => ({ text, caster: false })), ...(card.casterEffects ?? []).map(text => ({ text, caster: true }))];
  const list = make('ul', '', 'spell-card-effects');
  lines.forEach((line, index) => {
    const mark = card.marks?.[index];
    const item = make('li', '', 'spell-card-effect'); if (line.caster) item.dataset.caster = 'true';
    const key = mark ? conditionKey(mark.kind, effects) : 'other';
    item.dataset.cue = key; item.append(iconElement(key));
    if (brief && mark?.figures && key !== 'other') {
      const figures = make('span', mark.figures, 'spell-card-figures'); figures.setAttribute('aria-hidden', 'true');
      item.append(figures, make('span', line.text, 'spell-card-sr'));
    } else item.append(make('span', line.text));
    list.append(item);
  });
  return lines.length ? list : null;
}

// One spell as a card, everywhere a spell is shown in full: the mutation tree, a mutation's preview, the spell
// choice and the creature inspection to come. Its words are the host's, line for line; each effect line carries the
// icon of the stat it moves, from the kind the host marks it with (CardMark) and the catalogue's cue for that kind,
// so the page never reads the words to draw it. Cost and crit chance sit in the head as chips.
export function spellCard(card: Card | undefined, id: string, effects: Cue[] = [], options: SpellCardOptions = {}) {
  const { compact = false, inline = false, brief = false, portraitSizes } = options;
  const make = maker(inline);
  const article = make('article', '', `spell-card${compact ? ' compact' : ''}${inline ? ' inline' : ''}${brief ? ' brief' : ''}`);
  // Inside a button, the button names the spell: one element per spell answers [data-spell].
  if (!inline) article.dataset.spell = id;
  const head = make('header', '', 'spell-card-head');
  head.append(make('strong', card?.name ?? id, 'spell-card-name'));
  const { chips, chance } = chipsOf(make, card, options);
  // A compact card sits in a docked sheet whose foot must stay on a 320 by 568 screen: it keeps its text only,
  // unless the screen names a width for its portrait (the Tempo sheet's 24 px, UX12 P7).
  const portrait = compact && !portraitSizes ? null : spellPortrait(card?.visual?.portrait, portraitSizes ?? 40);
  if (portrait) article.append(portrait);
  if (!brief) head.append(chips);
  article.append(head);
  // Beside the name, the chips would leave it about 60 px of a column at 320. A spell the catalogue does not hold
  // keeps them too: its cost is the host's guide's.
  const chipsRow = () => { if (brief && chips.childNodes.length) article.append(chips); };
  if (!card) { chipsRow(); article.append(make('p', 'Spell details unavailable.', 'spell-card-line')); return article; }
  if (card.targeting) article.append(make('p', card.targeting, 'spell-card-target'));
  chipsRow();
  const list = effectList(make, card, effects, brief);
  if (list) article.append(list);
  if (!compact && !brief && card.criticalNote && chance) article.append(make('p', card.criticalNote, 'spell-card-note'));
  return article;
}
