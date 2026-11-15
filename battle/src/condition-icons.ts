import { cuePath, STROKED } from './cue-art.js';
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
export function iconElement(key: string, title = '') {
  const svg = document.createElementNS(SVG, 'svg');
  svg.setAttribute('viewBox', '0 0 16 16'); svg.setAttribute('aria-hidden', 'true');
  svg.classList.add('cue-icon', `cue-${key}`);
  const path = document.createElementNS(SVG, 'path'); path.setAttribute('d', cuePath(key));
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
