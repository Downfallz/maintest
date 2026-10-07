import { EFFECT_GROUPS, energyCost, strategyOverview, compactEffect } from './strategy.js';
import { active, named, originText, packageFamilies, packageParents, packageRequirements, packagesTeaching, requirementText, spellInTier, spellLevels, spellMatches, spellOrigins, effectText, targetText, criticalText, percentText } from './catalogue.js';
import { standing } from './value.js';
import { passiveLines } from './tiers.js';

const h = (tag, className, text) => {
  const node = document.createElement(tag);
  node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
};
const append = (node, ...children) => { node.append(...children.filter(Boolean)); return node; };
const button = (text, className, click) => {
  const node = h('button', className, text);
  node.type = 'button'; node.addEventListener('click', click); return node;
};
const label = text => h('p', 'eyebrow', text);
const title = (kicker, heading, description) => append(h('div', 'codex-heading'), label(kicker), h('h2', '', heading), h('p', 'codex-copy', description));
const stat = (value, name) => append(h('div', 'codex-stat'), h('strong', '', value), h('span', '', name));
const message = text => h('p', 'codex-empty', text);

// The agents' scoring weights, read once the page has them. Until then, and on a page that cannot read them, the
// value is simply not shown: it is a reading added to a card, never something a card waits for.
let weights = null;
export function useWeights(values) { weights = values && typeof values === 'object' ? values : null; }

const decimal = value => (Math.round(value * 10) / 10).toFixed(1);
function tierGroup(read) {
  const tier = read.level === 0 ? 'Starting kit' : 'Tier ' + read.level;
  return `${tier} ${read.attack ? 'attacks' : 'non-attacks'}`;
}

function valuePlace(read) {
  if (read.level === null) return 'No package teaches it and no creature starts with it, so it has no tier to be compared in.';
  if (read.peers < 2) return `It is the only one of the ${tierGroup(read)}, so there is nothing to compare it with.`;
  const place = { lowest: 'the lowest of', highest: 'the highest of', within: 'inside' }[read.place];
  return `${tierGroup(read)} today: ${decimal(read.low)} to ${decimal(read.high)}, median ${decimal(read.median)}. This one is ${place} that range.`;
}

function reading(item, catalogue) {
  return weights ? standing(item, active(catalogue.spells), spellLevels(catalogue), weights) : null;
}

/** One line: the value a round and, when the spell has a group, the range of that group. */
function valueLine(item, catalogue) {
  const read = reading(item, catalogue);
  if (!read) return null;
  const range = read.level === null || read.peers < 2 ? '' : ` · ${tierGroup(read)} ${decimal(read.low)}–${decimal(read.high)}`;
  const node = h('span', 'spell-value', `Value ${decimal(read.value)} a round${range}`);
  node.title = 'A coarse estimate, not a balance verdict: open the spell for what it leaves out.';
  return node;
}

/** The reader's explanation: the number, where it sits, and what it cannot see. */
function valueReading(item, catalogue) {
  const read = reading(item, catalogue);
  if (!read) return null;
  const where = valuePlace(read);
  const box = append(h('details', 'value-reading'),
    h('summary', '', `Value ${decimal(read.value)} a round · ${decimal(read.cast)} a cast`),
    h('p', '', where),
    h('p', 'muted', 'Damage-equivalents priced with the agents\' scoring weights, the reading check-knobs uses: every effect for every target allowed, critical chance included, divided by the rounds of energy one cast costs. It leaves out the board, the target\'s defense and health, the kill, and the threat behind a defensive effect, and overstates a spell with several targets late in a match. Played matches decide balance; this only places a spell next to its neighbours.'));
  return box;
}

// The tier and the package a spell is bought in, on every card that lists it: browsing spells is how a player
// plans the next pick, and the pick is a package, not a spell.
function origin(item, catalogue) {
  const node = h('span', 'spell-origin', originText(spellOrigins(item, catalogue)));
  node.setAttribute('aria-label', `Learned from: ${node.textContent}`);
  return node;
}

function rune(index) {
  const node = h('span', `rune rune-${index % 3}`);
  node.setAttribute('aria-hidden', 'true');
  node.textContent = ['✦', '⟡', '◈'][index % 3];
  return node;
}

function itemLink(item, open) {
  if (item.missing) return h('span', 'missing-reference', `${item.name} · unavailable`);
  return button(item.name, 'codex-link', () => open(item.path));
}

