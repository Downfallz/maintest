// The content studio (ADR 0015). One page over the authored content: browse it, edit it in forms that know
// the schema, cut a new version, turn things off, rebuild, and play what the change does.
// No framework and no build step, like the viewer it sits next to. Loaded as a module, so it is strict and
// scoped to itself, and the first read of the content is awaited at the top level.
//
// Where the content lives is `backend.js`'s business, not this file's (ADR 0023): the page asks for the
// catalogue, hands over a change, and asks for a build, a run or an audit. The same page is served by the
// local host and from GitHub Pages, and which backend answers is decided by where it was loaded from.

import { backendForThisPage } from './backend.js';
import { storeToken, storedToken } from './github.js';
import { aliasOfSpell, constraintsOf, entryFor, formatNumber, objectiveOf, readBalance, summarise, survey } from './balance.js';

// Not `const`: a token pasted or forgotten picks a different backend, and every call reads this at call time.
let backend = backendForThisPage();

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
  Regeneration: { amounts: ['amountPerRound'], rounds: true, stacking: 'Refresh' },
  EnergyRegeneration: { amounts: ['amountPerRound'], rounds: true, stacking: 'Refresh' },
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

const state = { catalogue: null, tab: 'spells', selected: null, draft: null, dirty: false, node: null, busy: false, runs: [], weights: null, audit: null };

/** Below this width the list and the panels are sheets over the editor rather than beside it (studio.css agrees). */
const narrow = globalThis.matchMedia('(max-width: 899px)');


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

/** One of the symbols index.html draws once; SVG lives in its own namespace, so createElement will not do. */
function icon(name) {
  const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
  const use = document.createElementNS('http://www.w3.org/2000/svg', 'use');
  use.setAttribute('href', `#i-${name}`);
  svg.setAttribute('aria-hidden', 'true');
  svg.append(use);
  return svg;
}

/** A label with a shorter reading for a phone; studio.css shows one of the two, so the button is named by one. */
function labelled(long, short) {
  return [element('span', { className: 'long', textContent: long }), element('span', { className: 'short', textContent: short })];
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

// ---------- talking to the backend ----------

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
    refreshBalancePanel();
  }
}

/** The balance sheet reads the catalogue rather than a request of its own, so a change to it is a redraw. */
function refreshBalancePanel() {
  if (!$('balance').hidden) renderBalance();
}

// ---------- the banner ----------

function banner(message, kind = 'info', problems = []) {
  const node = $('banner');
  node.hidden = false;
  node.className = kind === 'error' || kind === 'ok' ? `banner ${kind}` : 'banner';
  // An error interrupts; anything else is read when the reader gets to it.
  node.setAttribute('role', kind === 'error' ? 'alert' : 'status');
  const text = element('div', { className: 'banner-text' }, [element('div', { textContent: message })]);
  if (problems.length) {
    text.append(element('ul', {}, problems.map(problem => element('li', { textContent: problem }))));
  }
  const dismiss = element('button', { type: 'button', className: 'close', ariaLabel: 'Dismiss' }, [icon('close')]);
  dismiss.addEventListener('click', clearBanner);
  node.replaceChildren(text, dismiss);
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
  hash.classList.toggle('warn', !catalogue.contentHash);
  if (!catalogue.contentHash && catalogue.problems.length) {
    banner('The content does not build.', 'error', catalogue.problems);
  }
}

function renderNav() {
  for (const tab of document.querySelectorAll('.tab')) {
    tab.classList.toggle('selected', tab.dataset.tab === state.tab);
    tab.querySelector('.count').textContent = String(documentsOf(tab.dataset.tab).length || '');
  }

  const filter = $('search').value.trim().toLowerCase();
  const items = documentsOf(state.tab)
    .filter(item => !filter || item.id.toLowerCase().includes(filter) || (item.name || '').toLowerCase().includes(filter))
    .sort((left, right) => (left.name || left.id).localeCompare(right.name || right.id));

  $('list').replaceChildren(...items.map(item => {
    const entry = element('li', { className: [item.enabled ? '' : 'off', item.problem ? 'broken' : ''].join(' ').trim() }, [
      itemButton(item),
    ]);
    if (state.selected?.path === item.path) entry.classList.add('selected');
    return entry;
  }));

  if (!items.length) {
    $('list').replaceChildren(element('li', { className: 'none', textContent: filter ? 'Nothing matches.' : 'Nothing here yet.' }));
  }

  $('new').querySelector('span').textContent = `New ${TABS[state.tab].label}`;
}

/**
 * One row of the list: the name, and beside it what a reader scanning for a number wants -- the class and cost
 * of a spell and what it does, the health and initiative of a creature, the size of a tree. The versioned id
 * is the tooltip, and what the filter box also matches.
 */
function itemButton(item) {
  const parsed = parseId(item.id);
  const button = element('button', { type: 'button', className: 'item', title: item.id, ariaCurrent: state.selected?.path === item.path ? 'true' : null }, [
    element('span', { className: 'name' }, [
      item.name || item.id,
      parsed ? element('span', { className: 'version mono', textContent: `v${parsed.version}` }) : null,
      item.enabled ? null : element('span', { className: 'tag', textContent: 'off' }),
      item.problem ? element('span', { className: 'tag bad', textContent: 'broken' }) : null,
    ]),
    ...glance(item),
  ]);
  button.addEventListener('click', () => select(item.path));
  return button;
}

/**
 * The figure and the line under the name. A broken file has neither: its document is the JSON that failed to
 * parse into its DTO, so nothing below can assume a shape, and the line says what is wrong instead.
 */
function glance(item) {
  if (item.problem) {
    return [element('span', { className: 'meta bad', textContent: item.problem })];
  }

  const doc = item.document || {};
  if (item.kind === 'Spell') {
    const figure = asArray(doc.effects).map(effectSummary).filter(Boolean).join(', ');
    const cost = typeof doc.energyCost === 'number' ? `${doc.energyCost} energy` : null;
    return [
      element('span', { className: 'figure', textContent: figure }),
      element('span', { className: 'meta', textContent: [doc.creatureClass, doc.spellType, cost].filter(Boolean).join(' \u00b7 ') }),
    ];
  }

  if (item.kind === 'Creature') {
    return [
      element('span', { className: 'figure', textContent: `${doc.baseHealth ?? '?'} hp` }),
      element('span', { className: 'meta', textContent: [doc.creatureClass, `${doc.baseInitiative ?? '?'} initiative`, `${percent(Number(doc.baseCriticalChance) || 0)} crit`].join(' \u00b7 ') }),
    ];
  }

  if (item.kind === 'TalentTree') {
    let nodes = 0;
    let spells = 0;
    walkNodes(doc.root, node => { nodes += 1; spells += asArray(node.spells).length; });
    return [
      element('span', { className: 'figure', textContent: `${nodes} nodes` }),
      element('span', { className: 'meta', textContent: `${spells} spells taught` }),
    ];
  }

  return [element('span', { className: 'meta mono', textContent: item.path })];
}

/** What one effect comes to, in a word: what a list row has room for. */
function effectSummary(effect) {
  if (!effect || typeof effect !== 'object') return '';
  const rounds = effect.permanent ? '' : ` for ${effect.durationRounds ?? 1}r`;
  switch (effect.kind) {
    case 'Damage': return `${effect.amount} dmg`;
    case 'Heal': return `${effect.amount} heal`;
    case 'EnergyGain': return `+${effect.amount} energy`;
    case 'Bleed': return `${effect.amountPerRound}/r bleed`;
    case 'Regeneration': return `${effect.amountPerRound}/r regen`;
    case 'EnergyRegeneration': return `+${effect.amountPerRound}/r energy`;
    case 'Stun': return `stun ${effect.durationRounds ?? 1}r`;
    case 'DefenseBuff': return `+${effect.amount} def${rounds}`;
    case 'InitiativeDebuff': return `-${effect.amount} init${rounds}`;
    default: return effect.kind || '';
  }
}

