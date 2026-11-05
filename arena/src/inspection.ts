import { creatureInspection } from '../../table/arena-inspection.js';

type Inspection = NonNullable<ReturnType<typeof creatureInspection>>;
const el = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
const text = (id: string, value: string) => { if (el(id).textContent !== value) el(id).textContent = value; };
const node = (tag: string, value: string) => { const item = document.createElement(tag); item.textContent = value; return item; };

// Each disclosure is keyed independently: HP polls must not collapse a spell, reset scroll or steal focus.
export function inspectionControls() {
  let identity = '', mutationsKey = '', spellsKey = '';
  const dialog = el<HTMLDialogElement>('inspect-dialog');
  function clear() {
    identity = ''; mutationsKey = ''; spellsKey = '';
    dialog.close();
    for (const id of ['inspect-name', 'inspect-side', 'inspect-context', 'inspect-stats', 'inspect-appearance', 'inspect-effects', 'inspect-mutations', 'inspect-spells', 'inspect-last-action']) el(id).replaceChildren();
    el('inspect-history').hidden = true; el('inspect-growth').hidden = true;
  }
  function render(model: Inspection | null, context: string, historical: boolean, lastAction: string) {
    if (!model) { clear(); return; }
    if (identity !== model.id) { mutationsKey = ''; spellsKey = ''; el('inspect-body').scrollTop = 0; identity = model.id; }
    text('inspect-name', model.name); text('inspect-side', model.side); text('inspect-context', context);
    text('inspect-stats', model.stats); text('inspect-effects', model.effects);
    text('inspect-appearance', model.description ? `Appearance: ${model.description}` : 'Artwork unavailable · identity comes from the recorded creature');
    el('inspect-growth').dataset.growthCreature = model.id;
    el('inspect-growth').hidden = historical;
    text('inspect-action-heading', historical ? 'Latest loaded action at this point' : 'Last public action');
    text('inspect-last-action', lastAction || (historical ? 'See History for actions recorded at this point.' : 'No public cast received for this creature yet.'));
    el('inspect-history').hidden = !lastAction;
    const nextMutations = JSON.stringify([model.mutations, model.mutationsRecorded]);
    if (nextMutations !== mutationsKey) {
      el('inspect-mutations').replaceChildren(...model.mutations.map(pack => {
        const item = node('li', ''); item.dataset.inspectMutation = String(pack.id);
        item.append(node('strong', pack.name), node('p', ['Acquired', pack.stage == null ? '' : `Stage ${pack.stage}`,
          pack.initiative == null ? 'Initiative bonus unavailable' : `Initiative bonus ${pack.initiative >= 0 ? '+' : ''}${pack.initiative}`].filter(Boolean).join(' · ')));
        if (pack.tags.length) item.append(node('p', pack.tags.join(' · ')));
        for (const passive of pack.passives) item.append(node('p', passive));
        if (pack.missing) item.append(node('p', 'Mutation details unavailable.'));
        return item;
      }));
      if (!model.mutations.length) el('inspect-mutations').append(node('li', model.mutationsRecorded ? 'No publicly acquired mutations.' : 'Mutation ownership not recorded.'));
      mutationsKey = nextMutations;
    }
    const nextSpells = JSON.stringify([model.spells, model.spellsRecorded]);
    if (nextSpells !== spellsKey) {
      const list = el('inspect-spells'), old = new Map(Array.from(list.querySelectorAll<HTMLDetailsElement>('details')).map(item => [item.dataset.inspectSpell, item]));
      const wanted = new Set(model.spells.map(spell => String(spell.id)));
      for (const [id, item] of old) if (!wanted.has(id!)) item.remove();
      list.querySelector('[data-empty]')?.remove();
      for (const spell of model.spells) {
        const key = JSON.stringify(spell);
        let item = old.get(String(spell.id));
        if (!item) { item = document.createElement('details'); item.dataset.inspectSpell = String(spell.id); list.append(item); }
        if (item.dataset.content === key) continue;
        const summary = item.querySelector('summary') ?? document.createElement('summary');
        summary.textContent = `${spell.name}${spell.cost ? ` · ${spell.cost} energy` : ''}`;
        item.replaceChildren(summary, node('p', [spell.type, spell.critical].filter(Boolean).join(' · ')));
        for (const detail of spell.details) item.append(node('p', `${detail.role === 'caster' ? 'Caster: ' : ''}${detail.text}`));
        if (spell.criticalNote) item.append(node('p', spell.criticalNote));
        if (spell.missing) item.append(node('p', 'Spell details unavailable.'));
        item.append(node('p', spell.sources.length ? `Acquired through: ${spell.sources.join(', ')}` : 'Known spell · no acquired mutation source recorded.'));
        item.dataset.content = key;
      }
      if (!model.spells.length) { const empty = node('p', model.spellsRecorded ? 'No known spells.' : 'Known spells not recorded.'); empty.dataset.empty = ''; list.append(empty); }
      spellsKey = nextSpells;
    }
  }
  return { render, clear };
}