function spellTile(item, catalogue, open) {
  const doc = item.document;
  const tile = button('', 'spell-tile', () => open(item.path));
  const families = packageFamilies(catalogue);
  const family = families.find(group => packagesTeaching(item, catalogue).some(pack => group.packages.includes(pack)));
  const summary = [...(doc.effects ?? []).map(effectText), ...(doc.casterEffects ?? []).map(effect => `Caster: ${effectText(effect)}`)].join(' · ');
  return append(tile,
    append(h('span', 'spell-tile-top'), rune(family?.tone ?? 0), h('span', 'energy-cost', `${doc.energyCost ?? 0} energy`)),
    h('strong', 'spell-name', item.name), origin(item, catalogue), h('span', 'spell-summary', summary),
    valueLine(item, catalogue), h('span', 'spell-meta', [doc.spellType, targetText(doc.targeting), criticalText(doc)].filter(Boolean).join(' · ')));
}

// A capstone teaches no spell and is bought for its passive (ADR 0100), so its tile reads what it gives instead.
function packageTile(item, catalogue, open) {
  const doc = item.document;
  const tile = button('', 'package-tile', () => open(item.path));
  const spells = (doc.spells ?? []).map(id => named(catalogue.spells, id, catalogue)?.name ?? id);
  const gives = passiveLines(doc.passive).map(line => `Passive: ${line}`);
  return append(tile,
    append(h('span', 'package-top'), h('span', 'tier-label', `TIER ${doc.level}`), h('span', 'initiative-bonus', `+${doc.initiativeBonus ?? 0} initiative`)),
    h('strong', 'package-name', item.name), h('span', 'package-spells', [...spells, ...gives].join(' · ') || 'No spells'),
    h('span', 'package-parent', requirementText(item, catalogue) || 'Start here'),
    h('span', 'tile-arrow', '↗'));
}

export function explore(catalogue, ui, open, repaint, navigate) {
  const groups = packageFamilies(catalogue);
  const packages = active(catalogue.tiers);
  const spells = active(catalogue.spells);
  const view = h('div', 'codex');
  view.append(append(h('section', 'codex-hero'),
    label('THE ARENA FIELD GUIDE'),
    h('h2', '', 'Find your next move.'),
    h('p', 'codex-copy', 'Every package. Every spell. Your next build.'),
    append(h('div', 'hero-stats'), stat(packages.length, 'packages'), stat(spells.length, 'spells'), stat(new Set(packages.map(item => item.document.level)).size, 'tiers'))));
  view.append(button('Compare energy & effects →', 'strategy-entry', () => navigate('strategy')));
  if (!packages.length) { view.append(message('No enabled packages yet. Open Catalogue to add the first one.')); return view; }
  const selected = groups.find(group => group.root.id === ui.family) ?? groups[0];
  if (selected) {
    const chooser = h('div', 'family-chooser'); chooser.setAttribute('aria-label', 'Progression family');
    for (const group of groups) {
      const pick = button('', `family-pick tone-${group.tone}`, () => { ui.family = group.root.id; repaint(); });
      pick.setAttribute('aria-pressed', String(group === selected));
      append(pick, rune(group.tone), append(h('span', ''), h('strong', '', group.root.name), h('small', '', `${group.packages.length} packages`)));
      chooser.append(pick);
    }
    view.append(title('CHOOSE A PATH', 'Make it your own.', 'Follow a family or combine paths. Each package teaches all its spells.'), chooser,
      progression(selected, catalogue, open));
  }
  const covered = new Set(groups.flatMap(group => group.packages.map(item => item.id)));
  const ungrouped = packages.filter(item => !covered.has(item.id));
  if (ungrouped.length) view.append(title('MORE PACKAGES', 'Other paths', 'Inspect these packages to see their prerequisites.'),
    append(h('div', 'package-grid'), ...ungrouped.map(item => packageTile(item, catalogue, open))));
  const starters = new Set(active(catalogue.creatures).flatMap(item => item.document.startingSpellIds ?? []).map(id => named(catalogue.spells, id, catalogue)?.id));
  const kit = spells.filter(item => starters.has(item.id));
  if (kit.length) view.append(title('THE STARTING KIT', 'Your first moves', 'Spells known by the creatures in this catalogue.'),
    append(h('div', 'spell-grid'), ...kit.map(item => spellTile(item, catalogue, open))));
  return view;
}