function select(path, node = null) {
  const found = findDocument(path);
  if (!found) {
    state.selected = null;
    state.draft = null;
    state.node = null;
    renderNav();
    renderDetail();
    return;
  }

  const changed = state.selected?.path !== path;
  state.tab = found.tab;
  state.selected = found.item;
  state.draft = clone(found.item.document);
  state.dirty = false;
  // The draft is a copy, so a node handed in from the catalogue is found again in it by its code.
  state.node = node ? nodeNamed(state.draft.root, node.code) : null;
  renderNav();
  renderDetail();
  closeNav();
  // A new item starts at its title; a save re-selects the same one and must not throw the reader to the top.
  if (changed) globalThis.scrollTo({ top: 0 });
}

function nodeNamed(root, code) {
  let found = null;
  walkNodes(root, node => { if (!found && node.code === code) found = node; });
  return found;
}

// ---------- form building blocks ----------

let fieldSequence = 0;

function fields(rows, { single = false } = {}) {
  const grid = element('div', { className: single ? 'fields single' : 'fields' });
  for (const [text, control] of rows) {
    const label = element('label', { textContent: text });
    // The label names the first control it is over, so tapping the word focuses the box, and a screen reader
    // reads the word with the box. A list of rows has no single box to name, and gets no `for`.
    const target = ['INPUT', 'SELECT', 'TEXTAREA'].includes(control.tagName) ? control : control.querySelector('.inline > input, .inline > select');
    if (target) {
      target.id ||= `field-${++fieldSequence}`;
      label.htmlFor = target.id;
    }
    grid.append(element('div', { className: 'field' }, [label, control]));
  }
  return grid;
}

function markDirty() {
  state.dirty = true;
  const flag = $('dirty-flag');
  if (flag) {
    flag.textContent = 'Unsaved changes';
    flag.closest('.actions')?.classList.add('dirty');
  }

  // Every control ends here, which makes it the one place a reading of the draft can be kept honest: a band
  // still showing the damage from before the keystroke is worse than no band. The panel surveys the whole
  // catalogue and redraws only while it is open, so the spell being edited cannot read one way on its own
  // sheet and another in the roll-up.
  refreshBalanceStrip();
  refreshBalancePanel();
}

function textBox(target, key, { placeholder = '' } = {}) {
  const input = element('input', { type: 'text', value: target[key] ?? '', placeholder });
  input.addEventListener('input', () => { target[key] = input.value; markDirty(); });
  return input;
}

