// The content studio (ADR 0015). One page over the studio API: browse the authored content, edit it in forms
// that know the schema, cut a new version, turn things off, rebuild, and play what the change does.
// No framework and no build step, like the viewer it sits next to. Loaded as a module, so it is strict and
// scoped to itself, and the first read of the content is awaited at the top level.

// ---------- what the schema allows ----------

const TABS = {
  creatures: { kind: 'Creature', label: 'creature', folder: 'Creatures' },
  spells: { kind: 'Spell', label: 'spell', folder: 'Spells' },
  talentTrees: { kind: 'TalentTree', label: 'talent tree', folder: 'TalentTrees' },
};

const SPELL_TYPES = ['Offensive', 'Defensive', 'Passive'];
const CREATURE_CLASSES = [
  'Creature', 'Brawler', 'Scoundrel', 'Sorcerer', 'Mercenary', 'Warlord', 'Berserker',
  'Leech', 'Assassin', 'Trickster', 'Wizard', 'Necromancer', 'Shaman',
];
const TARGET_ORIGINS = ['Self', 'Ally', 'Enemy', 'Any'];
const TARGET_SCOPES = ['SingleTarget', 'Multi'];
const STACKING = ['Stack', 'Refresh', 'Ignore'];

// Mirrors the effect taxonomy (ADR 0012) and the table in data/README.md.
const EFFECTS = {
  Damage: { amounts: ['amount'] },
  Heal: { amounts: ['amount'] },
  EnergyGain: { amounts: ['amount'] },
  Bleed: { amounts: ['amountPerRound'], rounds: true, stacking: 'Refresh' },
  Stun: { amounts: [], rounds: true, stacking: 'Refresh' },
  DefenseBuff: { amounts: ['amount'], rounds: true, permanent: true, stacking: 'Stack' },
  InitiativeDebuff: { amounts: ['amount'], rounds: true, permanent: true, stacking: 'Stack' },
};

const TEMPLATES = {
  spells: () => ({
    id: 'spell:new_spell:v1',
    name: 'New spell',
    spellType: 'Offensive',
    creatureClass: 'Creature',
    initiative: 1,
    energyCost: 0,
    criticalChance: 0,
    targeting: { origin: 'Enemy', scope: 'SingleTarget', maxTargets: 1 },
    effects: [{ kind: 'Damage', amount: 1 }],
  }),
  creatures: () => ({
    id: 'creature:new_creature:v1',
    name: 'New creature',
    creatureClass: 'Creature',
    baseHealth: 20,
    baseEnergy: 0,
    baseDefense: 0,
    baseInitiative: 5,
    baseCriticalChance: 0.05,
    talentTreeId: '',
    startingSpellIds: [],
  }),
  talentTrees: () => ({
    id: 'talent-tree:new_tree:v1',
    name: 'New talent tree',
    root: emptyNode('Root', 'Root'),
  }),
};

const state = { catalogue: null, tab: 'spells', selected: null, draft: null, dirty: false, node: null, busy: false };

// ---------- small helpers ----------

const $ = id => document.getElementById(id);
const clone = value => structuredClone(value);

function emptyNode(code, name) {
  return { code, name, prerequisites: { allOf: [], anyOf: [] }, spells: [], children: [] };
}

function parseId(id) {
  const match = /^([A-Za-z0-9_-]+):([A-Za-z0-9_-]+):v(\d+)$/.exec(id || '');
  return match ? { kind: match[1], name: match[2], version: Number(match[3]) } : null;
}

function element(tag, properties = {}, children = []) {
  const node = Object.assign(document.createElement(tag), properties);
  for (const child of [children].flat()) {
    if (child !== null && child !== undefined) node.append(child);
  }
  return node;
}

function documentsOf(tab) {
  return state.catalogue?.[tab] || [];
}

function findDocument(path) {
  for (const tab of Object.keys(TABS)) {
    const found = documentsOf(tab).find(item => item.path === path);
    if (found) return { tab, item: found };
  }
  return null;
}

/** Alphabetical, by the reader's collation rather than by UTF-16 code unit. */
function byName(values) {
  return values.toSorted((left, right) => left.localeCompare(right));
}