function progression(group, catalogue, open) {
  const grid = h('div', `progression tone-${group.tone}`);
  const levels = [...new Set(group.packages.map(item => item.document.level))].sort((a, b) => a - b);
  for (const level of levels) {
    const items = group.packages.filter(item => item.document.level === level);
    grid.append(append(h('section', 'tier-section'),
      append(h('div', 'tier-heading'), h('span', 'tier-number', String(level).padStart(2, '0')), h('h3', '', `Tier ${level}`), h('span', 'muted', `${items.length} package${items.length === 1 ? '' : 's'}`)),
      append(h('div', 'package-grid'), ...items.map(item => packageTile(item, catalogue, open)))));
  }
  return grid;
}

export function spellLibrary(catalogue, ui, open, navigate) {
  const view = append(h('div', 'codex'), title('SPELL LIBRARY', 'Know your options.', 'Find a spell by name, effect or package. Tap a card for the full details.'));
  view.prepend(spellViews('spells', navigate));
  const controls = h('div', 'library-controls');
  const search = h('input', 'codex-search'); search.type = 'search'; search.placeholder = 'Search spells, effects, packages…'; search.value = ui.query ?? '';
  search.setAttribute('aria-label', 'Search spells');
  const results = h('div', 'spell-grid');
  const count = h('p', 'result-count'); count.setAttribute('aria-live', 'polite');
  const filters = h('div', 'spell-filters'); filters.setAttribute('aria-label', 'Spell type');
  const tiers = h('div', 'spell-filters'); tiers.setAttribute('aria-label', 'Tier');
  const draw = () => {
    const matches = active(catalogue.spells).filter(item => spellMatches(item, ui.query ?? '', ui.type ?? 'All', catalogue) && spellInTier(item, ui.tier ?? 'All', catalogue));
    count.textContent = `${matches.length} spells`;
    results.replaceChildren(...matches.map(item => spellTile(item, catalogue, open)));
    if (!matches.length) results.append(message('No spells match. Try another name or clear the filters.'));
    for (const pick of filters.children) pick.setAttribute('aria-pressed', String(pick.textContent === (ui.type ?? 'All')));
    for (const pick of tiers.children) pick.setAttribute('aria-pressed', String(pick.dataset.tier === (ui.tier ?? 'All')));
  };
  for (const type of ['All', 'Offensive', 'Defensive', 'Passive']) filters.append(button(type, 'filter-pill', () => { ui.type = type; draw(); }));
  const levels = [...new Set(active(catalogue.tiers).map(item => item.document.level))].sort((a, b) => a - b);
  for (const [tier, name] of [['All', 'Any tier'], ...levels.map(level => [String(level), `Tier ${level}`]), ['Starting', 'Starting kit']]) {
    const pick = button(name, 'filter-pill', () => { ui.tier = tier; draw(); });
    pick.dataset.tier = tier; tiers.append(pick);
  }
  search.addEventListener('input', () => { ui.query = search.value; draw(); });
  view.append(append(controls, search, filters, tiers), count, results); draw(); return view;
}

export function reader(item, catalogue, open, edit, back) {
  const doc = item.document ?? {};
  const view = h('article', 'codex reader');
  view.append(append(h('div', 'reader-toolbar'), button('← Back', 'button ghost', back), button('Edit content', 'button', edit)));
  const kind = item.kind === 'Tier' ? `TIER ${doc.level} PACKAGE` : item.kind;
  view.append(append(h('div', 'reader-heading'), label(kind), h('h2', '', item.name || item.id)));
  if (item.enabled === false) view.append(message('Disabled · this content is not used in matches.'));
  if (item.problem) view.append(message(item.problem));
  if (item.kind === 'Tier') packageReading(view, item, catalogue, open);
  else if (item.kind === 'Spell') spellReading(view, item, catalogue, open);
  else if (item.kind === 'Creature') creatureReading(view, item, catalogue, open);
  else view.append(message('This is an authoring class tree. Package prerequisites determine progression in play. Use Edit content to inspect or change its nodes.'));
  const identity = append(h('details', 'reader-identity'), h('summary', '', 'Content reference'), h('code', '', item.id), h('p', 'muted', item.path));
  view.append(identity); return view;
}