function numberBox(target, key, { step = 1, min = null, onChange = null } = {}) {
  const input = element('input', { type: 'number', step, value: target[key] ?? 0, inputMode: step < 1 ? 'decimal' : 'numeric' });
  if (min !== null) input.min = min;
  input.addEventListener('input', () => {
    target[key] = input.value === '' ? null : Number(input.value);
    markDirty();
    onChange?.();
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
    view.replaceChildren(overview());
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

  const parts = [element('div', { className: 'detail-head' }, [header(item), actions(item)])];
  if (item.problem) {
    parts.push(element('div', { className: 'banner error', textContent: `${item.path}: ${item.problem}` }));
  }

  if (state.tab === 'spells') parts.push(spellEditor(), usedBy(item));
  if (state.tab === 'creatures') parts.push(creatureEditor());
  if (state.tab === 'talentTrees') parts.push(treeEditor());

  view.replaceChildren(...parts);
}

function header(item) {
  // One tap back to where this came from: the list, on a phone, where it is a sheet; the overview on a desk,
  // where the list never left.
  const back = element('button', { type: 'button', className: 'back', ariaLabel: 'Back to the list', title: 'Back to the list' }, [icon('back')]);
  back.addEventListener('click', () => { if (narrow.matches) openNav(); else select(null); });
  return element('div', { className: 'title' }, [
    back,
    element('div', { className: 'title-text' }, [
      element('h2', { textContent: state.draft.name || item.id }),
      element('div', { className: 'meta' }, [
        element('span', { className: 'badge mono', textContent: item.id }),
        element('span', { className: 'muted mono path', textContent: item.path }),
      ]),
    ]),
  ]);
}

/**
 * The four actions, loudest last: Save is the one filled button, the two that change what is saved sit beside
 * it, and Delete is a quiet icon at the other end, so the thumb that reaches for Save never lands on it.
 */
function actions(item) {
  const enabled = state.draft.enabled !== false;
  const deleteButton = element('button', { type: 'button', className: 'button ghost danger', ariaLabel: 'Delete', title: 'Delete' }, [
    icon('trash'),
    element('span', { className: 'long', textContent: 'Delete' }),
  ]);
  deleteButton.addEventListener('click', () => remove(item));
  const next = element('button', { type: 'button', className: 'button next', title: 'Save as next version' }, labelled('Save as next version', 'Next version'));
  next.addEventListener('click', saveAsNextVersion);
  return element('div', { className: `actions${state.dirty ? ' dirty' : ''}` }, [
    element('span', { id: 'dirty-flag', className: 'dirty-flag', textContent: state.dirty ? 'Unsaved changes' : '' }),
    element('span', { className: 'spacer' }),
    deleteButton,
    miniButton(enabled ? 'Disable' : 'Enable', () => setEnabled(!enabled), 'button'),
    next,
    miniButton('Save', save, 'button primary'),
  ]);
}

// ---------- the overview ----------

/**
 * What the content is, on one screen: each creature with its numbers, what it starts with, and its talent
 * tree drawn as a tree, every spell a chip that opens it and every node a tap into the tree editor. What no
 * creature is on comes after. It is what the page opens on, and what nothing-selected shows.
 */
function overview() {
  const creatures = documentsOf('creatures');
  const trees = documentsOf('talentTrees');
  const spells = documentsOf('spells');
  const off = spells.filter(spell => !spell.enabled).length;
  const view = element('div', { className: 'overview' });

  if (!creatures.length && !trees.length && !spells.length) {
    view.append(element('div', { className: 'empty' }, [
      icon('sparkle'),
      element('h2', { textContent: 'Nothing here yet' }),
      element('p', { textContent: 'Create a creature, a spell or a talent tree from the list, and it shows up here.' }),
      browseButton('Open the list'),
    ]));
    return view;
  }

  const offNote = off ? ` (${off} off)` : '';
  view.append(element('div', { className: 'overview-head' }, [
    element('h2', { textContent: 'Overview' }),
    element('p', { className: 'muted', textContent: `${creatures.length} creature${creatures.length === 1 ? '' : 's'}, ${spells.length} spells${offNote}, ${trees.length} talent tree${trees.length === 1 ? '' : 's'}. Tap anything to open it.` }),
  ]));

  const covered = new Set();
  for (const creature of creatures) view.append(creatureCard(creature, covered));
  const orphans = trees.filter(tree => !covered.has(tree.path));
  if (orphans.length) {
    view.append(element('h3', { className: 'section', textContent: creatures.length ? 'Talent trees no creature is on' : 'Talent trees' }));
    for (const tree of orphans) view.append(treeCard(tree));
  }

  return view;
}

/** Opens the list -- a sheet on a phone; on a desk it is already there, so the filter box takes the focus. */
function browseButton(label) {
  const button = element('button', { type: 'button', className: 'button' }, [icon('list'), element('span', { textContent: label })]);
  button.addEventListener('click', () => { if (narrow.matches) openNav(); else $('search').focus(); });
  return button;
}

function creatureCard(creature, covered) {
  if (creature.problem) return brokenCard(creature, 'creature');
  const doc = creature.document || {};
  const tree = documentsOf('talentTrees').find(candidate => candidate.id === resolveReference(doc.talentTreeId));
  if (tree) covered.add(tree.path);

  const card = element('div', { className: `card overview-card${creature.enabled ? '' : ' off'}` });
  card.append(cardTitle(creature, doc.creatureClass));
  card.append(element('div', { className: 'pills' }, [
    pill(doc.baseHealth, 'hp'),
    pill(doc.baseEnergy, 'energy'),
    pill(doc.baseDefense, 'def'),
    pill(doc.baseInitiative, 'init'),
    pill(percent(Number(doc.baseCriticalChance) || 0), 'crit'),
  ]));

  const starting = asArray(doc.startingSpellIds);
  card.append(
    element('div', { className: 'label', textContent: starting.length ? 'Starts with' : 'Starts with nothing' }),
    element('div', { className: 'chips' }, starting.map(spellLink)),
  );

  if (tree) {
    card.append(element('div', { className: 'label' }, ['Talent tree ', treeLink(tree)]), compactTree(tree));
  } else if (doc.talentTreeId) {
    card.append(element('div', { className: 'label warn', textContent: `Talent tree ${doc.talentTreeId} is not in the catalogue.` }));
  } else {
    card.append(element('div', { className: 'label', textContent: 'No talent tree.' }));
  }

  return card;
}

function treeCard(tree) {
  if (tree.problem) return brokenCard(tree, 'talent tree');
  const card = element('div', { className: `card overview-card${tree.enabled ? '' : ' off'}` });
  card.append(cardTitle(tree, tree.enabled ? 'talent tree' : 'talent tree, off'), compactTree(tree));
  return card;
}

/** A file that did not parse: the way in, and what is wrong with it, and nothing read from its document. */
function brokenCard(item, kind) {
  return element('div', { className: 'card overview-card broken' }, [
    cardTitle(item, `${kind}, does not parse`),
    element('div', { className: 'label warn', textContent: `${item.path}: ${item.problem}` }),
  ]);
}

/** The name as the way in, and beside it what kind of thing it is. */
function cardTitle(item, kind) {
  const open = miniButton(item.name || item.id, () => select(item.path), 'link title-link');
  return element('h3', {}, [open, element('span', { className: 'muted', textContent: kind })]);
}

function treeLink(tree) {
  return miniButton(tree.name || tree.id, () => select(tree.path), 'link');
}

function pill(value, unit) {
  return element('span', { className: 'pill' }, [element('b', { textContent: String(value ?? '?') }), ` ${unit}`]);
}

/** The tree with nothing to edit on it: a code per node, the spells it teaches, and the lines between. */
function compactTree(tree) {
  const list = element('ul', { className: 'tree compact' });
  const draw = node => {
    const pick = element('button', { type: 'button', className: 'node-pick', title: node.name || node.code }, [
      element('span', { className: 'code', textContent: node.code || '(no code)' }),
      node.name && node.name !== node.code ? element('span', { className: 'muted', textContent: node.name }) : null,
    ]);
    pick.addEventListener('click', () => select(tree.path, node));
    const card = element('div', { className: 'node' }, [element('div', { className: 'head' }, [pick])]);
    const taught = asArray(node.spells).filter(spell => spell?.id);
    if (taught.length) {
      card.append(element('div', { className: 'chips' }, taught.map(spell => spellLink(spell.id))));
    }
    const entry = element('li', {}, [card]);
    const children = asArray(node.children).filter(child => child && typeof child === 'object');
    if (children.length) entry.append(element('ul', {}, children.map(draw)));
    return entry;
  };
  const root = tree.document?.root;
  list.append(draw(root && typeof root === 'object' ? root : emptyNode('Root', 'Root')));
  return list;
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
    ['Spell initiative', numberBox(draft, 'initiative', { min: 0 })],
    ['Energy cost', numberBox(draft, 'energyCost', { min: 0 })],
    ['Critical chance bonus', critField(draft)],
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
  // The strip sits between the spell's own numbers and its effects: it reads both, and what it says about a
  // number is worth knowing before changing the one under it rather than after scrolling past everything.
  return element('div', {}, [card, balanceStrip(), effects]);
}

/** A single target takes no count; a multi target takes at least two, which is what the engine will accept. */
/** The bonus and, beside it, what it comes to — rewritten as it is typed, or it would report the old value. */
function critField(draft) {
  let rate = effectiveCrit(draft);
  const redraw = () => {
    const next = effectiveCrit(draft);
    rate.replaceWith(next);
    rate = next;
  };

  return element('div', { className: 'inline' }, [
    numberBox(draft, 'criticalChance', { step: 0.01, min: 0, onChange: redraw }),
    rate,
  ]);
}

/**
 * What a cast of this spell actually crits at. A spell's critical chance is a bonus added to the creature's
 * own (`ResolutionRules`), so the field alone reads as "never crits" when the creature behind it does.
 */
function effectiveCrit(draft) {
  const bases = [...new Set(documentsOf('creatures').map(creature => Number(creature.document.baseCriticalChance) || 0))].sort((a, b) => a - b);
  const bonus = Number(draft.criticalChance) || 0;
  if (!bases.length) return element('span', { className: 'muted', textContent: 'added to the creature\u2019s own chance' });
  const rates = [...new Set(bases.map(base => percent(Math.min(1, base + bonus))))];
  const own = bases.length === 1
    ? `a creature's own ${percent(bases[0])}`
    : `each creature's own, ${percent(bases[0])} to ${percent(bases.at(-1))}`;
  return element('span', { className: 'muted', textContent: `added to ${own}, so a cast crits at ${rates.join(' or ')}` });
}

const percent = value => `${(value * 100).toFixed(1)} %`;

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
    ['Initiative', element('div', { className: 'inline' }, [
      numberBox(draft, 'baseInitiative', { min: 0 }),
      element('span', { className: 'muted', textContent: 'at spawn; unlocking a spell raises it' }),
    ])],
    ['Critical chance', element('div', { className: 'inline' }, [
      numberBox(draft, 'baseCriticalChance', { step: 0.01, min: 0 }),
      element('span', { className: 'muted', textContent: 'its own; a spell adds a bonus to it' }),
    ])],
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
  // The node itself is the tap: it opens in the editor below, which then scrolls into reach on a phone.
  // Its own buttons show only on the node being edited, so the tree stays a tree and not a wall of buttons.
  const pick = element('button', { type: 'button', className: 'node-pick', ariaPressed: selected ? 'true' : 'false' }, [
    element('span', { className: 'code', textContent: node.code || '(no code)' }),
    element('span', { className: 'muted', textContent: node.name || '' }),
  ]);
  pick.addEventListener('click', () => {
    state.node = node;
    redraw();
    refreshNodeEditor(redraw);
    const editor = $('node-editor');
    if (editor && editor.getBoundingClientRect().top > globalThis.innerHeight * 0.6) editor.scrollIntoView({ behavior: 'smooth', block: 'start' });
  });
  const head = element('div', { className: 'head' }, [pick]);

  if (selected) {
    head.append(miniButton('Add child', () => {
      node.children = [...(node.children || []), emptyNode(`Node${(node.children || []).length + 1}`, 'New node')];
      markDirty();
      redraw();
    }));
  }

  if (selected && parent) {
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
    // `element` drops what is not there; `append` would write the word "null" into the tree.
    card.append(...[balanceFold(node.spells.map(spell => spell.id), selected)].filter(Boolean));
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
      element('p', { className: 'muted', textContent: 'Tap a node above to edit its code, its prerequisites and the spells it teaches.' }),
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
          // The verdict where the reference is picked: what this node is about to teach, in a word.
          balanceTag(spell.id),
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
  ], { single: true }));
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

// ---------- the balance knobs ----------

// What a tuning pass may change about a spell, and what the spell is for (`data/balance/README.md`). The page
// reads it and never writes it: the file is authoring metadata the data builder does not even see, and the
// checks below are `check-knobs`' own, surfaced where an edit causes them instead of only on the command line.
//
// The reading itself is `balance.js`, which knows nothing about the DOM; everything here is how it is drawn.

/** Read at draw time, never cached: a save, a build or a reload replaces the catalogue under the page. */
const knobsHere = () => readBalance(state.catalogue);

/** The one discreet line where the knobs would have gone. A host that publishes none is not a broken sheet. */
const noKnobs = why => element('p', { className: 'muted balance-none', textContent: why });