/** Every way an author may name a spell: its versioned id, and the aliases pointing at it. */
function spellReferences() {
  const aliases = state.catalogue?.aliases || {};
  const versioned = documentsOf('spells').map(spell => spell.id);
  const named = Object.keys(aliases).filter(alias => versioned.includes(aliases[alias]));
  return [...new Set([...byName(named), ...byName(versioned)])];
}

/** The versioned id a reference means, following the alias map like the data builder does. */
function resolveReference(reference) {
  const aliases = state.catalogue?.aliases || {};
  return aliases[reference] || reference;
}

function spellNamed(reference) {
  const target = resolveReference(reference);
  return documentsOf('spells').find(spell => spell.id === target) || null;
}

// ---------- talking to the studio ----------

async function call(path, body) {
  const response = await fetch(path, body === undefined
    ? { headers: { accept: 'application/json' } }
    : { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
  const payload = await response.json().catch(() => ({ ok: false, message: `${response.status} ${response.statusText}` }));
  if (!payload.ok) {
    const error = new Error(payload.message || 'The studio refused that.');
    error.problems = payload.problems || [];
    throw error;
  }
  return payload.result;
}

/** Runs one action at a time, showing in the banner what it did or what it refused. */
async function act(what, action) {
  if (state.busy) return null;
  state.busy = true;
  banner(`${what}...`, 'info');
  try {
    const result = await action();
    return result;
  } catch (error) {
    banner(error.message, 'error', error.problems);
    return null;
  } finally {
    state.busy = false;
  }
}

function adopt(result) {
  if (result?.catalogue) {
    state.catalogue = result.catalogue;
    renderHeader();
    renderNav();
  }
}

// ---------- the banner ----------

function banner(message, kind = 'info', problems = []) {
  const node = $('banner');
  node.hidden = false;
  node.className = kind === 'error' || kind === 'ok' ? `banner ${kind}` : 'banner';
  node.replaceChildren(element('div', { textContent: message }));
  if (problems.length) {
    node.append(element('ul', {}, problems.map(problem => element('li', { textContent: problem }))));
  }
}

function clearBanner() {
  $('banner').hidden = true;
}

/** Says what a change did, and whether the content still builds after it. */
function report(message) {
  const problems = state.catalogue?.problems || [];
  banner(problems.length ? `${message} The content does not build yet.` : message, problems.length ? 'error' : 'ok', problems);
}

// ---------- header and navigation ----------

function renderHeader() {
  const catalogue = state.catalogue;
  const hash = $('hash');
  if (!catalogue) {
    hash.textContent = 'loading...';
    return;
  }

  hash.textContent = catalogue.contentHash
    ? `content ${catalogue.contentHash.slice(0, 12)}`
    : `${catalogue.problems.length} problem(s): the content does not build`;
  hash.classList.toggle('dirty', !catalogue.contentHash);
  if (!catalogue.contentHash && catalogue.problems.length) {
    banner('The content does not build.', 'error', catalogue.problems);
  }
}

function renderNav() {
  for (const tab of document.querySelectorAll('.tab')) {
    tab.classList.toggle('selected', tab.dataset.tab === state.tab);
  }

  const filter = $('search').value.trim().toLowerCase();
  const items = documentsOf(state.tab)
    .filter(item => !filter || item.id.toLowerCase().includes(filter) || (item.name || '').toLowerCase().includes(filter))
    .sort((left, right) => (left.name || left.id).localeCompare(right.name || right.id));

  $('list').replaceChildren(...items.map(item => {
    const entry = element('li', { className: [item.enabled ? '' : 'off', item.problem ? 'broken' : ''].join(' ').trim() }, [
      element('span', { textContent: item.name || item.id }),
      element('span', { className: 'id', textContent: item.id }),
    ]);
    if (state.selected?.path === item.path) entry.classList.add('selected');
    entry.addEventListener('click', () => select(item.path));
    return entry;
  }));

  $('new').textContent = `New ${TABS[state.tab].label}`;
}

function select(path, node = null) {
  const found = findDocument(path);
  if (!found) {
    state.selected = null;
    state.draft = null;
    renderDetail();
    return;
  }

  state.tab = found.tab;
  state.selected = found.item;
  state.draft = clone(found.item.document);
  state.dirty = false;
  state.node = node;
  renderNav();
  renderDetail();
}

// ---------- form building blocks ----------

function fields(rows) {
  const grid = element('div', { className: 'fields' });
  for (const [label, control] of rows) {
    grid.append(element('label', { textContent: label }), control);
  }
  return grid;
}

function markDirty() {
  state.dirty = true;
  const flag = $('dirty-flag');
  if (flag) flag.textContent = 'unsaved changes';
}

function textBox(target, key, { placeholder = '' } = {}) {
  const input = element('input', { type: 'text', value: target[key] ?? '', placeholder });
  input.addEventListener('input', () => { target[key] = input.value; markDirty(); });
  return input;
}

function numberBox(target, key, { step = 1, min = null } = {}) {
  const input = element('input', { type: 'number', step, value: target[key] ?? 0 });
  if (min !== null) input.min = min;
  input.addEventListener('input', () => {
    target[key] = input.value === '' ? null : Number(input.value);
    markDirty();
  });
  return input;
}

function picker(target, key, options, { onChange = null, allowEmpty = false } = {}) {
  const select = element('select');
  const values = allowEmpty ? ['', ...options] : options;
  for (const value of values) {
    select.append(element('option', { value, textContent: value || '(none)', selected: (target[key] ?? '') === value }));
  }
  const current = target[key] ?? '';
  if (current !== '' && !values.includes(current)) {
    select.append(element('option', { value: current, textContent: `${current} (unknown)`, selected: true }));
  }
  select.addEventListener('change', () => {
    target[key] = select.value;
    markDirty();
    onChange?.();
  });
  return select;
}

function miniButton(label, onClick, className = 'mini') {
  const button = element('button', { type: 'button', className, textContent: label });
  button.addEventListener('click', onClick);
  return button;
}

function spellLink(reference) {
  const spell = spellNamed(reference);
  const label = spell ? `${spell.name || spell.id}` : `${reference} (unknown)`;
  const chip = element('button', {
    type: 'button',
    className: `chip spell link${spell && !spell.enabled ? ' off' : ''}`,
    textContent: label,
    title: reference,
  });
  chip.addEventListener('click', () => { if (spell) select(spell.path); });
  return chip;
}

/** An editable list of spell references: a picker per row, plus one to add. */
function spellList(target, key) {
  const container = element('div');
  const redraw = () => {
    const rows = (target[key] || []).map((reference, index) => {
      const holder = { value: reference };
      const select = picker(holder, 'value', spellReferences());
      select.addEventListener('change', () => { target[key][index] = holder.value; markDirty(); redraw(); });
      return element('div', { className: 'row' }, [
        element('span', { className: 'grow' }, [select]),
        spellLink(reference),
        miniButton('Remove', () => { target[key].splice(index, 1); markDirty(); redraw(); }, 'mini remove'),
      ]);
    });
    rows.push(element('div', { className: 'row' }, [
      miniButton('Add spell', () => {
        target[key] = [...(target[key] || []), spellReferences()[0] || ''];
        markDirty();
        redraw();
      }),
    ]));
    container.replaceChildren(...rows);
  };

  redraw();
  return container;
}

// ---------- detail views ----------

function renderDetail() {
  const view = $('detail');
  if (!state.selected || !state.draft) {
    view.replaceChildren(element('p', { className: 'empty', textContent: 'Pick something on the left.' }));
    return;
  }

  const item = state.selected;
  if (item.problem && !state.draft.id) {
    view.replaceChildren(
      element('div', { className: 'title' }, [element('h2', { textContent: item.path })]),
      element('div', { className: 'banner error', textContent: `${item.path}: ${item.problem}` }),
      element('p', { className: 'muted', textContent: 'The studio will not edit a file it cannot read: fix the JSON by hand, then reload this page.' }),
    );
    return;
  }

  const parts = [header(item), actions(item)];
  if (item.problem) {
    parts.push(element('div', { className: 'banner error', textContent: `${item.path}: ${item.problem}` }));
  }

  if (state.tab === 'spells') parts.push(spellEditor(), usedBy(item));
  if (state.tab === 'creatures') parts.push(creatureEditor());
  if (state.tab === 'talentTrees') parts.push(treeEditor());

  view.replaceChildren(...parts);
}

function header(item) {
  return element('div', { className: 'title' }, [
    element('h2', { textContent: state.draft.name || item.id }),
    element('span', { className: 'muted mono', textContent: item.id }),
    element('span', { className: 'muted mono', textContent: item.path }),
  ]);
}

function actions(item) {
  const enabled = state.draft.enabled !== false;
  return element('div', { className: 'actions' }, [
    miniButton('Save', save, 'button primary'),
    miniButton('Save as next version', saveAsNextVersion, 'button'),
    miniButton(enabled ? 'Disable' : 'Enable', () => setEnabled(!enabled), 'button'),
    element('span', { className: 'spacer' }),
    element('span', { id: 'dirty-flag', className: 'dirty', textContent: state.dirty ? 'unsaved changes' : '' }),
    miniButton('Delete', () => remove(item), 'button danger'),
  ]);
}

function spellEditor() {
  const draft = state.draft;
  draft.targeting = draft.targeting || { origin: 'Enemy', scope: 'SingleTarget' };
  const card = element('div', { className: 'card' }, [element('h3', { textContent: 'Spell' })]);
  card.append(fields([
    ['Id', textBox(draft, 'id')],
    ['Name', textBox(draft, 'name')],
    ['Type', picker(draft, 'spellType', SPELL_TYPES)],
    ['Class', picker(draft, 'creatureClass', CREATURE_CLASSES)],
    ['Initiative', numberBox(draft, 'initiative')],
    ['Energy cost', numberBox(draft, 'energyCost', { min: 0 })],
    ['Critical chance', numberBox(draft, 'criticalChance', { step: 0.01, min: 0 })],
    ['Target origin', picker(draft.targeting, 'origin', TARGET_ORIGINS)],
    ['Target scope', picker(draft.targeting, 'scope', TARGET_SCOPES, { onChange: normalizeTargeting })],
    ['Max targets', draft.targeting.scope === 'Multi'
      ? numberBox(draft.targeting, 'maxTargets', { min: 2 })
      : element('span', { className: 'muted', textContent: 'single target' })],
  ]));

  const effects = element('div', { className: 'card' }, [element('h3', { textContent: 'Effects' })]);
  const list = element('div');
  const redraw = () => {
    list.replaceChildren(...(draft.effects || []).map((effect, index) => effectRow(effect, index, redraw)));
    list.append(element('div', { className: 'row' }, [
      miniButton('Add effect', () => {
        draft.effects = [...(draft.effects || []), { kind: 'Damage', amount: 1 }];
        markDirty();
        redraw();
      }),
    ]));
  };

  redraw();
  effects.append(list);
  return element('div', {}, [card, effects]);
}

/** A single target takes no count; a multi target takes at least two, which is what the engine will accept. */
function normalizeTargeting() {
  const targeting = state.draft.targeting;
  if (targeting.scope === 'Multi') {
    if (typeof targeting.maxTargets !== 'number' || targeting.maxTargets < 2) targeting.maxTargets = 2;
  } else {
    delete targeting.maxTargets;
  }

  renderDetail();
}

function effectRow(effect, index, redraw) {
  const draft = state.draft;
  const shape = EFFECTS[effect.kind] || { amounts: [] };
  const row = element('div', { className: 'row' });

  row.append(picker(effect, 'kind', Object.keys(EFFECTS), {
    onChange: () => {
      // A kind carries its own fields; keeping the old ones would write nonsense the builder then refuses.
      draft.effects[index] = defaultEffect(effect.kind);
      redraw();
    },
  }));

  for (const amount of shape.amounts) {
    row.append(element('span', { className: 'muted', textContent: amount }), numberBox(effect, amount));
  }

  if (shape.rounds && !effect.permanent) {
    row.append(element('span', { className: 'muted', textContent: 'rounds' }), numberBox(effect, 'durationRounds', { min: 1 }));
  }

  if (shape.permanent) {
    const permanent = element('input', { type: 'checkbox', checked: Boolean(effect.permanent) });
    permanent.addEventListener('change', () => {
      effect.permanent = permanent.checked;
      if (effect.permanent) delete effect.durationRounds; else effect.durationRounds = 1;
      markDirty();
      redraw();
    });
    row.append(permanent, element('span', { className: 'muted', textContent: 'permanent' }));
  }

  if (shape.stacking) {
    row.append(element('span', { className: 'muted', textContent: 'stacking' }), picker(effect, 'stacking', STACKING));
  }

  row.append(element('span', { className: 'grow' }));
  row.append(miniButton('Remove', () => { draft.effects.splice(index, 1); markDirty(); redraw(); }, 'mini remove'));
  return row;
}

function defaultEffect(kind) {
  const shape = EFFECTS[kind];
  const effect = { kind };
  for (const amount of shape.amounts) effect[amount] = 1;
  if (shape.rounds) effect.durationRounds = 1;
  if (shape.stacking) effect.stacking = shape.stacking;
  return effect;
}

function creatureEditor() {
  const draft = state.draft;
  const trees = documentsOf('talentTrees');
  const treeIds = trees.map(tree => tree.id);
  const card = element('div', { className: 'card' }, [element('h3', { textContent: 'Creature' })]);
  const treeRow = element('span', { className: 'inline' }, [
    picker(draft, 'talentTreeId', treeIds, { allowEmpty: true }),
    miniButton('Open tree', () => {
      const tree = trees.find(candidate => candidate.id === resolveReference(draft.talentTreeId));
      if (tree) select(tree.path);
    }),
  ]);

  card.append(fields([
    ['Id', textBox(draft, 'id')],
    ['Name', textBox(draft, 'name')],
    ['Class', picker(draft, 'creatureClass', CREATURE_CLASSES)],
    ['Health', numberBox(draft, 'baseHealth', { min: 1 })],
    ['Energy', numberBox(draft, 'baseEnergy', { min: 0 })],
    ['Defense', numberBox(draft, 'baseDefense', { min: 0 })],
    ['Initiative', numberBox(draft, 'baseInitiative', { min: 0 })],
    ['Critical chance', numberBox(draft, 'baseCriticalChance', { step: 0.01, min: 0 })],
    ['Talent tree', treeRow],
  ]));

  const spells = element('div', { className: 'card' }, [
    element('h3', { textContent: 'Starting spells' }),
    spellList(draft, 'startingSpellIds'),
  ]);
  return element('div', {}, [card, spells]);
}

// ---------- the talent tree ----------

function treeEditor() {
  const draft = state.draft;
  draft.root = draft.root || emptyNode('Root', 'Root');
  const identity = element('div', { className: 'card' }, [
    element('h3', { textContent: 'Talent tree' }),
    fields([['Id', textBox(draft, 'id')], ['Name', textBox(draft, 'name')]]),
  ]);

  const tree = element('div', { className: 'card' }, [element('h3', { textContent: 'Nodes' })]);
  const list = element('ul', { className: 'tree' });
  const redraw = () => {
    list.replaceChildren(nodeView(draft.root, null, redraw));
  };

  redraw();
  tree.append(list);
  return element('div', {}, [identity, tree, nodeEditor(redraw)]);
}

function nodeView(node, parent, redraw) {
  const selected = state.node === node;
  const card = element('div', { className: `node${selected ? ' selected' : ''}` });
  const head = element('div', { className: 'head' }, [
    element('span', { className: 'code', textContent: node.code || '(no code)' }),
    element('span', { className: 'muted', textContent: node.name || '' }),
    element('span', { className: 'grow' }),
    miniButton(selected ? 'Editing' : 'Edit', () => { state.node = node; redraw(); refreshNodeEditor(redraw); }),
    miniButton('Add child', () => {
      node.children = [...(node.children || []), emptyNode(`Node${(node.children || []).length + 1}`, 'New node')];
      markDirty();
      redraw();
    }),
  ]);

  if (parent) {
    head.append(miniButton('Remove', () => {
      parent.children.splice(parent.children.indexOf(node), 1);
      if (state.node === node) state.node = null;
      markDirty();
      redraw();
      refreshNodeEditor(redraw);
    }, 'mini remove'));
  }

  card.append(head);
  for (const [what, references] of [['all of', node.prerequisites?.allOf], ['any of', node.prerequisites?.anyOf]]) {
    if (!references?.length) continue;
    card.append(
      element('div', { className: 'muted', textContent: `needs ${what}:` }),
      element('div', { className: 'chips' }, references.map(spellLink)),
    );
  }

  if ((node.spells || []).length) {
    card.append(element('div', { className: 'chips' }, node.spells.map(spell => spellLink(spell.id))));
  }

  const entry = element('li', {}, [card]);
  if ((node.children || []).length) {
    entry.append(element('ul', {}, node.children.map(child => nodeView(child, node, redraw))));
  }

  return entry;
}

function nodeEditor(redrawTree) {
  const holder = element('div', { className: 'card', id: 'node-editor' });
  fillNodeEditor(holder, redrawTree);
  return holder;
}

function refreshNodeEditor(redrawTree) {
  const holder = $('node-editor');
  if (holder) fillNodeEditor(holder, redrawTree);
}

function fillNodeEditor(holder, redrawTree) {
  const node = state.node;
  if (!node) {
    holder.replaceChildren(
      element('h3', { textContent: 'Node' }),
      element('p', { className: 'muted', textContent: 'Pick a node above to edit its code, its prerequisites and its spells.' }),
    );
    return;
  }

  node.prerequisites = node.prerequisites || { allOf: [], anyOf: [] };
  const spells = element('div');
  const redrawSpells = () => {
    spells.replaceChildren(...(node.spells || []).map((spell, index) => {
      spell.prerequisites = spell.prerequisites || { allOf: [], anyOf: [] };
      const reference = { value: spell.id };
      const select = picker(reference, 'value', spellReferences());
      select.addEventListener('change', () => { spell.id = reference.value; markDirty(); redrawTree(); });
      const needs = (spell.prerequisites.allOf || []).length + (spell.prerequisites.anyOf || []).length;
      const folded = element('details', {}, [
        element('summary', { textContent: `Prerequisites (${needs})` }),
        prerequisiteCard(`Prerequisites of ${spell.id}`, spell.prerequisites, redrawTree),
      ]);
      return element('div', {}, [
        element('div', { className: 'row' }, [
          select,
          spellLink(spell.id),
          element('span', { className: 'grow' }),
          miniButton('Remove', () => { node.spells.splice(index, 1); markDirty(); redrawSpells(); redrawTree(); }, 'mini remove'),
        ]),
        folded,
      ]);
    }));
    spells.append(element('div', { className: 'row' }, [
      miniButton('Add spell', () => {
        node.spells = [...(node.spells || []), { id: spellReferences()[0] || '', prerequisites: { allOf: [], anyOf: [] } }];
        markDirty();
        redrawSpells();
        redrawTree();
      }),
    ]));
  };

  redrawSpells();
  holder.replaceChildren(
    element('h3', { textContent: `Node ${node.code || ''}` }),
    fields([['Code', textBox(node, 'code')], ['Name', textBox(node, 'name')]]),
    element('h3', { textContent: 'The node is reachable when the creature knows' }),
    prerequisiteCard('Node prerequisites', node.prerequisites, redrawTree),
    element('h3', { textContent: 'Spells taught here' }),
    spells,
  );
}

function prerequisiteCard(title, prerequisites, redrawTree) {
  prerequisites.allOf = prerequisites.allOf || [];
  prerequisites.anyOf = prerequisites.anyOf || [];
  const wrap = element('div', { title });
  wrap.append(fields([
    ['All of', spellList(prerequisites, 'allOf')],
    ['Any of', spellList(prerequisites, 'anyOf')],
  ]));
  wrap.addEventListener('change', () => redrawTree());
  return wrap;
}

// ---------- where a spell is used ----------

function usedBy(item) {
  const references = [];
  for (const creature of documentsOf('creatures')) {
    const starting = creature.document.startingSpellIds || [];
    if (starting.some(reference => resolveReference(reference) === item.id)) {
      references.push({ label: `${creature.name || creature.id} starts with it`, path: creature.path });
    }
  }

  for (const tree of documentsOf('talentTrees')) {
    walkNodes(tree.document.root, node => {
      const mentioned = [
        ...(node.spells || []).map(spell => spell.id),
        ...(node.prerequisites?.allOf || []),
        ...(node.prerequisites?.anyOf || []),
        ...(node.spells || []).flatMap(spell => [...(spell.prerequisites?.allOf || []), ...(spell.prerequisites?.anyOf || [])]),
      ];
      if (mentioned.some(reference => resolveReference(reference) === item.id)) {
        references.push({ label: `${tree.name || tree.id}, node ${node.code}`, path: tree.path });
      }
    });
  }

  const aliases = Object.entries(state.catalogue?.aliases || {})
    .filter(([, target]) => target === item.id)
    .map(([alias]) => alias);

  const card = element('div', { className: 'card' }, [element('h3', { textContent: 'Used by' })]);
  if (aliases.length) {
    card.append(element('p', { className: 'muted' }, [`Named by ${aliases.join(', ')}`]));
  }

  if (!references.length) {
    card.append(element('p', { className: 'muted', textContent: 'Nothing references this spell yet.' }));
    return card;
  }

  card.append(element('ul', { className: 'uses' }, references.map(reference => {
    const link = miniButton(reference.label, () => select(reference.path), 'link');
    return element('li', {}, [link]);
  })));
  return card;
}

function walkNodes(node, visit) {
  if (!node) return;
  visit(node);
  for (const child of node.children || []) walkNodes(child, visit);
}

// ---------- the actions ----------

function payload() {
  const content = clone(state.draft);
  if (content.enabled !== false) delete content.enabled;
  return content;
}

async function save() {
  const item = state.selected;
  const result = await act('Saving', () => call('/api/documents', {
    kind: TABS[state.tab].kind,
    path: item.path,
    document: payload(),
  }));

  if (!result) return;
  adopt(result);
  state.dirty = false;
  report(`Saved ${result.saved}.`);
  select(item.path);
}

async function saveAsNextVersion() {
  const parsed = parseId(state.draft.id);
  if (!parsed) {
    banner(`'${state.draft.id}' is not a versioned id, so there is no next version to cut.`, 'error');
    return;
  }

  const nextId = `${parsed.kind}:${parsed.name}:v${parsed.version + 1}`;
  const path = state.selected.path;
  const nextPath = /\.v\d+\.json$/.test(path)
    ? path.replace(/\.v\d+\.json$/, `.v${parsed.version + 1}.json`)
    : path.replace(/\.json$/, `.v${parsed.version + 1}.json`);

  const result = await act(`Cutting ${nextId}`, async () => {
    const saved = await call('/api/documents', {
      kind: TABS[state.tab].kind,
      path: nextPath,
      document: { ...payload(), id: nextId },
      create: true,
    });
    const aliases = { ...state.catalogue.aliases, [`${parsed.kind}:${parsed.name}`]: nextId };
    return { ...saved, ...(await call('/api/aliases', { aliases })) };
  });

  if (!result) return;
  adopt(result);
  report(`${nextId} written to ${nextPath}; the alias ${parsed.kind}:${parsed.name} now points at it.`);
  select(nextPath);
}

async function setEnabled(enabled) {
  state.draft.enabled = enabled;
  if (enabled) delete state.draft.enabled;
  markDirty();
  await save();
}

async function remove(item) {
  if (!window.confirm(`Delete ${item.path}? The file goes away; git still has it.`)) return;
  const result = await act('Deleting', async () => {
    const deleted = await call('/api/documents/delete', { kind: TABS[state.tab].kind, path: item.path });
    // An alias left pointing at a deleted item stops the content from building, so it goes with the file.
    const aliases = Object.fromEntries(Object.entries(state.catalogue.aliases).filter(([, target]) => target !== item.id));
    return { ...deleted, ...(await call('/api/aliases', { aliases })) };
  });

  if (!result) return;
  adopt(result);
  state.selected = null;
  state.draft = null;
  renderDetail();
  report(`Deleted ${item.path}.`);
}

async function create() {
  const template = TEMPLATES[state.tab]();
  const parsed = parseId(template.id);
  const name = window.prompt(`Name of the new ${TABS[state.tab].label} (letters, digits and underscores)`, parsed.name);
  if (!name) return;

  const safe = name.trim().toLowerCase().replace(/[^a-z0-9_-]+/g, '_');
  const id = `${parsed.kind}:${safe}:v1`;
  const path = `${TABS[state.tab].folder}/${safe}.v1.json`;
  const content = { ...template, id, name: name.trim() };

  const result = await act(`Creating ${id}`, async () => {
    const saved = await call('/api/documents', { kind: TABS[state.tab].kind, path, document: content, create: true });
    const aliases = { ...state.catalogue.aliases, [`${parsed.kind}:${safe}`]: id };
    return { ...saved, ...(await call('/api/aliases', { aliases })) };
  });

  if (!result) return;
  adopt(result);
  report(`${id} written to ${path}.`);
  select(path);
}

async function build() {
  const result = await act('Building', () => call('/api/build', {}));
  if (!result) return;
  adopt(result);
  banner(`Content ${result.contentHash} written to ${result.output}.`, 'ok', result.notes);
}

async function run() {
  const request = {
    mode: $('run-mode').value,
    player1: $('run-p1').value.trim() || 'random',
    player2: $('run-p2').value.trim() || 'random',
    matches: Number($('run-matches').value) || 20,
    seed: $('run-seed').value === '' ? null : Number($('run-seed').value),
  };

  // Opened here, on the click itself: a run outlasts the browser's user-activation window, so a window.open
  // after the await is an unsolicited popup and gets blocked. The tab holds a line until there is a page for it.
  const tab = window.open('', '_blank');
  if (tab) {
    tab.document.title = 'Playing…';
    const waiting = tab.document.createElement('p');
    waiting.style.font = '14px system-ui';
    waiting.textContent = 'Playing the run…';
    tab.document.body.replaceChildren(waiting);
  }

  const status = $('run-status');
  status.replaceChildren('playing...');
  const result = await act('Playing', () => call('/api/runs', request));
  if (!result) {
    status.replaceChildren();
    if (tab) tab.close();
    return;
  }

  clearBanner();
  // The link is the reliable way in: a browser that refused the tab still leaves the author one click away.
  const link = element('a', { href: result.url, target: '_blank', rel: 'noopener', textContent: `${result.id} (seed ${result.seed})` });
  status.replaceChildren(link);
  if (tab) tab.location.replace(result.url);
}

// ---------- wiring ----------

async function load() {
  const catalogue = await act('Reading the content', () => call('/api/catalogue'));
  if (!catalogue) return;
  state.catalogue = catalogue;
  renderHeader();
  renderNav();
  if (!catalogue.problems.length) clearBanner();
}

for (const tab of document.querySelectorAll('.tab')) {
  tab.addEventListener('click', () => {
    state.tab = tab.dataset.tab;
    state.selected = null;
    state.draft = null;
    state.node = null;
    renderNav();
    renderDetail();
  });
}

/** How many seeds only means something for an evaluation; one match is one match. */
function syncRunMode() {
  const evaluation = $('run-mode').value === 'evaluation';
  $('run-matches').disabled = !evaluation;
  $('run-go').textContent = evaluation ? 'Evaluate' : 'Play it';
}

$('run-mode').addEventListener('change', syncRunMode);
syncRunMode();

$('search').addEventListener('input', renderNav);
$('new').addEventListener('click', create);
$('build').addEventListener('click', build);
$('run-go').addEventListener('click', run);
$('run-panel').addEventListener('click', () => { $('run').hidden = !$('run').hidden; });

window.addEventListener('beforeunload', event => {
  if (state.dirty) event.preventDefault();
});

await load();