function packageReading(view, item, catalogue, open) {
  const doc = item.document;
  const gives = passiveLines(doc.passive);
  view.append(append(h('div', 'reader-stats'), stat(`+${doc.initiativeBonus ?? 0}`, 'base initiative'), stat((doc.spells ?? []).length, 'spells learned'),
    gives.length ? stat(gives.length, gives.length === 1 ? 'passive' : 'passives') : null));
  const { allOf, anyOf } = packageRequirements(item, catalogue);
  if (!allOf.length && !anyOf.length) view.append(title('TO UNLOCK', 'An open starting point', 'This package has no prerequisite.'));
  if (allOf.length) {
    view.append(title('TO UNLOCK', 'Required packages', 'Own all of these packages before choosing this one.'),
      append(h('div', 'reader-links'), ...allOf.map(parent => itemLink(parent, open))));
  }
  // ADR 0100: a capstone is opened by whichever of its family's closers the creature climbed through.
  if (anyOf.length) {
    view.append(title(allOf.length ? 'AND' : 'TO UNLOCK', 'Any one of these', 'Own at least one of these packages before choosing this one.'),
      append(h('div', 'reader-links'), ...anyOf.map(parent => itemLink(parent, open))));
  }
  if (gives.length) {
    view.append(title('ONE PICK GIVES', 'Passive', 'Held for as long as the creature owns this package, for the rest of the match.'),
      append(h('div', 'reader-links'), ...gives.map(line => h('span', 'chip', line))));
  }
  if ((doc.spells ?? []).length || !gives.length) {
    view.append(title('ONE PICK TEACHES', 'Included spells', 'Learn every spell below when you acquire this package.'));
  }
  const spells = (doc.spells ?? []).map(id => named(catalogue.spells, id, catalogue));
  view.append(append(h('div', 'spell-grid'), ...spells.filter(Boolean).map(spell => spellTile(spell, catalogue, open))));
  for (const id of (doc.spells ?? []).filter(id => !named(catalogue.spells, id, catalogue))) view.append(message(`Spell unavailable: ${id}`));
  const next = active(catalogue.tiers).filter(pack => packageParents(pack, catalogue).some(parent => parent.id === item.id));
  if (next.length) view.append(title('WHAT COMES NEXT', 'Continue the path', 'Each card shows its complete prerequisites.'), append(h('div', 'package-grid'), ...next.map(pack => packageTile(pack, catalogue, open))));
}

function spellReading(view, item, catalogue, open) {
  const doc = item.document;
  view.append(append(h('div', 'reader-stats'), stat(doc.energyCost ?? 0, 'energy'), stat(targetText(doc.targeting), 'target'), stat(percentText(doc.criticalChance), 'critical bonus')));
  view.append(origin(item, catalogue));
  const value = valueReading(item, catalogue);
  if (value) view.append(value);
  view.append(title(doc.spellType ?? 'SPELL', 'What it does', 'Authored effect values; actual results depend on the combat situation.'));
  for (const effect of doc.effects ?? []) view.append(effectRow(effect));
  if (doc.casterEffects?.length) {
    view.append(h('h3', 'reader-subtitle', 'On the caster'));
    for (const effect of doc.casterEffects) view.append(effectRow(effect));
  }
  const packages = packagesTeaching(item, catalogue);
  view.append(title('LEARN THIS SPELL', packages.length ? 'Available in these packages' : 'Starting spell or unassigned', ''));
  view.append(append(h('div', 'package-grid'), ...packages.map(pack => packageTile(pack, catalogue, open))));
}

function effectRow(effect) {
  return append(h('div', 'effect-row'), h('span', 'effect-symbol', '✦'), append(h('div', ''), h('strong', '', effectText(effect)),
    effect.stacking ? h('small', 'muted', `Stacking: ${effect.stacking}`) : null));
}

function creatureReading(view, item, catalogue, open) {
  const doc = item.document;
  view.append(append(h('div', 'reader-stats'), stat(doc.baseHealth, 'HP'), stat(doc.baseEnergy, 'energy'), stat(doc.baseDefense, 'defense'), stat(doc.baseInitiative, 'initiative')),
    title('STARTING KIT', 'Ready for the arena', 'Spells this creature knows before acquiring any packages.'));
  const spells = (doc.startingSpellIds ?? []).map(id => named(catalogue.spells, id, catalogue)).filter(Boolean);
  view.append(append(h('div', 'spell-grid'), ...spells.map(spell => spellTile(spell, catalogue, open))));
}

function spellViews(selected, navigate) {
  const nav = h('div', 'spell-views');
  nav.setAttribute('role', 'navigation');
  nav.setAttribute('aria-label', 'Spell views');
  for (const [id, name] of [['spells', 'Cards'], ['strategy', 'Energy & effects']]) {
    const pick = button(name, 'filter-pill', () => navigate(id));
    pick.setAttribute('aria-pressed', String(selected === id));
    nav.append(pick);
  }
  return nav;
}

