import type { Placement } from './types';

// The accessible hit targets over the figures: one button a creature, placed where the scene says its figure
// stands. Both the laboratory and the live preview draw them, so the two cannot drift apart in what a tap lands on.

// Static structure only; every authored text is assigned through textContent by the caller.
const TEMPLATE = '<span class="creature-tag"></span><span class="creature-result"></span><span class="caption"><span class="creature-name"></span><span class="creature-init"></span><span class="health-track" aria-hidden="true"><span class="health-fill"></span></span><span class="creature-stats"></span></span>';

export function creatureButton(id: string) {
  const node = document.createElement('button');
  node.type = 'button';
  node.dataset.creature = id;
  node.disabled = true;
  node.innerHTML = TEMPLATE;
  return node;
}

// Buttons in the board's order, which is the layout's order too; the caption hangs 42px below the feet.
export function placeHitTargets(buttons: HTMLButtonElement[], positions: Placement[]) {
  for (const [index, node] of buttons.entries()) {
    const position = positions[index];
    if (!position) continue;
    node.style.left = `${position.x - position.hitWidth / 2}px`;
    node.style.top = `${Math.max(0, position.y - position.size)}px`;
    node.style.width = `${position.hitWidth}px`;
    node.style.height = `${Math.max(44, position.size + 42)}px`;
    node.style.zIndex = String(Math.round(position.y));
  }
}