/**
 * One spell's entry, found by the **unversioned alias** that points at the spell rather than by its versioned
 * id, so cutting a `:v2` keeps the entry instead of orphaning it. `document` is what is on screen -- the draft
 * in the editor, the catalogue's copy anywhere else -- because the value a knob is judged against is the one
 * being authored, not the one that was on disk when the page loaded.
 */
function balanceOf(balance, id, document) {
  const alias = aliasOfSpell(id, state.catalogue?.aliases || {});
  const entry = alias ? entryFor(balance, alias) : null;
  return entry ? { alias, summary: summarise(entry, document) } : { alias, summary: null };
}

/** Where a value stands in its band, in words: the part of the strip that is read rather than looked at. */
function whereLabel(reading) {
  switch (reading.at) {
    case 'min': return 'at its minimum';
    case 'max': return 'at its maximum';
    case 'below': return 'below its band';
    case 'above': return 'above its band';
    case 'inside': return reading.room ? `${reading.room.down} down, ${reading.room.up} up` : 'inside its band';
    default: return 'nothing to read';
  }
}

/**
 * The band as a bar: the track is what the knob may reach, the mark is where the content sits on it today. A
 * value outside its bounds is drawn against the edge it left, in the colour of a problem, because clamping it
 * silently into the track is exactly the lie this view exists to prevent.
 */
function knobBand(reading) {
  if (reading.position === null) {
    // Labelled like every other band: a reader who is not looking at it gets the same sentence the hover does.
    return element('div', {
      className: 'band empty',
      role: 'img',
      ariaLabel: 'No band to draw: this knob addresses no number.',
      title: 'No band to draw: this knob addresses no number.',
    });
  }

  const mark = element('span', { className: `mark${reading.at === 'below' || reading.at === 'above' ? ' out' : ''}` });
  mark.style.left = `${(reading.position * 100).toFixed(2)}%`;
  const bounds = `${formatNumber(reading.minimum)} to ${formatNumber(reading.maximum)}`;
  return element('div', {
    className: 'band',
    role: 'img',
    // The bar carries the whole reading for anyone not looking at it; the text beside it repeats the numbers.
    ariaLabel: `${formatNumber(reading.value)} in a band of ${bounds}, ${whereLabel(reading)}`,
  }, [mark]);
}

/** One knob: what it addresses, what the content carries, where that sits, and what is wrong with the pair. */
function knobRow(reading) {
  const row = element('div', { className: `knob tone-${reading.tone}` }, [
    element('code', { className: 'pointer', textContent: reading.path || '(no pointer)' }),
    element('span', { className: 'value', textContent: reading.value === null ? '—' : formatNumber(reading.value) }),
    knobBand(reading),
    element('span', { className: 'bounds' }, [
      reading.position === null ? 'no band' : `${formatNumber(reading.minimum)}–${formatNumber(reading.maximum)} · step ${formatNumber(reading.step)}`,
      element('span', { className: 'where', textContent: whereLabel(reading) }),
    ]),
  ]);
  for (const problem of reading.problems) {
    row.append(element('p', { className: 'problem', textContent: problem.message }));
  }

  return row;
}

/**
 * The strip on a spell's sheet. It is a `details` so a phone keeps the form's own fields within reach: closed,
 * it still shows the intent and the verdict, which is the part a number cannot say; open, it adds the
 * invariants and every knob's band. A desk has the room, so it opens there.
 */
function balanceStrip() {
  const holder = element('details', { className: 'card balance', id: 'balance-strip', open: !narrow.matches });
  fillBalanceStrip(holder);
  return holder;
}

/** Redrawn as the spell is typed, the way the critical chance reading is: a stale band is a wrong band. */
function refreshBalanceStrip() {
  const holder = $('balance-strip');
  if (holder) fillBalanceStrip(holder);
}

function fillBalanceStrip(holder) {
  const answer = knobsHere();
  const title = element('summary', {}, [element('span', { className: 'what', textContent: 'Balance' })]);
  if (!answer.ok) {
    // Discreet on purpose: a page served by an older deployment shows one folded line here, not a wall about
    // a file it was never given. The whole sentence is one tap away for whoever wonders why. Whether it is
    // folded is the reader's -- this runs again on every keystroke, so setting it here would snap shut the
    // explanation they just opened, and today this is the branch every host takes.
    holder.className = 'card balance quiet';
    title.append(element('span', { className: 'headline', textContent: 'not published by this host' }));
    holder.replaceChildren(title, noKnobs(answer.why));
    return;
  }

  // A file that did not survive parsing holds whatever it holds, so every pointer would read as addressing
  // nothing and the strip would blame the knobs for the file. `survey` leaves these alone for the same reason.
  if (state.selected?.problem) {
    holder.className = 'card balance quiet';
    title.append(element('span', { className: 'headline', textContent: 'not read for this file' }));
    holder.replaceChildren(title, element('p', { className: 'muted', textContent: 'This file did not parse as a spell, so there are no numbers to read its knobs against. Fix the file and the bands come back.' }));
    return;
  }

  // The entry belongs to the spell being edited, so the alias is looked up by the id it was opened under and
  // the numbers are read from the draft: typing a new id in the box must not make the entry vanish mid-word.
  const { alias, summary } = balanceOf(answer.balance, state.selected?.id ?? state.draft.id, state.draft);
  if (!summary) {
    holder.className = 'card balance tone-bad';
    holder.replaceChildren(title, missingEntry(alias));
    return;
  }

  title.append(element('span', { className: 'headline', textContent: summary.headline }));
  // A span rather than a paragraph: a summary holds phrasing content, and the stylesheet makes it a block.
  title.append(element('span', {
    className: `intent${summary.intent ? '' : ' absent'}`,
    textContent: summary.intent || 'No intent: nothing says what these numbers are for.',
  }));

  const body = [];
  // Off means out of the build, and `validate` reads the entry of no spell that left it. The reading stays --
  // it is what says whether turning the spell back on would owe anyone work -- but it is not a disagreement
  // with anything today, so it does not wear the colour of one.
  const off = state.draft.enabled === false;
  if (off) {
    body.push(element('p', { className: 'muted', textContent: 'This spell is off, so it is out of the build and check-knobs does not read its entry. Nothing below is failing anything; it is what would be owed if the spell came back.' }));
  }

  if (summary.keep.length) {
    body.push(element('div', { className: 'label', textContent: 'Whatever the numbers do' }));
    body.push(element('ul', { className: 'keep' }, summary.keep.map(kept => element('li', { textContent: kept }))));
  }

  if (summary.note) body.push(element('p', { className: 'note', textContent: summary.note }));
  for (const problem of summary.problems) body.push(element('p', { className: 'problem', textContent: problem.message }));
  body.push(element('div', { className: 'knobs' }, summary.knobs.length
    ? summary.knobs.map(knobRow)
    : [element('p', { className: 'muted', textContent: 'No knob: every number of this spell is its identity, and a tuning pass may move none of it.' })]));
  body.push(element('p', { className: 'muted source' }, [
    'Read from ',
    element('code', { textContent: alias }),
    ' in data/balance/knobs.json. The studio does not write it.',
  ]));

  holder.className = off ? 'card balance quiet' : `card balance tone-${summary.tone}`;
  holder.replaceChildren(title, ...body);
}

/**
 * A spell the knobs file says nothing about. Which of the three reasons it is matters: an enabled spell with no
 * entry is what `check-knobs` fails on, a spell that is off is owed none, and a spell no alias points at cannot
 * be named by the file at all, whatever anyone writes in it.
 */
function missingEntry(alias) {
  if (!alias) {
    return element('p', { className: 'problem', textContent: 'No alias points at this spell, so the knobs file has no name to key an entry by. Aliases live in data/aliases.json.' });
  }

  if (state.draft.enabled === false) {
    return element('p', { className: 'muted', textContent: `No entry for ${alias}. This spell is off, so it is out of the build and nothing tunes it.` });
  }

  return element('p', { className: 'problem', textContent: `No entry for ${alias}: nothing says what this spell is for or which of its numbers may move. check-knobs fails on enabled content with no entry.` });
}

