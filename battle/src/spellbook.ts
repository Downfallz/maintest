import { battleSpellbook } from '../../table/battle-spellbook.js';
import type { createBattleCombat } from '../../table/battle-combat.js';
import type { SpellCatalogue } from './combat';

type Controller = ReturnType<typeof createBattleCombat>;
type Model = ReturnType<Controller['model']>;
type Spell = ReturnType<typeof battleSpellbook>[number]['spells'][number];
const text = (tag: string, className: string, value = '') => {
  const node = document.createElement(tag); node.className = className; node.textContent = value; return node;
};

function spellButton(spell: Spell, choose: (id: string) => void) {
  const node = document.createElement('button'); node.type = 'button'; node.className = 'combat-spell'; node.dataset.spell = spell.id;
  node.title = spell.name;
  if (spell.colour) node.style.setProperty('--spell-accent', spell.colour);
  const detail = text('span', 'spell-detail');
  detail.append(...spell.details.map((row: { role: string; text: string }) => text('span', `spell-${row.role}`, row.text)));
  if (spell.criticalNote) detail.append(text('span', 'spell-note', spell.criticalNote));
  if (spell.missing) detail.append(text('span', 'spell-note', 'Catalogue details unavailable.'));
  const metadata = text('span', 'spell-metadata');
  metadata.append(text('span', 'spell-cost'), text('span', 'spell-class', spell.creatureClass));
  node.append(text('strong', 'spell-name', spell.name), metadata, text('span', 'spell-critical'), text('span', 'spell-availability'), detail);
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
  let full = false;
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
    question = ''; structure = ''; catalogueIdentity = null; full = false; resetPosition = true; lastChosen = null; buttons.clear(); list.replaceChildren();
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
  function rebuild(groups: ReturnType<typeof battleSpellbook>) {
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
        const button = spellButton(spell, id => { lastChosen = id; controller.choose(id); }); buttons.set(spell.id, button); grid.append(button);
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
    const next = JSON.stringify(groups.map(group => [group.type, group.spells.map((one: { id: string }) => one.id)]));
    let settle = () => {};
    if (next !== structure || catalogue !== catalogueIdentity) {
      settle = rebuild(groups); structure = next; catalogueIdentity = catalogue;
    }
    for (const spell of groups.flatMap(group => group.spells)) {
      const node = buttons.get(spell.id)!;
      node.disabled = !spell.offered || !model.canEdit;
      node.setAttribute('aria-pressed', String(spell.id === model.chosen));
      node.querySelector('.spell-cost')!.textContent = spell.cost === '' ? 'Cost unavailable' : `${spell.cost} energy`;
      node.querySelector('.spell-critical')!.textContent = spell.critical;
      node.classList.toggle('spell-new', newSpells.includes(spell.id));
      node.querySelector('.spell-availability')!.textContent = `${newSpells.includes(spell.id) ? 'New · ' : ''}${spell.availability}`;
    }
    drawMode();
    settle();
  }
  // A fresh question starts at the top; returning from the field keeps the place and the card in hand.
  function opened() {
    if (resetPosition) { scroll.scrollTop = 0; resetPosition = false; return; }
    if (lastChosen) buttons.get(lastChosen)?.focus({ preventScroll: true });
  }
  return { render, clear, opened };
}
