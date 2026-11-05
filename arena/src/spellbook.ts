import { arenaSpellbook } from '../../table/arena-spellbook.js';
import type { createArenaCombat } from '../../table/arena-combat.js';
import type { SpellCatalogue } from './combat';

type Controller = ReturnType<typeof createArenaCombat>;
type Model = ReturnType<Controller['model']>;
type Spell = ReturnType<typeof arenaSpellbook>[number]['spells'][number];
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
  metadata.append(text('span', 'spell-cost'), text('span', 'spell-family', spell.family));
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
  mode.addEventListener('click', () => { full = !full; drawMode(); });
  function drawMode() {
    list.dataset.details = full ? 'full' : 'compact'; mode.setAttribute('aria-pressed', String(full));
    mode.textContent = full ? 'Compact view' : 'Full details';
  }
  function clear() {
    question = ''; structure = ''; catalogueIdentity = null; full = false; resetPosition = true; buttons.clear(); list.replaceChildren();
    scroll.scrollTop = 0; drawMode();
  }
  function rebuild(groups: ReturnType<typeof arenaSpellbook>) {
    const position = scroll.scrollTop;
    const focused = (document.activeElement as HTMLElement | null)?.dataset.spell;
    buttons = new Map();
    list.replaceChildren(...groups.map(group => {
      const section = document.createElement('section'); section.className = 'combat-spell-group';
      section.setAttribute('aria-label', group.type);
      const grid = document.createElement('div'); grid.className = 'combat-spell-grid';
      for (const spell of group.spells) {
        const button = spellButton(spell, id => controller.choose(id)); buttons.set(spell.id, button); grid.append(button);
      }
      section.append(text('h3', 'spell-group-title', `${group.type} · ${group.spells.length}`), grid); return section;
    }));
    if (focused) buttons.get(focused)?.focus({ preventScroll: true });
    scroll.scrollTop = position;
  }
  function render(model: Model, catalogue: SpellCatalogue | null) {
    if (question !== model.question) { clear(); question = model.question; }
    const groups = arenaSpellbook(model, catalogue);
    const next = JSON.stringify(groups.map(group => [group.type, group.spells.map((one: { id: string }) => one.id)]));
    if (next !== structure || catalogue !== catalogueIdentity) {
      rebuild(groups); structure = next; catalogueIdentity = catalogue;
    }
    for (const spell of groups.flatMap(group => group.spells)) {
      const node = buttons.get(spell.id)!;
      node.disabled = !spell.offered || !model.canEdit;
      node.setAttribute('aria-pressed', String(spell.id === model.chosen));
      node.querySelector('.spell-cost')!.textContent = spell.cost === '' ? 'Cost unavailable' : `${spell.cost} energy`;
      node.querySelector('.spell-critical')!.textContent = spell.critical;
      node.querySelector('.spell-availability')!.textContent = spell.availability;
    }
    drawMode();
  }
  return { render, clear, opened() { if (resetPosition) { scroll.scrollTop = 0; resetPosition = false; } } };
}
