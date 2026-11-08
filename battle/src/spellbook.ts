import { battleSpellbook } from '../../table/battle-spellbook.js';
import type { createBattleCombat } from '../../table/battle-combat.js';
import { cardsById } from '../../table/card.js';
import type { SpellCatalogue } from './combat';
import { spellCard } from './spell-card';

type Controller = ReturnType<typeof createBattleCombat>;
type Model = ReturnType<Controller['model']>;
type Spell = ReturnType<typeof battleSpellbook>[number]['spells'][number];
const text = (tag: string, className: string, value = '') => {
  const node = document.createElement(tag); node.className = className; node.textContent = value; return node;
};

// A spell to choose is the same card as everywhere else (the mutation tree's): name, cost and crit chips, its
// targeting, and its effect lines with their icons, which Full details shows. Under it, whether it can be cast now.
function spellButton(spell: Spell, card: Parameters<typeof spellCard>[0], effects: Parameters<typeof spellCard>[2], choose: (id: string) => void) {
  const node = document.createElement('button'); node.type = 'button'; node.className = 'combat-spell'; node.dataset.spell = spell.id;
  node.title = spell.name;
  if (spell.colour) node.style.setProperty('--spell-accent', spell.colour);
  const face = spellCard(card, spell.id, effects, { inline: true, cost: spell.cost === '' ? null : spell.cost, crit: spell.crit });
  // The effect lines and the crit note are the card's full details; compact shows its head and targeting.
  const detail = text('span', 'spell-detail');
  detail.append(...face.querySelectorAll(':scope > .spell-card-effects, :scope > .spell-card-note'));
  if (spell.missing) detail.append(text('span', 'spell-note', 'Catalogue details unavailable.'));
  face.append(text('span', 'spell-critical', spell.crit?.hint ?? ''), text('span', 'spell-availability'), detail);
  node.append(face);
  node.addEventListener('click', () => choose(spell.id));
  return node;
}

// Keep row nodes and scroll across polls and target selection. Reference browsing never sends a decision.
export function spellbookControls(controller: Controller) {
  const list = document.getElementById('combat-spells')!;
  const mode = document.getElementById('combat-details-mode') as HTMLButtonElement;
  const scroll = document.getElementById('combat-browser')!;
  let question = '', structure = '', catalogueIdentity: SpellCatalogue | null = null;
  let buttons = new Map<string, HTMLButtonElement>();
  // Full details by default: a spell is read in full before it is chosen; compact is the player's choice.
  let full = true;
  let resetPosition = true;
  // Going back from the field clears the draft; the card the player was on is still where they left it.
  let lastChosen: string | null = null;
  mode.addEventListener('click', () => { full = !full; drawMode(); });
  // One label and a pressed state: a label that flipped with the state would announce the opposite mode.
  function drawMode() {
    list.dataset.details = full ? 'full' : 'compact'; mode.setAttribute('aria-pressed', String(full));
    mode.textContent = 'Full details';
  }
  function clear() {
    question = ''; structure = ''; catalogueIdentity = null; full = true; resetPosition = true; lastChosen = null; buttons.clear(); list.replaceChildren();
    scroll.scrollTop = 0; drawMode();
  }
  // The card under the player's eye: the focused one, or else the first one showing in the scroller.
  function anchor() {
    const focused = (document.activeElement as HTMLElement | null)?.dataset.spell;
    if (focused && buttons.has(focused)) return focused;
    const top = scroll.getBoundingClientRect().top;
    return [...buttons].find(([, node]) => node.getBoundingClientRect().bottom > top)?.[0];
  }
  const offsetOf = (id: string | undefined) => id && buttons.has(id)
    ? buttons.get(id)!.getBoundingClientRect().top - scroll.getBoundingClientRect().top : null;
  // Rebuilt cards are measured only once their cost and availability are filled, or the recalibration is short.
  function rebuild(groups: ReturnType<typeof battleSpellbook>, catalogue: SpellCatalogue | null) {
    const cards = cardsById(catalogue), cues = catalogue?.effects ?? [];
    const position = scroll.scrollTop;
    const focused = (document.activeElement as HTMLElement | null)?.dataset.spell;
    const held = anchor(), before = offsetOf(held);
    buttons = new Map();
    list.replaceChildren(...groups.map((group, index) => {
      // A group, not a landmark: one dialog does not need a region per spell type.
      const section = document.createElement('div'); section.className = 'combat-spell-group'; section.setAttribute('role', 'group');
      const title = text('h3', 'spell-group-title', `${group.type} · ${group.spells.length}`); title.id = `spell-group-${index}`;
      section.setAttribute('aria-labelledby', title.id);
      const grid = document.createElement('div'); grid.className = 'combat-spell-grid';
      for (const spell of group.spells) {
        // An Action's card goes straight to its targets, so the card is remembered when it is tapped.
        const button = spellButton(spell, cards.get(spell.id), cues, id => { lastChosen = id; controller.choose(id); }); buttons.set(spell.id, button); grid.append(button);
      }
      section.append(title, grid); return section;
    }));
    if (focused) buttons.get(focused)?.focus({ preventScroll: true });
    return () => {
      scroll.scrollTop = position;
      // Cards added or removed above it must not move the card the player is on.
      const after = offsetOf(held);
      if (before !== null && after !== null) scroll.scrollTop += after - before;
    };
  }
  // `newSpells` are this creature's spells a public reveal has just unlocked (AU8): marked, never selected.
  function render(model: Model, catalogue: SpellCatalogue | null, newSpells: string[] = []) {
    if (question !== model.question) { clear(); question = model.question; }
    const groups = battleSpellbook(model, catalogue);
    // A card is drawn again when what its face shows changes: the cost and crit chance follow the host's guide.
    const next = JSON.stringify(groups.map(group => [group.type, group.spells.map((one: Spell) => [one.id, one.cost, one.crit])]));
    let settle = () => {};
    if (next !== structure || catalogue !== catalogueIdentity) {
      settle = rebuild(groups, catalogue); structure = next; catalogueIdentity = catalogue;
    }
    for (const spell of groups.flatMap(group => group.spells)) {
      const node = buttons.get(spell.id)!;
      node.disabled = !spell.offered || !model.canEdit;
      node.setAttribute('aria-pressed', String(spell.id === model.chosen));
      node.classList.toggle('spell-new', newSpells.includes(spell.id));
      node.querySelector('.spell-availability')!.textContent = `${newSpells.includes(spell.id) ? 'New · ' : ''}${spell.availability}`;
    }
    drawMode();
    settle();
  }
  // A fresh question starts at the top; returning from the field keeps the place and the card in hand.
  function opened() {
    // The docked choice is not modal: the browser would focus its close button, so a fresh question starts on the
    // first spell that can be cast, as the mutation choice starts on its first offer.
    if (resetPosition) {
      scroll.scrollTop = 0; resetPosition = false;
      [...buttons.values()].find(node => !node.disabled)?.focus({ preventScroll: true });
      return;
    }
    if (lastChosen) buttons.get(lastChosen)?.focus({ preventScroll: true });
  }
  return { render, clear, opened };
}
