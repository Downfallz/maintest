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
  const one = document.createElement(tag); one.className = className; if (text) one.textContent = text; return one;
}

export function resultCardElement(card: ResultCard, effects: Parameters<typeof chipElement>[1] = []) {
  const article = node('article', 'result-card');
  article.dataset.kind = card.kind;
  if (card.status) article.dataset.status = card.status;
  article.append(node('p', 'result-card-kind', [card.kind === 'summary' ? card.title : KIND_LABELS[card.kind],
    Number.isInteger(card.round) ? `Round ${card.round}` : ''].filter(Boolean).join(' · ')));
  if (card.kind === 'summary') {
    const lines = card.lines?.length ? card.lines : card.detail ? [{ text: card.detail, tone: '' }] : [];
    if (lines.length) {
      const list = node('ul', 'result-card-lines');
      for (const line of lines) { const item = node('li', '', line.text); if (line.tone) item.dataset.tone = line.tone; list.append(item); }
      article.append(list);
    } else article.append(node('p', 'result-card-empty', 'Nothing changed.'));
    return article;
  }
  const head = node('h3', 'result-card-head');
  head.append(node('span', 'result-card-who', card.caster ?? ''), node('span', 'result-card-what', card.spell ?? ''));
  if (card.status && STATUS_BADGES[card.status]) head.append(node('span', 'result-card-status', STATUS_BADGES[card.status]));
  article.append(head);
  if (card.status === 'Fizzled') article.append(node('p', 'result-card-reason', card.reason ? `Fizzled: ${card.reason}` : 'Fizzled'));
  else if (card.groups?.length) {
    const groups = node('ul', 'result-card-groups');
    for (const group of card.groups) {
      const item = node('li', 'result-card-group');
      const chips = node('span', 'result-card-chips'); chips.append(...group.chips.map(chip => chipElement(chip, effects)));
      item.append(node('span', 'result-card-target', group.target), chips);
      groups.append(item);
    }
    article.append(groups);
  } else article.append(node('p', 'result-card-empty', 'No effect'));
  for (const text of card.dropped ?? []) article.append(node('p', 'result-card-dropped', `Dropped: ${text}`));
  return article;
}
