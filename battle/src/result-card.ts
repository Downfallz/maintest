import { chipElement, type EffectChip } from './condition-icons';

// One action's result, drawn the way the journal's summary draws a cast (battle/src/history.ts): whose cast in the
// side's colour, the spell after an arrow, a CRIT or FIZZLE badge, then what came of it, as the field's chips
// gathered by the creature they landed on. A turnover or a replay interlude is its title and its lines.
// The last result sheet and the match replay draw the same card, from `readingCard` and `replayCard`.
export interface ResultCard {
  kind: 'incoming' | 'own' | 'summary';
  title: string; detail: string; round?: number | null;
  caster?: string; spell?: string; status?: string; reason?: string; dropped?: string[];
  groups?: { target: string; chips: EffectChip[] }[];
  lines?: { text: string; tone?: string }[];
}

const KIND_LABELS: Record<ResultCard['kind'], string> = { incoming: "Opponent's action", own: 'Your cast', summary: 'Round summary' };
const STATUS_BADGES: Record<string, string> = { Critical: 'Crit', Fizzled: 'Fizzle' };

function node(tag: string, className: string, text = '') {
  const one = document.createElement(tag);
  one.className = className;
  if (text) one.textContent = text;
  return one;
}

// A turnover's lines, or its one-line detail when it has none, or a word that nothing changed.
function summaryBody(card: ResultCard) {
  let lines = card.lines ?? [];
  if (!lines.length && card.detail) lines = [{ text: card.detail, tone: '' }];
  if (!lines.length) return node('p', 'result-card-empty', 'Nothing changed.');
  const list = node('ul', 'result-card-lines');
  for (const line of lines) {
    const item = node('li', '', line.text);
    if (line.tone) item.dataset.tone = line.tone;
    list.append(item);
  }
  return list;
}

// What a cast did: its fizzle, or its chips by the creature they landed on, or no effect.
function castBody(card: ResultCard, effects: Parameters<typeof chipElement>[1]) {
  if (card.status === 'Fizzled') return node('p', 'result-card-reason', card.reason ? `Fizzled: ${card.reason}` : 'Fizzled');
  if (!card.groups?.length) return node('p', 'result-card-empty', 'No effect');
  const groups = node('ul', 'result-card-groups');
  for (const group of card.groups) {
    const item = node('li', 'result-card-group');
    const chips = node('span', 'result-card-chips');
    chips.append(...group.chips.map(chip => chipElement(chip, effects)));
    item.append(node('span', 'result-card-target', group.target), chips);
    groups.append(item);
  }
  return groups;
}

function castHead(card: ResultCard) {
  const head = node('h3', 'result-card-head');
  head.append(node('span', 'result-card-who', card.caster ?? ''), node('span', 'result-card-what', card.spell ?? ''));
  const badge = card.status ? STATUS_BADGES[card.status] : undefined;
  if (badge) head.append(node('span', 'result-card-status', badge));
  return head;
}

export function resultCardElement(card: ResultCard, effects: Parameters<typeof chipElement>[1] = []) {
  const article = node('article', 'result-card');
  article.dataset.kind = card.kind;
  if (card.status) article.dataset.status = card.status;
  const kind = card.kind === 'summary' ? card.title : KIND_LABELS[card.kind];
  const round = Number.isInteger(card.round) ? `Round ${card.round}` : '';
  article.append(node('p', 'result-card-kind', [kind, round].filter(Boolean).join(' · ')));
  if (card.kind === 'summary') {
    article.append(summaryBody(card));
    return article;
  }
  article.append(castHead(card), castBody(card, effects));
  for (const text of card.dropped ?? []) article.append(node('p', 'result-card-dropped', `Dropped: ${text}`));
  return article;
}