export function strategyLibrary(catalogue, ui, open, navigate) {
  const view = append(h('div', 'codex strategy'), spellViews('strategy', navigate),
    h('h2', 'strategy-heading', 'Energy & effects'));
  const summary = h('p', 'strategy-summary');
  const costs = h('div', 'strategy-costs');
  costs.setAttribute('aria-label', 'Energy cost');
  const effects = h('div', 'strategy-effects');
  effects.setAttribute('aria-label', 'Affected stats');
  const results = h('div', 'strategy-results');
  const count = h('p', 'result-count'); count.setAttribute('role', 'status');
  const scopeText = h('p', 'strategy-scope-text');
  const facets = [...EFFECT_GROUPS, { id: 'other', name: 'Other effects' }];
  const costButtons = new Map();
  const effectButtons = new Map();
  const allCosts = strategyOverview(catalogue).costs.map(([cost]) => cost);
  const draw = () => {
    const data = strategyOverview(catalogue, ui);
    summary.textContent = `${data.total} distinct spells · Energy per cast`;
    const costCounts = new Map(data.costs);
    for (const [cost, pick] of costButtons) {
      const total = cost === 'all' ? data.total : costCounts.get(cost) ?? 0;
      pick.querySelector('small').textContent = `${total} spells`;
      // Unknown cost is a real bucket; null must not become the default All selection.
      pick.setAttribute('aria-pressed', String(cost === (ui.cost === undefined ? 'all' : ui.cost)));
      pick.style.setProperty('--share', `${data.total ? total / data.total * 100 : 0}%`);
    }
    for (const [id, pick] of effectButtons) {
      const total = id ? data.facets.find(group => group.id === id)?.count ?? 0 : data.atCost;
      pick.querySelector('strong').textContent = String(total);
      pick.setAttribute('aria-pressed', String(id === (ui.effect ?? '')));
      pick.hidden = id === 'other' && total === 0 && ui.effect !== 'other';
    }
    const side = ui.side === 'caster' ? 'caster effects' : 'target effects';
    const costName = costLabel(ui.cost);
    count.textContent = `${data.matches.length} spells · ${costName} · ${side}`;
    scopeText.textContent = scopeDescription(catalogue, ui);
    results.replaceChildren(...data.matches.map(item => strategySpell(item, catalogue, open)));
    if (!data.matches.length) results.append(message('No spells in this combination. Try another cost or effect, or widen the package scope.'));
  };
  for (const cost of ['all', ...allCosts]) {
    const pick = button('', 'strategy-cost', () => { ui.cost = cost; draw(); });
    const short = cost === 'all' ? 'All' : String(cost ?? '?');
    pick.setAttribute('aria-label', costLabel(cost));
    append(pick, h('span', '', short), h('small', '', ''));
    costs.append(pick); costButtons.set(cost, pick);
  }
  for (const group of [{ id: '', name: 'All effects' }, ...facets]) {
    const pick = button('', 'strategy-effect', () => { ui.effect = group.id; draw(); });
    append(pick, h('span', '', group.name), h('strong', '', ''));
    effects.append(pick); effectButtons.set(group.id, pick);
  }
  const filters = strategyFilters(catalogue, ui, draw);
  const sides = h('div', 'strategy-sides'); sides.setAttribute('aria-label', 'Effect recipient');
  for (const [id, name] of [['target', 'On targets'], ['caster', 'On caster']]) {
    const pick = button(name, 'filter-pill', () => {
      ui.side = id;
      for (const child of sides.children) child.setAttribute('aria-pressed', String(child === pick));
      draw();
    });
    pick.setAttribute('aria-pressed', String(id === (ui.side ?? 'target'))); sides.append(pick);
  }
  filters.append(scopeText, h('p', 'strategy-note', 'Counts are distinct spells. A spell can affect several stats. Target effects include self-targeted spells.'));
  view.append(filters, append(h('div', 'strategy-sticky'), summary, costs), sides, effects, count, results);
  draw(); return view;
}

function costLabel(cost) {
  if (cost === null) return 'Unknown cost';
  if (cost === undefined || cost === 'all') return 'All costs';
  return `${cost} energy`;
}

function scopeDescription(catalogue, ui) {
  if (ui.scope === 'starting') return 'Starting spells from enabled creatures.';
  if (ui.scope !== 'packages') return 'All enabled spells · authored values before defense, criticals and caps.';
  const names = active(catalogue.tiers).filter(item => ui.packages?.includes(item.id)).map(item => item.name);
  if (ui.includeStarting) names.unshift('Starting kit');
  return names.length ? names.join(' + ') : 'Add a package or include the starting kit.';
}

