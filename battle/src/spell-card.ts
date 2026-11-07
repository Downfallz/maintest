import { conditionKey, iconElement } from './condition-icons';
import type { SpellCatalogue } from './combat';

type Card = NonNullable<SpellCatalogue['cards']>[number];
type Cue = { kind: string; glyph?: string; harmful: boolean };
const node = (tag: string, text = '', cls = '') => { const el = document.createElement(tag); if (text) el.textContent = text; if (cls) el.className = cls; return el; };

// One spell as a card, everywhere a spell is shown in full: the mutation tree, a mutation's preview, and the
// creature inspection to come. Its words are the host's, line for line; each effect line carries the icon of
// the stat it moves, from the kind the host marks it with (CardMark) and the catalogue's cue for that kind,
// so the page never reads the words to draw it. Cost and crit chance sit in the head as chips.
export function spellCard(card: Card | undefined, id: string, effects: Cue[] = [], { compact = false } = {}) {
  const article = node('article', '', `spell-card${compact ? ' compact' : ''}`);
  article.dataset.spell = id;
  const head = node('header', '', 'spell-card-head');
  head.append(node('strong', card?.name ?? id, 'spell-card-name'));
  const chips = node('span', '', 'spell-card-chips');
  if (Number.isInteger(card?.cost)) {
    const cost = node('span', '', 'spell-chip spell-chip-cost'); cost.title = 'Energy cost';
    cost.append(iconElement('energy-gain'), document.createTextNode(String(card!.cost)), node('span', ' energy', 'spell-card-sr'));
    chips.append(cost);
  }
  if (Number.parseFloat(card?.critical ?? '') > 0) {
    const crit = node('span', '', 'spell-chip spell-chip-crit'); crit.title = card?.criticalNote ?? 'Crit chance';
    crit.append(node('span', '✦', 'spell-crit-mark'), document.createTextNode(card!.critical!), node('span', ' crit', 'spell-card-sr'));
    if (card?.criticalThreshold) crit.append(node('small', `d20 ${card.criticalThreshold}+`));
    chips.append(crit);
  }
  head.append(chips); article.append(head);
  if (!card) { article.append(node('p', 'Spell details unavailable.', 'spell-card-line')); return article; }
  if (card.targeting) article.append(node('p', card.targeting, 'spell-card-target'));
  const lines = [...(card.effects ?? []).map(text => ({ text, caster: false })), ...(card.casterEffects ?? []).map(text => ({ text, caster: true }))];
  const list = node('ul', '', 'spell-card-effects');
  lines.forEach((line, index) => {
    const mark = card.marks?.[index];
    const item = node('li', '', 'spell-card-effect'); if (line.caster) item.dataset.caster = 'true';
    const key = mark ? conditionKey(mark.kind, effects) : 'other';
    item.dataset.cue = key; item.append(iconElement(key), node('span', line.text));
    list.append(item);
  });
  if (lines.length) article.append(list);
  if (!compact && card.criticalNote && Number.parseFloat(card.critical ?? '') > 0) article.append(node('p', card.criticalNote, 'spell-card-note'));
  return article;
}