/** The spells one talent node offers, each with what the balance file says about it. */
function briefsOf(references) {
  const answer = knobsHere();
  if (!answer.ok) return null;
  return references.map(reference => {
    const spell = spellNamed(reference);
    // A file that did not parse is left alone here the way `survey` leaves it alone: its numbers are whatever
    // the broken file held, so reading knobs against them would report the breakage as a balance problem.
    const broken = Boolean(spell?.problem);
    return { reference, spell, broken, ...balanceOf(answer.balance, spell?.id || reference, spell?.document || {}) };
  });
}

/**
 * What the balance file says about the spells a node teaches, folded away unless the node is the one being
 * edited. A tier is the set offered at one depth, which is several nodes, so reading one means opening a fold
 * or two rather than leaving the tree; the summary line carries the verdict, so a closed fold still shows
 * trouble. Nothing is drawn at all when the host publishes no knobs: the sheet says that once, loudly enough.
 */
function balanceFold(references, open) {
  const briefs = briefsOf(references);
  if (!briefs?.length) return null;
  const wrong = briefs.filter(brief => !brief.broken && (!brief.summary || brief.summary.tone === 'bad')).length;
  const tone = wrong ? 'bad' : 'ok';
  const verdict = wrong ? `${wrong} of ${briefs.length} to look at` : 'all inside their bands';

  return element('details', { className: `balance-fold tone-${tone}`, open }, [
    element('summary', {}, [
      element('span', { className: 'what', textContent: 'Balance' }),
      element('span', { className: 'headline', textContent: `${briefs.length} spell${briefs.length === 1 ? '' : 's'} · ${verdict}` }),
    ]),
    ...briefs.map(brief),
  ]);
}

/** One spell of a node, compact: what it is for in a line or two, and its knobs as bands and nothing else. */
function brief({ reference, spell, alias, summary, broken }) {
  if (broken) {
    return element('div', { className: 'brief' }, [
      element('div', { className: 'brief-head' }, [spellLink(reference)]),
      element('p', { className: 'muted', textContent: 'This file did not parse, so there are no numbers to read its knobs against.' }),
    ]);
  }

  if (!summary) {
    return element('div', { className: 'brief tone-bad' }, [
      element('div', { className: 'brief-head' }, [spellLink(reference)]),
      element('p', { className: 'problem', textContent: alias ? `No entry for ${alias} in the knobs file.` : 'No alias points at this spell, so the knobs file cannot name it.' }),
    ]);
  }

  return element('div', { className: `brief tone-${summary.tone}` }, [
    element('div', { className: 'brief-head' }, [
      spellLink(reference),
      element('span', { className: 'headline', textContent: summary.headline }),
      spell?.enabled === false ? element('span', { className: 'tag', textContent: 'off' }) : null,
    ]),
    element('p', {
      className: `intent${summary.intent ? '' : ' absent'}`,
      textContent: summary.intent || 'No intent: nothing says what these numbers are for.',
    }),
    element('div', { className: 'mini-knobs' }, summary.knobs.map(reading => element('div', { className: `mini-knob tone-${reading.tone}`, title: `${reading.path}: ${reading.value === null ? 'no number' : formatNumber(reading.value)}, ${whereLabel(reading)}` }, [
      element('code', { textContent: reading.path }),
      knobBand(reading),
    ]))),
  ]);
}

/** Beside a spell in the node editor: the verdict in a word, where the reference itself is picked. */
function balanceTag(reference) {
  const answer = knobsHere();
  if (!answer.ok) return null;
  const spell = spellNamed(reference);
  if (spell?.problem) return null;
  const { alias, summary } = balanceOf(answer.balance, spell?.id || reference, spell?.document || {});
  if (!summary) {
    return element('span', { className: 'balance-tag tone-bad', textContent: alias ? 'no balance entry' : 'no alias, so no entry' });
  }

  // Terse, because it shares a row with a picker: the whole reading is on hover and in the fold above.
  const count = summary.problems.length + summary.knobs.filter(knob => knob.tone === 'bad').length;
  const label = summary.tone === 'bad'
    ? `${count} to look at`
    : `${summary.knobs.length} knob${summary.knobs.length === 1 ? '' : 's'}`;
  return element('span', { className: `balance-tag tone-${summary.tone}`, textContent: label, title: `${summary.headline} — ${summary.intent}` });
}

// ---------- the balance panel ----------

/**
 * The file's own half of the story, which belongs to no one spell: what balanced means as a score, what a
 * candidate may never do, and how much of the catalogue the file covers at all. It reads the catalogue the page
 * already holds, so it costs no request and moves with the edits on screen.
 */
function renderBalance() {
  const body = $('balance-body');
  const answer = knobsHere();
  if (!answer.ok) {
    body.replaceChildren(noKnobs(answer.why));
    return;
  }

  const balance = answer.balance;
  // Filtered rather than handed straight to `replaceChildren`, which is not `element` and writes the word
  // "null" where it is given one. `about` is the file's own prose about itself, and a file without it gets
  // no empty paragraph.
  const about = typeof balance.about === 'string' && balance.about.trim() ? balance.about : null;
  body.replaceChildren(...[
    balanceCoverage(balance),
    balanceObjective(balance),
    balanceConstraints(balance),
    about ? element('p', { className: 'hint', textContent: about }) : null,
  ].filter(Boolean));
}

/**
 * The spell rows to survey: the catalogue's, with the draft standing in for the one being edited.
 *
 * `state.catalogue` keeps the document as it was read until a save adopts a new one, so surveying it alone
 * would report the spell on disk while its own strip reports the spell on screen -- the same number, read two
 * ways, on one page. The draft is what the author is looking at, so it is what the roll-up counts.
 */
function surveyedSpells() {
  const rows = documentsOf('spells');
  if (!state.draft || state.tab !== 'spells' || !state.selected) return rows;
  return rows.map(row => (row.path === state.selected.path ? { ...row, document: state.draft } : row));
}

/**
 * A finding's way into the spell it is about, or its name and nothing more.
 *
 * `select('')` clears the selection rather than opening anything (see `select`), so a row whose path did not
 * survive reading would throw the reader out of the editor on a click that looked like a link.
 */
function openSpell(path, label) {
  return path ? miniButton(label, () => select(path), 'link') : element('span', { textContent: label });
}

/** The file against the content it describes: the same reading `check-knobs` prints, on what is on screen. */
function balanceCoverage(balance) {
  const rolled = survey(balance, surveyedSpells(), state.catalogue?.aliases || {});
  const clean = !rolled.uncovered.length && !rolled.unresolved.length && !rolled.flagged.length
    && !rolled.constraintProblems.length;
  const block = element('div', {}, [
    element('div', { className: 'audit-line' }, [
      element('span', { textContent: `${rolled.covered} of ${rolled.enabled} enabled spells have an entry` }),
      element('span', { textContent: `${rolled.entries} entries, ${rolled.knobs} knobs` }),
      // Most of this catalogue is off, and those entries are kept on purpose: they are the only thing left
      // saying what a spell was for while it waits for a rule to come back.
      rolled.resting ? element('span', { textContent: `${rolled.resting} for spells that are off` }) : null,
      rolled.uncovered.length ? element('span', { className: 'warn', textContent: `${rolled.uncovered.length} with no entry` }) : null,
      rolled.flagged.length ? element('span', { className: 'warn', textContent: `${rolled.flagged.length} the file disagrees with` }) : null,
      rolled.unresolved.length ? element('span', { className: 'warn', textContent: `${rolled.unresolved.length} naming nothing` }) : null,
      rolled.constraintProblems.length ? element('span', { className: 'warn', textContent: `${rolled.constraintProblems.length} constraint${rolled.constraintProblems.length === 1 ? '' : 's'} checking nothing` }) : null,
      clean ? element('span', { textContent: 'every enabled spell is covered' }) : null,
    ]),
  ]);

  if (clean) {
    block.append(element('p', { className: 'hint', textContent: 'Nothing disagrees: every enabled spell has an entry with an intent, every pointer addresses a number, and every number the content carries sits inside its own band.' }));
    return block;
  }

  const findings = element('ul', { className: 'findings' });
  for (const spell of rolled.uncovered) {
    findings.append(element('li', {}, [
      openSpell(spell.path, spell.name || spell.id),
      element('span', { textContent: ` — enabled content with no entry in the knobs file${spell.alias ? '' : ', and no alias to key one by'}.` }),
    ]));
  }

  // A constraint naming a spell nothing resolves to is the quietest hole in the file: it does not fail, it
  // simply stops guarding anything, so it belongs at the top of what there is to look at.
  for (const problem of rolled.constraintProblems) {
    findings.append(element('li', {}, [
      element('span', { className: 'mono', textContent: problem.alias }),
      element('span', { textContent: ` — named by ${problem.constraint}, and not a spell any alias resolves to, so the constraint checks nothing.` }),
    ]));
  }

  for (const alias of rolled.unresolved) {
    findings.append(element('li', {}, [
      element('span', { className: 'mono', textContent: alias }),
      element('span', { textContent: ' — an entry for a spell no alias resolves to.' }),
    ]));
  }

  for (const spell of rolled.flagged) {
    const item = element('li', {}, [openSpell(spell.path, spell.name || spell.alias)]);
    item.append(element('ul', {}, spell.problems.map(problem => element('li', {}, [
      problem.path ? element('code', { textContent: `${problem.path} ` }) : null,
      element('span', { textContent: problem.message }),
    ]))));
    findings.append(item);
  }

  block.append(findings);
  return block;
}