function strategyFilters(catalogue, ui, draw) {
  const filters = append(h('details', 'strategy-filters'), h('summary', '', 'Compare packages'));
  const scope = h('select', ''); scope.setAttribute('aria-label', 'Spell scope');
  for (const [value, text] of [['all', 'Whole catalogue'], ['starting', 'Starting kit'], ['packages', 'Selected packages']]) {
    const option = h('option', '', text); option.value = value; scope.append(option);
  }
  scope.value = ui.scope ?? 'all';
  const packages = h('div', 'strategy-package-picker');
  const add = h('select', ''); add.setAttribute('aria-label', 'Add a package');
  const placeholder = h('option', '', 'Add a package…'); placeholder.value = ''; add.append(placeholder);
  for (const item of active(catalogue.tiers).toSorted((a, b) => a.document.level - b.document.level || a.name.localeCompare(b.name))) {
    const option = h('option', '', `Tier ${item.document.level} · ${item.name}`); option.value = item.id; add.append(option);
  }
  const chosen = h('div', 'strategy-package-chips');
  const include = h('input', ''); include.type = 'checkbox'; include.checked = Boolean(ui.includeStarting);
  include.addEventListener('change', () => { ui.includeStarting = include.checked; draw(); });
  const paint = () => {
    packages.hidden = ui.scope !== 'packages';
    const selected = active(catalogue.tiers).filter(item => ui.packages?.includes(item.id));
    chosen.replaceChildren(...selected.map(item => {
      const remove = button(`${item.name} ×`, 'filter-pill', () => {
        ui.packages = ui.packages.filter(id => id !== item.id); paint(); draw(); add.focus();
      });
      remove.setAttribute('aria-label', `Remove ${item.name}`); return remove;
    }));
    for (const option of add.options) option.disabled = Boolean(ui.packages?.includes(option.value));
  };
  scope.addEventListener('change', () => { ui.scope = scope.value; paint(); draw(); });
  add.addEventListener('change', () => {
    if (!add.value) return;
    ui.packages = [...new Set([...(ui.packages ?? []), add.value])]; add.value = ''; paint(); draw();
  });
  append(packages, add, chosen, append(h('label', 'strategy-starting'), include, h('span', '', 'Include starting kit')),
    h('p', 'strategy-note', 'Only the selected packages are counted. Prerequisites are not added automatically.'));
  filters.append(append(h('label', 'strategy-scope-label'), h('span', '', 'Spell scope'), scope), packages);
  paint(); return filters;
}

function strategySpell(item, catalogue, open) {
  const doc = item.document;
  const row = h('details', 'strategy-spell');
  const summary = h('summary', '');
  const heading = append(h('span', 'strategy-spell-heading'), h('strong', '', item.name), h('span', 'energy-cost', costLabel(energyCost(item))));
  const main = (doc.effects ?? []).map(compactEffect).join(' · ') || 'No target effects';
  const caster = (doc.casterEffects ?? []).map(compactEffect).join(' · ');
  const facts = [`${targetText(doc.targeting)}: ${main}`, criticalText(doc)].filter(Boolean).join(' · ');
  append(summary, heading, origin(item, catalogue), h('span', 'strategy-spell-facts', facts));
  const value = valueLine(item, catalogue);
  if (value) summary.append(value);
  if (caster) summary.append(h('span', 'strategy-caster', `Caster: ${caster}`));
  const detail = h('div', 'strategy-spell-detail');
  detail.append(h('p', 'strategy-note', 'Authored values per target; actual results depend on the board.'));
  for (const effect of doc.effects ?? []) detail.append(effectRow(effect));
  if (doc.casterEffects?.length) {
    detail.append(h('h3', 'reader-subtitle', 'On the caster'));
    for (const effect of doc.casterEffects) detail.append(effectRow(effect));
  }
  if (doc.criticalChance > 0) detail.append(h('p', '', `${percentText(doc.criticalChance)} critical bonus · Standard speed`));
  const packs = packagesTeaching(item, catalogue);
  if (packs.length) detail.append(h('p', 'strategy-note', 'Taught by'), append(h('div', 'reader-links'), ...packs.map(pack => itemLink(pack, open))));
  detail.append(button('Full spell →', 'codex-link', () => open(item.path)));
  row.append(summary, detail); return row;
}
