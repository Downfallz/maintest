import { chipText } from '../../table/board.js';

type Cue = { kind: string; glyph?: string; harmful: boolean };
type Condition = { effect?: { kind?: string } };
export interface ConditionIcon { key: string; title: string }

// The host marks each effect kind with the stat it moves and whether it is a loss (EffectCue); the page names
// no effect. An icon is that pair: a health loss reads as a drop, a gain as a heart, defense as a shield up or
// cracked, a stun as a star. The words, the engine's own, stay in the icon's title.
const STATS: Record<string, string> = { '♥': 'health', 'ϟ': 'energy', '◇': 'defense', '↟': 'initiative', '⊘': 'stun', '⚔': 'damage' };

// The icon an effect kind takes, from the stat the host says it moves and whether it is a loss.
export function conditionKey(kind: string | null | undefined, effects: Cue[] = []) {
  const cue = effects.find(one => one.kind === kind);
  const stat = STATS[cue?.glyph ?? ''];
  return !stat ? 'other' : stat === 'stun' ? 'stun' : `${stat}-${cue!.harmful ? 'loss' : 'gain'}`;
}

export function conditionIcons(creature: { isStunned?: boolean; conditions?: Condition[] }, effects: Cue[] = []) {
  const icons = new Map<string, string[]>();
  const add = (key: string, title: string) => { icons.set(key, [...(icons.get(key) ?? []), title].filter(Boolean)); };
  for (const condition of creature.conditions ?? []) add(conditionKey(condition.effect?.kind, effects), chipText(condition));
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

export interface EffectChip { kind: string; value: string; text?: string; condition?: string | null; critical?: boolean; label?: string; tempo?: string }
// One chip per effect, everywhere an effect lands on a creature: the icon of what moved, the amount, and the
// engine's words kept unseen for screen readers. Damage is a drop, healing a heart, energy a bolt, a condition
// its own icon; a condition ending is its icon dimmed.
const CHIP_ICONS: Record<string, string> = { damage: 'health-loss', heal: 'health-gain', energy: 'energy-gain', drain: 'energy-loss', immune: 'stun', other: 'other' };
export function chipElement(chip: EffectChip, effects: Cue[] = []) {
  const node = document.createElement('span');
  node.className = 'effect-chip'; node.dataset.kind = chip.kind;
  // A place in the turn order: its number large, its tempo beside it.
  if (chip.kind === 'order') {
    node.dataset.tempo = chip.tempo ?? '';
    const number = document.createElement('span'); number.className = 'order-number'; number.textContent = chip.value; number.setAttribute('aria-hidden', 'true');
    const tempo = document.createElement('span'); tempo.className = 'chip-label'; tempo.textContent = chip.label ?? ''; tempo.setAttribute('aria-hidden', 'true');
    const words = document.createElement('span'); words.className = 'unseen'; words.textContent = chip.text || chip.value;
    node.append(number, tempo, words); return node;
  }
  const key = chip.kind === 'condition' || chip.kind === 'ends' ? conditionKey(chip.condition, effects) : CHIP_ICONS[chip.kind] ?? 'other';
  if (chip.kind === 'condition' || chip.kind === 'ends') node.dataset.cue = key;
  node.append(iconElement(key));
  const value = document.createElement('span'); value.className = 'chip-value'; value.textContent = chip.value; value.setAttribute('aria-hidden', 'true');
  node.append(value);
  if (chip.critical) { const crit = document.createElement('span'); crit.className = 'chip-crit'; crit.textContent = 'CRIT'; crit.setAttribute('aria-hidden', 'true'); node.append(crit); node.dataset.critical = 'true'; }
  const words = document.createElement('span'); words.className = 'unseen'; words.textContent = chip.text || chip.value; node.append(words);
  return node;
}