/** What balanced means: which evaluations are played, and the band each metric they report should land in. */
function balanceObjective(balance) {
  const objective = objectiveOf(balance);
  const block = element('div', {}, [element('h3', { className: 'section', textContent: 'The objective' })]);
  if (objective.seeds) block.append(element('p', { className: 'hint', textContent: `Played on ${objective.seeds}. ${objective.score}` }));
  for (const evaluation of objective.evaluations) {
    block.append(element('p', { className: 'hint' }, [
      element('code', { textContent: evaluation.name }),
      element('span', { textContent: ` — ${evaluation.p1} against ${evaluation.p2}. ${evaluation.reads}` }),
    ]));
  }

  if (!objective.targets.length) {
    block.append(element('p', { className: 'hint', textContent: 'No target, so every candidate scores the same and the search has nothing to climb.' }));
    return block;
  }

  // A block each rather than a table: every target carries a paragraph saying why its band is where it is,
  // and a paragraph in a cell is a row nobody reads on a phone.
  for (const target of objective.targets) {
    block.append(element('div', { className: 'target' }, [
      element('div', { className: 'constraint-head' }, [
        element('code', { textContent: target.metric }),
        // A target reading an evaluation nobody plays is a term silently missing from every score.
        element('span', { className: target.declared ? 'muted mono' : 'warn mono', textContent: target.declared ? `read from ${target.on}` : `reads ${target.on}, which the objective does not declare` }),
      ]),
      element('div', { className: 'pills' }, [
        pill(target.band, 'band'),
        pill(formatNumber(target.scale), 'scale'),
        pill(formatNumber(target.weight), 'weight'),
      ]),
      element('p', { className: 'muted', textContent: target.why }),
    ]));
  }

  return block;
}

/** The hard rules. A candidate that breaks one is not scored at all, so an author is owed the list. */
function balanceConstraints(balance) {
  const block = element('div', {}, [element('h3', { className: 'section', textContent: 'The constraints' })]);
  for (const constraint of constraintsOf(balance)) {
    const card = element('div', { className: `constraint${constraint.enabled ? '' : ' off'}` }, [
      element('div', { className: 'constraint-head' }, [
        element('code', { textContent: constraint.name }),
        element('span', { className: `tag ${constraint.enabled ? 'on' : ''}`, textContent: constraint.enabled ? 'enforced' : 'off' }),
      ]),
      element('p', { textContent: constraint.what }),
      element('p', { className: 'muted', textContent: constraint.why }),
    ]);
    // The kit is the one hand every match is dealt, so the spells it names are worth a tap each; a name
    // nothing resolves to shows as unknown on the chip, which is the constraint quietly checking nothing.
    if (constraint.spells.length) {
      card.append(element('div', { className: 'chips' }, constraint.spells.map(spellLink)));
    }

    block.append(card);
  }

  return block;
}

function walkNodes(node, visit) {
  if (!node || typeof node !== 'object') return;
  visit(node);
  for (const child of asArray(node.children)) walkNodes(child, visit);
}

/** The array a field is meant to hold, or nothing: a file that did not parse can hold anything there. */
function asArray(value) {
  return Array.isArray(value) ? value : [];
}

// ---------- the actions ----------

function payload() {
  const content = clone(state.draft);
  if (content.enabled !== false) delete content.enabled;
  return content;
}

async function save() {
  const item = state.selected;
  const request = { kind: TABS[state.tab].kind, write: [{ path: item.path, document: payload() }] };
  const result = await act('Saving', () => backend.change(request));

  if (!result) return;
  adopt(result);
  applyLocally(state.tab, request);
  state.dirty = false;
  reportSave(result, `Saved ${result.saved}.`);
  select(item.path);
}

/**
 * What a hosted save cannot do: rebuild the catalogue. `ContentStore` validates against the same DTOs the data
 * builder uses and a browser cannot run that, so CI is the authority (ADR 0023) and the page carries its own
 * edit forward instead of dropping it or pretending to have checked it.
 *
 * It is driven by the request that was sent, not by each caller's idea of what it did: every mutation writes,
 * removes and repoints through the same shape, so what is shown cannot drift from what was committed. The alias
 * map especially -- it is written whole, so a stale copy silently reverts the edit before it.
 */
function applyLocally(tab, request) {
  if (backend.kind !== 'hosted') return;

  for (const written of request.write || []) {
    const found = findDocument(written.path);
    if (found) {
      found.item.document = clone(written.document);
    } else {
      // `kind` is what the list draws a row by, so a row without it would render as nothing.
      documentsOf(tab).push({ path: written.path, kind: TABS[tab].kind, document: clone(written.document) });
    }
  }

  for (const path of request.remove || []) {
    const list = documentsOf(tab);
    const at = list.findIndex(item => item.path === path);
    if (at >= 0) list.splice(at, 1);
  }

  if (request.aliases) state.catalogue.aliases = { ...request.aliases };
  renderNav();
  refreshBalancePanel();
}

/** A save that became a commit says where to watch it; one that rebuilt the content says what it did. */
function reportSave(result, message) {
  if (!result.pullRequest) {
    report(message);
    return;
  }

  banner(`${message} Committed ${result.commit.slice(0, 7)} on ${result.branch}.`, 'ok',
    [`CI validates it on pull request #${result.pullRequest.number} — this page cannot: ${result.pullRequest.url}`]);
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

  const request = {
    kind: TABS[state.tab].kind,
    write: [{ path: nextPath, document: { ...payload(), id: nextId }, create: true }],
    aliases: { ...state.catalogue.aliases, [`${parsed.kind}:${parsed.name}`]: nextId },
  };
  const result = await act(`Cutting ${nextId}`, () => backend.change(request));

  if (!result) return;
  adopt(result);
  applyLocally(state.tab, request);
  reportSave(result, `${nextId} written to ${nextPath}; the alias ${parsed.kind}:${parsed.name} now points at it.`);
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
  // An alias left pointing at a deleted item stops the content from building, so it goes with the file.
  const request = {
    kind: TABS[state.tab].kind,
    remove: [item.path],
    aliases: Object.fromEntries(Object.entries(state.catalogue.aliases).filter(([, target]) => target !== item.id)),
  };
  const result = await act('Deleting', () => backend.change(request));

  if (!result) return;
  adopt(result);
  applyLocally(state.tab, request);
  state.selected = null;
  state.draft = null;
  renderDetail();
  reportSave(result, `Deleted ${item.path}.`);
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

  const request = {
    kind: TABS[state.tab].kind,
    write: [{ path, document: content, create: true }],
    aliases: { ...state.catalogue.aliases, [`${parsed.kind}:${safe}`]: id },
  };
  const result = await act(`Creating ${id}`, () => backend.change(request));

  if (!result) return;
  adopt(result);
  applyLocally(state.tab, request);
  reportSave(result, `${id} written to ${path}.`);
  select(path);
}

