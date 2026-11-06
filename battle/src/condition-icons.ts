import { chipText } from '../../table/board.js';

type Cue = { kind: string; glyph?: string; harmful: boolean };
type Condition = { effect?: { kind?: string } };
export interface ConditionIcon { key: string; title: string }

// The host marks each effect kind with the stat it moves and whether it is a loss (EffectCue); the page names
// no effect. An icon is that pair: a health loss reads as a drop, a gain as a heart, defense as a shield up or
// cracked, a stun as a star. The words, the engine's own, stay in the icon's title.
const STATS: Record<string, string> = { '♥': 'health', 'ϟ': 'energy', '◇': 'defense', '↟': 'initiative', '⊘': 'stun', '⚔': 'damage' };

export function conditionIcons(creature: { isStunned?: boolean; conditions?: Condition[] }, effects: Cue[] = []) {
  const icons = new Map<string, string[]>();
  const add = (key: string, title: string) => { icons.set(key, [...(icons.get(key) ?? []), title].filter(Boolean)); };
  for (const condition of creature.conditions ?? []) {
    const cue = effects.find(one => one.kind === condition.effect?.kind);
    const stat = STATS[cue?.glyph ?? ''];
    const key = !stat ? 'other' : stat === 'stun' ? 'stun' : `${stat}-${cue!.harmful ? 'loss' : 'gain'}`;
    add(key, chipText(condition));
  }
  if (creature.isStunned && !icons.has('stun')) add('stun', 'Stunned');
  return [...icons].map(([key, titles]) => ({ key, title: [...new Set(titles)].join(' · ') }));
}

const SVG = 'http://www.w3.org/2000/svg';
const PATHS: Record<string, string> = {
  'health-loss': 'M8 1.5C8 1.5 3.5 7 3.5 10a4.5 4.5 0 0 0 9 0C12.5 7 8 1.5 8 1.5Z',
  'health-gain': 'M8 14s-5.5-3.4-5.5-7.2A3 3 0 0 1 8 5a3 3 0 0 1 5.5 1.8C13.5 10.6 8 14 8 14Z',
  'defense-gain': 'M8 1.5 13.5 3.5V8c0 3.2-2.4 5.4-5.5 6.5C4.9 13.4 2.5 11.2 2.5 8V3.5Z M8 5 5.5 8H7v3h2V8h1.5Z',
  'defense-loss': 'M8 1.5 13.5 3.5V8c0 3.2-2.4 5.4-5.5 6.5C4.9 13.4 2.5 11.2 2.5 8V3.5Z M8.6 3.6 7 7.4l2 1.2-1.8 3.8',
  'stun': 'M8 1l1.6 4.1L14 5.5l-3.4 2.9L11.7 13 8 10.6 4.3 13l1.1-4.6L2 5.5l4.4-.4Z',
  'energy-gain': 'M9 1 3.5 9H8l-1 6 5.5-8H8Z',
  'energy-loss': 'M9 1 3.5 9H8l-1 6 5.5-8H8Z M2 14 14 2',
  'initiative-gain': 'M8 2l4.5 5H10v7H6V7H3.5Z',
  'initiative-loss': 'M8 14l4.5-5H10V2H6v7H3.5Z',
  'damage-gain': 'M13.5 2.5 6.5 9.5l1 1 7-7V2.5ZM4 10l2 2-1.5 1.5-2-2Z M10 12h4M12 10v4',
  'damage-loss': 'M13.5 2.5 6.5 9.5l1 1 7-7V2.5ZM4 10l2 2-1.5 1.5-2-2Z M10 12h4',
  'other': 'M8 3a5 5 0 1 0 0 10A5 5 0 0 0 8 3Z',
};
// Outlined marks read as a line drawing; the others are filled shapes.
const STROKED = new Set(['defense-loss', 'energy-loss', 'damage-gain', 'damage-loss']);

export function iconElement(key: string, title = '') {
  const svg = document.createElementNS(SVG, 'svg');
  svg.setAttribute('viewBox', '0 0 16 16'); svg.setAttribute('aria-hidden', 'true');
  svg.classList.add('cue-icon', `cue-${key}`);
  const path = document.createElementNS(SVG, 'path'); path.setAttribute('d', PATHS[key] ?? PATHS.other);
  if (STROKED.has(key)) path.classList.add('stroked'); else path.setAttribute('fill-rule', 'evenodd');
  svg.append(path);
  if (title) { const label = document.createElementNS(SVG, 'title'); label.textContent = title; svg.prepend(label); }
  return svg;
}
