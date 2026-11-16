import { createBattleReactions, REACTIONS } from '../../table/battle-reactions.js';

const el = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
type Bubble = { glyph: string; label: string; ago: string } | null;

/**
 * The reactions' controls (ADR 0173): the header button that opens the set, the set itself, and the two bubbles,
 * the other player's by their side of the field and this seat's by its own. Choosing one sends it and closes the
 * set: a reaction is one tap, not a conversation.
 */
export function reactionControls(controller: ReturnType<typeof createBattleReactions>, { opponentName, onShown }: { opponentName: () => string; onShown: (shown: boolean) => void }) {
  const dialog = el<HTMLDialogElement>('reactions-dialog');
  el('reactions-list').replaceChildren(...REACTIONS.map(reaction => {
    const button = document.createElement('button');
    button.type = 'button'; button.className = 'reaction-choice'; button.dataset.reaction = reaction.id;
    const glyph = document.createElement('span'); glyph.className = 'reaction-glyph'; glyph.setAttribute('aria-hidden', 'true'); glyph.textContent = reaction.glyph;
    const label = document.createElement('span'); label.textContent = reaction.label;
    button.append(glyph, label);
    button.addEventListener('click', () => { dialog.close(); void controller.send(reaction.id); });
    return button;
  }));
  el('reactions-open').addEventListener('click', () => { dialog.showModal(); render(); });
  el('reactions-close').addEventListener('click', () => dialog.close());
  dialog.addEventListener('close', () => el('reactions-open').focus({ preventScroll: true }));
  el('reactions-shown').addEventListener('change', () => onShown(el<HTMLInputElement>('reactions-shown').checked));

  function draw(id: string, bubble: Bubble, who: string) {
    const node = el(id);
    node.hidden = !bubble;
    if (!bubble) { delete node.dataset.shown; return; }
    const key = `${bubble.glyph}${bubble.label}${bubble.ago}${who}`;
    if (node.dataset.shown === key) return;
    node.dataset.shown = key;
    const glyph = document.createElement('span'); glyph.className = 'reaction-glyph'; glyph.setAttribute('aria-hidden', 'true'); glyph.textContent = bubble.glyph;
    const words = document.createElement('span'); words.className = 'reaction-words'; words.textContent = bubble.label;
    const parts: Node[] = [glyph, words];
    if (bubble.ago) { const ago = document.createElement('small'); ago.textContent = bubble.ago; parts.push(ago); }
    node.replaceChildren(...parts);
    node.setAttribute('aria-label', `${who}: ${bubble.label}${bubble.ago ? `, ${bubble.ago}` : ''}`);
  }

  function render() {
    const model = controller.model();
    el('reactions-open').hidden = !model.available;
    if (!model.available && dialog.open) dialog.close();
    const waiting = model.sending || model.coolingMs > 0;
    for (const button of el('reactions-list').querySelectorAll<HTMLButtonElement>('button')) button.disabled = waiting;
    el('reactions-note').textContent = model.message || (waiting ? 'Sent. You can react again in a moment.' : 'Your opponent sees it at once, or the next time they open the game.');
    el<HTMLInputElement>('reactions-shown').checked = model.shown;
    draw('reaction-theirs', model.theirs as Bubble, opponentName());
    draw('reaction-mine', model.mine as Bubble, 'You');
  }

  return { render };
}
