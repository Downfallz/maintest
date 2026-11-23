import type { Placement } from './types';

// The accessible hit targets over the figures: one button a creature, placed where the scene says its figure
// stands. Both the laboratory and the live preview draw them, so the two cannot drift apart in what a tap lands on.

// Static structure only; every authored text is assigned through textContent by the caller.
// A screen reader hears the name a player sees, then one sentence (`speakCreature`): the drawn badges are silent,
// so the button's name is made of its own words and never differs from them (WCAG 2.5.3, axe's
// label-content-name-mismatch).
const CAPTION = '<span class="creature-name"></span><span class="unseen creature-spoken"></span><span class="creature-badges" aria-hidden="true"><span class="creature-init"></span><span class="creature-defense" hidden></span><span class="creature-conditions"></span></span><span class="health-track" aria-hidden="true"><span class="health-fill"></span></span><span class="creature-stats" aria-hidden="true"></span>';
const TEMPLATE = `<span class="creature-tag" aria-hidden="true"></span><span class="creature-result" aria-hidden="true"></span><span class="caption">${CAPTION}</span>`;

export function creatureButton(id: string) {
  const node = document.createElement('button');
  node.type = 'button';
  node.dataset.creature = id;
  node.disabled = true;
  node.innerHTML = TEMPLATE;
  return node;
}

/**
 * The caption's contents drawn afresh, empty, for the caller to fill in the same render. A caption that changes
 * form (compact under a docked sheet, full on the open field) would otherwise move its bar and its numbers inside
 * it, a layout shift a translation cannot carry (#1112); new contents have no place before to shift from.
 */
export function renewCaption(node: HTMLElement) {
  const caption = node.querySelector<HTMLElement>('.caption')!;
  // The health last shown stays known, so a blow landing in the same render still reads as a number that changed.
  const health = caption.querySelector<HTMLElement>('.creature-stats')?.dataset.health;
  caption.innerHTML = CAPTION;
  if (health !== undefined) caption.querySelector<HTMLElement>('.creature-stats')!.dataset.health = health;
}

/** What a screen reader hears after the creature's name: its side, its numbers and what a press does. */
export function speakCreature(node: HTMLElement, sentence: string) {
  const spoken = node.querySelector<HTMLElement>('.creature-spoken')!;
  if (spoken.textContent !== sentence) spoken.textContent = sentence;
}

// Buttons in the board's order, which is the layout's order too; the caption hangs below the feet, as far as the
// layout lifted the figure above it (42px at least).
export function placeHitTargets(buttons: HTMLButtonElement[], positions: Placement[]) {
  for (const [index, node] of buttons.entries()) {
    const position = positions[index];
    if (!position) continue;
    // Placed by a translation, not by left and top: a button that follows its figure when a sheet docks or closes
    // moves with the canvas, and is no layout shift of the page (UX11, docs/art/journeys/match.md).
    node.style.left = '0'; node.style.top = '0';
    node.style.transform = `translate(${position.x - position.hitWidth / 2}px, ${Math.max(0, position.y - position.size)}px)`;
    node.style.width = `${position.hitWidth}px`;
    const height = Math.max(44, position.size + (position.caption ?? 42));
    node.style.height = `${height}px`;
    // The caption hangs from this by a translation (battle-lab.css): a height that changes moves no layout.
    node.style.setProperty('--hit-height', `${height}px`);
    node.style.zIndex = String(Math.round(position.y));
  }
}