async function build() {
  const result = await act('Building', () => backend.build());
  if (!result) return;
  adopt(result);
  banner(`Content ${result.contentHash} written to ${result.output}.`, 'ok', result.notes);
}

async function run() {
  const request = {
    mode: $('run-mode').value,
    player1: agentSpec('p1'),
    player2: agentSpec('p2'),
    matches: Number($('run-matches').value) || 20,
    seed: $('run-seed').value === '' ? null : Number($('run-seed').value),
    weights: weightsPayload(),
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
  const result = await act('Playing', () => backend.play(request));
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
  state.runs = [result, ...state.runs];
  renderRuns();
}

// ---------- wiring ----------

async function load() {
  const catalogue = await act('Reading the content', () => backend.read());
  if (!catalogue) return;
  state.catalogue = catalogue;
  renderHeader();
  renderNav();
  renderDetail();
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

// ---------- the sheets ----------

/** The list, where it is a sheet: opened from the bar or from an editor's back button, closed by a pick. */
function openNav() {
  document.body.classList.add('nav-open');
  $('browse').setAttribute('aria-expanded', 'true');
  syncScrim();
  $('list').querySelector('li.selected')?.scrollIntoView({ block: 'center' });
}

function closeNav() {
  document.body.classList.remove('nav-open');
  $('browse').setAttribute('aria-expanded', 'false');
  syncScrim();
}

const PANELS = ['run', 'runs', 'audit', 'balance'];

/** The scrim is there whenever something is open over the editor on a phone; studio.css hides it on a desk. */
function syncScrim() {
  $('scrim').hidden = !document.body.classList.contains('nav-open') && PANELS.every(id => $(id).hidden);
}

function closePanels() {
  for (const id of PANELS) {
    $(id).hidden = true;
    $(`${id}-panel`).setAttribute('aria-pressed', 'false');
  }
}

function closeSheets() {
  closeNav();
  closePanels();
  syncScrim();
}

// The agents the engine can seat (docs/learning/agents.md). The kinds that read a file keep a box for its
// path, so picking one does not mean remembering the spec syntax; the heuristic agent can also be driven from
// the weights panel below, and then it reads the file this run writes for itself.
const AGENTS = [
  { value: 'random', label: 'Random — picks uniformly' },
  { value: 'greedy', label: 'Greedy — one-step lookahead, deterministic' },
  { value: 'explore:0.2', label: 'Explore 20% — greedy, one action in five at random' },
  { value: 'heuristic-weights', label: 'Heuristic — with the weights below', weights: true, spec: () => 'heuristic:' },
  { value: 'heuristic-file', label: 'Heuristic — with weights from a file', path: 'learning/weights/greedy.json', spec: path => `heuristic:${path}` },
  { value: 'policy-file', label: 'Policy — a trained policy from a file', path: 'models/clone/v1/policy.json', spec: path => `policy:${path}` },
];

const agentOf = slot => AGENTS.find(candidate => candidate.value === $(`run-${slot}`).value) || AGENTS[0];

/** Fills one agent picker, and shows the path box only for the kinds that read a file. */
function fillAgentPicker(slot) {
  const select = $(`run-${slot}`);
  select.replaceChildren(...AGENTS.map(agent => element('option', { value: agent.value, textContent: agent.label })));
  select.addEventListener('change', syncAgents);
}

/** Shows each slot's path box, and the weights panel when either slot is playing the panel's weights. */
function syncAgents() {
  for (const slot of ['p1', 'p2']) {
    const agent = agentOf(slot);
    const path = $(`run-${slot}-path`);
    path.hidden = !agent.path;
    if (agent.path && !path.value) path.value = agent.path;
  }

  $('run-weights').hidden = !['p1', 'p2'].some(slot => agentOf(slot).weights);
}

/** The agent spec the run panel is asking for: the kind, plus the file path when the kind reads one. */
function agentSpec(slot) {
  const agent = agentOf(slot);
  return agent.spec ? agent.spec($(`run-${slot}-path`).value.trim()) : agent.value;
}

// ---------- the weights panel ----------

/** One box per weight the engine has, filled from the engine's own defaults rather than from a copy here. */
async function loadWeights() {
  if (state.weights) return;
  const weights = await act('Reading the weights', () => backend.weights());
  if (!weights) return;
  state.weights = weights;
  $('run-weights-fields').replaceChildren(...weights.order.map(name => element('label', { textContent: name }, [
    element('input', { id: `weight-${name}`, type: 'number', step: '0.1', value: String(weights.values[name]) }),
  ])));
  clearBanner();
}

function resetWeights() {
  if (!state.weights) return;
  for (const name of state.weights.order) $(`weight-${name}`).value = String(state.weights.values[name]);
}

/**
 * The weights to play, or null when no slot is asking for them. An emptied box is left out rather than sent as
 * a zero: a weights file that omits a name keeps the built-in value, and an empty field reads as "unset", not
 * as "nothing". Number('') is 0, which would quietly play a very different agent.
 */
function weightsPayload() {
  if (!state.weights || $('run-weights').hidden) return null;
  const weights = {};
  for (const name of state.weights.order) {
    const text = $(`weight-${name}`).value.trim();
    if (text !== '') weights[name] = Number(text);
  }
  return weights;
}

// ---------- the run list ----------

async function loadRuns() {
  const runs = await act('Reading the runs', () => backend.runs());
  if (!runs) return;
  state.runs = runs;
  renderRuns();
  clearBanner();
}

function renderRuns() {
  if (!state.runs.length) {
    $('runs-body').replaceChildren(element('p', { className: 'hint', textContent: 'Nothing played yet. A run shows up here as soon as you play one.' }));
    return;
  }

  const table = element('table');
  table.append(element('tr', {}, ['', 'When', 'Mode', 'Player 1', 'Player 2', 'Seed', 'Matches', 'Content', '']
    .map((text, index) => element('th', { className: index === 5 || index === 6 ? 'num' : '', textContent: text }))));
  for (const run of state.runs) {
    table.append(element('tr', {}, [
      element('td', {}, [element('input', { type: 'checkbox', value: run.id, className: 'run-pick', ariaLabel: `Compare ${run.id}` })]),
      element('td', { className: 'mono', textContent: new Date(run.at).toLocaleString() }),
      element('td', { textContent: run.mode }),
      element('td', { textContent: agentLabel(run, run.player1), title: run.player1 }),
      element('td', { textContent: agentLabel(run, run.player2), title: run.player2 }),
      element('td', { className: 'num', textContent: String(run.seed) }),
      element('td', { className: 'num', textContent: String(run.matches) }),
      element('td', { className: 'mono', textContent: (run.contentHash || '').slice(0, 12) }),
      element('td', {}, [element('a', { href: run.url, target: '_blank', rel: 'noopener', textContent: 'Open' })]),
    ]));
  }

  $('runs-body').replaceChildren(table);
}

/** A spec pointing into the run's own directory is the weights that run was played with; the path adds nothing. */
function agentLabel(run, spec) {
  return spec.includes(run.id) ? `${spec.split(':')[0]} (its own weights)` : spec;
}

/**
 * Opens the two ticked runs side by side. Two runs on the same seeds and the same agents is what makes the
 * delta about the content; the table above says the seed, the agents and the content of each, so the page can
 * show what differs rather than decide for the author whether the comparison is fair.
 */
function compareRuns() {
  const picked = [...document.querySelectorAll('.run-pick:checked')].map(box => box.value);
  const status = $('runs-status');
  if (picked.length !== 2) {
    status.textContent = `Tick two runs to compare; ${picked.length} ticked.`;
    return;
  }

  // Oldest first, whatever order they were ticked in: the page reads the left side as the before and the right
  // as the after, and this list is newest first, so ticking from the top would otherwise reverse every delta.
  const [before, after] = picked.toSorted((left, right) => Date.parse(runAt(left)) - Date.parse(runAt(right)));
  status.textContent = '';
  window.open(`/compare/${before}/${after}`, '_blank', 'noopener');
}

const runAt = id => state.runs.find(run => run.id === id)?.at || '';

// ---------- the content audit ----------

async function loadAudit() {
  const result = await act('Auditing the content', () => backend.audit());
  if (!result) return;
  state.audit = result;
  renderAudit();
  clearBanner();
}

function auditSummary(result) {
  const audit = result.audit;
  const line = element('div', { className: 'audit-line' }, [
    element('span', { className: 'mono', textContent: `content ${audit.contentVersion.slice(0, 12)}` }),
    element('span', { textContent: `${audit.creatures} creatures, ${audit.spells} spells, ${audit.talentTrees} talent trees` }),
    element('span', { textContent: `reachability at ${audit.energyPerRound} energy per round over ${audit.roundCap} rounds` }),
  ]);

  // A digest is filed under the content hash it was measured on, so an edit leaves this content without one.
  // Saying so beats letting an author think the benchmark is still watching their back.
  line.append(result.benchmark.exists
    ? element('span', { textContent: 'benchmark digest: present' })
    : element('span', { className: 'warn', textContent: 'benchmark digest: none for this content yet' }));
  return line;
}

function auditFindings(findings) {
  if (!findings.length) {
    return element('p', { className: 'hint', textContent: 'Nothing unreachable: every spell is on some creature or behind a gate one can open, and every talent node opens.' });
  }

  return element('ul', { className: 'findings' }, findings.map(finding => element('li', {}, [
    element('span', { className: 'code', textContent: `${finding.code} ` }),
    element('span', { className: 'mono', textContent: finding.subject }),
    element('span', { textContent: ` — ${finding.message}` }),
  ])));
}

function auditReach(reach) {
  const table = element('table');
  table.append(element('tr', {}, ['Spell', 'Class', 'Cost', 'Damage', 'Bleed', 'Heal', 'Regen', 'Energy', 'EnRegen', 'Damage/energy', 'Starts on', 'Reachable by']
    .map((text, index) => element('th', { className: index >= 2 ? 'num' : '', textContent: text }))));
  for (const row of reach) {
    table.append(element('tr', {}, [
      element('td', {}, [element('button', { type: 'button', className: 'link', textContent: row.name, onclick: () => selectSpell(row.spell) })]),
      element('td', { textContent: row.creatureClass }),
      element('td', { className: 'num', textContent: String(row.cost) }),
      element('td', { className: 'num', textContent: String(row.damage) }),
      element('td', { className: 'num', textContent: String(row.bleedDamage) }),
      element('td', { className: 'num', textContent: String(row.healing) }),
      element('td', { className: 'num', textContent: String(row.regenerationHealing) }),
      element('td', { className: 'num', textContent: String(row.energy) }),
      element('td', { className: 'num', textContent: String(row.energyRegenerationEnergy) }),
      element('td', { className: 'num', textContent: row.damagePerEnergy.toFixed(1) }),
      element('td', { className: 'num', textContent: String(row.startingFor) }),
      element('td', { className: 'num', textContent: String(row.reachableBy) }),
    ]));
  }

  return table;
}

/** The audit talks in spell ids; the editor is keyed by file path, so go through the catalogue. */
function selectSpell(id) {
  const spell = documentsOf('spells').find(item => item.id === id);
  if (spell) select(spell.path);
}

function renderAudit() {
  const result = state.audit;
  $('audit-body').replaceChildren(auditSummary(result), auditFindings(result.audit.findings), auditReach(result.audit.reach));
}

/**
 * Shows one of the panels above the editor, loading what it needs the first time it is opened. One at a
 * time: the buttons read as a segmented control on a desk, and on a phone two sheets would stack.
 */
function togglePanel(id, load) {
  const panel = $(id);
  const opening = panel.hidden;
  closePanels();
  closeNav();
  panel.hidden = !opening;
  $(`${id}-panel`).setAttribute('aria-pressed', String(opening));
  syncScrim();
  if (!opening) return;
  load();
  // On a desk the panel is a card above the editor; opened from halfway down a form, it would open out of sight.
  if (!narrow.matches) panel.scrollIntoView({ behavior: 'smooth', block: 'start' });
}

/**
 * The token, kept in this browser and nowhere else. Pasting or forgetting one picks a different backend, so the
 * page re-reads which one it has rather than waiting for a reload to notice.
 */
function renderToken() {
  const held = storedToken();
  $('token').value = '';
  $('token').placeholder = held ? 'a token is kept in this browser' : 'github_pat_...';
  $('token-forget').hidden = !held;
  $('token-state').textContent = held
    ? 'Saving from here commits to studio/content.'
    : 'Reading only. Paste a token to save from this page.';
}

function useToken(token) {
  if (!storeToken(token)) {
    banner('This browser refuses to keep the token, so saving from here is not possible. Site data may be blocked.', 'error');
    return;
  }

  backend = backendForThisPage();
  renderToken();
  adoptBackendKind();
  banner(token ? 'Token kept in this browser.' : 'Token forgotten.', 'ok');
}

/** The run sheet of the published page launches workflows instead of matches; the rest of the page is the same. */
function adoptBackendKind() {
  const hosted = backend.kind === 'hosted';
  $('run-local').hidden = hosted;
  $('run-hosted').hidden = !hosted;
  $('run-title').textContent = hosted ? 'Launch on GitHub' : 'Run a match';
}

/** How many seeds only means something for an evaluation; one match is one match. */
function syncRunMode() {
  const evaluation = $('run-mode').value === 'evaluation';
  $('run-matches').disabled = !evaluation;
  $('run-go').textContent = evaluation ? 'Evaluate' : 'Play it';
}

$('run-mode').addEventListener('change', syncRunMode);
syncRunMode();
fillAgentPicker('p1');
fillAgentPicker('p2');
syncAgents();

$('search').addEventListener('input', renderNav);
$('new').addEventListener('click', create);
$('build').addEventListener('click', build);
$('run-go').addEventListener('click', run);
$('run-weights-reset').addEventListener('click', resetWeights);
$('runs-compare').addEventListener('click', compareRuns);
$('token-keep').addEventListener('click', () => useToken($('token').value.trim()));
$('token-forget').addEventListener('click', () => useToken(''));
renderToken();
$('run-panel').addEventListener('click', () => togglePanel('run', () => { if (backend.kind !== 'hosted') loadWeights(); }));
$('runs-panel').addEventListener('click', () => togglePanel('runs', loadRuns));
$('audit-panel').addEventListener('click', () => togglePanel('audit', loadAudit));
// Nothing to load: the knobs ride in on the catalogue, so opening this sheet is drawing what the page has.
$('balance-panel').addEventListener('click', () => togglePanel('balance', renderBalance));
$('browse').addEventListener('click', () => { if (document.body.classList.contains('nav-open')) closeNav(); else { closePanels(); openNav(); } });
$('nav-close').addEventListener('click', closeNav);
$('scrim').addEventListener('click', closeSheets);
for (const button of document.querySelectorAll('.panel .close')) {
  button.addEventListener('click', () => { $(button.dataset.close).hidden = true; $(`${button.dataset.close}-panel`).setAttribute('aria-pressed', 'false'); syncScrim(); });
}
document.addEventListener('keydown', event => { if (event.key === 'Escape') closeSheets(); });
// Growing past the phone width leaves the shell's sheets behind; the scrim must not stay over the desk.
narrow.addEventListener('change', () => { if (!narrow.matches) closeSheets(); });
adoptBackendKind();

window.addEventListener('beforeunload', event => {
  if (state.dirty) event.preventDefault();
});

await load();
