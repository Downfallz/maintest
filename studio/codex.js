import { active, named, packageFamilies, packageParents, packagesTeaching, spellMatches, effectText, targetText } from './catalogue.js';

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
    h('strong', 'spell-name', item.name), h('span', 'spell-summary', summary),
    h('span', 'spell-meta', `${doc.spellType} · ${targetText(doc.targeting)}`));
}

function packageTile(item, catalogue, open) {
  const doc = item.document;
  const tile = button('', 'package-tile', () => open(item.path));
  const parents = packageParents(item, catalogue);
  const spells = (doc.spells ?? []).map(id => named(catalogue.spells, id, catalogue)?.name ?? id);
  return append(tile,
    append(h('span', 'package-top'), h('span', 'tier-label', `TIER ${doc.level}`), h('span', 'initiative-bonus', `+${doc.initiativeBonus ?? 0} initiative`)),
    h('strong', 'package-name', item.name), h('span', 'package-spells', spells.join(' · ') || 'No spells'),
    h('span', 'package-parent', parents.length ? `Requires ${parents.map(parent => parent.name).join(' + ')}` : 'Start here'),
    h('span', 'tile-arrow', '↗'));
}

export function explore(catalogue, ui, open, repaint) {
  const groups = packageFamilies(catalogue);
  const packages = active(catalogue.tiers);
  const spells = active(catalogue.spells);
  const view = h('div', 'codex');
  view.append(append(h('section', 'codex-hero'),
    label('THE ARENA FIELD GUIDE'),
    h('h2', '', 'Find your next move.'),
    h('p', 'codex-copy', 'Every package. Every spell. Your next build.'),
    append(h('div', 'hero-stats'), stat(packages.length, 'packages'), stat(spells.length, 'spells'), stat(new Set(packages.map(item => item.document.level)).size, 'tiers'))));
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

export function spellLibrary(catalogue, ui, open) {
  const view = append(h('div', 'codex'), title('SPELL LIBRARY', 'Know your options.', 'Find a spell by name, effect or package. Tap a card for the full details.'));
  const controls = h('div', 'library-controls');
  const search = h('input', 'codex-search'); search.type = 'search'; search.placeholder = 'Search spells, effects, packages…'; search.value = ui.query ?? '';
  search.setAttribute('aria-label', 'Search spells');
  const results = h('div', 'spell-grid');
  const count = h('p', 'result-count'); count.setAttribute('aria-live', 'polite');
  const filters = h('div', 'spell-filters'); filters.setAttribute('aria-label', 'Spell type');
  const draw = () => {
    const matches = active(catalogue.spells).filter(item => spellMatches(item, ui.query ?? '', ui.type ?? 'All', catalogue));
    count.textContent = `${matches.length} spells`;
    results.replaceChildren(...matches.map(item => spellTile(item, catalogue, open)));
    if (!matches.length) results.append(message('No spells match. Try another name or clear the filters.'));
    for (const pick of filters.children) pick.setAttribute('aria-pressed', String(pick.textContent === (ui.type ?? 'All')));
  };
  for (const type of ['All', 'Offensive', 'Defensive', 'Passive']) filters.append(button(type, 'filter-pill', () => { ui.type = type; draw(); }));
  search.addEventListener('input', () => { ui.query = search.value; draw(); });
  view.append(append(controls, search, filters), count, results); draw(); return view;
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
  view.append(append(h('div', 'reader-stats'), stat(`+${doc.initiativeBonus ?? 0}`, 'base initiative'), stat((doc.spells ?? []).length, 'spells learned')));
  const parents = packageParents(item, catalogue);
  view.append(title('TO UNLOCK', parents.length ? 'Required packages' : 'An open starting point', parents.length ? 'Own all of these packages before choosing this one.' : 'This package has no prerequisite.'),
    append(h('div', 'reader-links'), ...parents.map(parent => itemLink(parent, open))));
  view.append(title('ONE PICK TEACHES', 'Included spells', 'Learn every spell below when you acquire this package.'));
  const spells = (doc.spells ?? []).map(id => named(catalogue.spells, id, catalogue));
  view.append(append(h('div', 'spell-grid'), ...spells.filter(Boolean).map(spell => spellTile(spell, catalogue, open))));
  for (const id of (doc.spells ?? []).filter(id => !named(catalogue.spells, id, catalogue))) view.append(message(`Spell unavailable: ${id}`));
  const next = active(catalogue.tiers).filter(pack => packageParents(pack, catalogue).some(parent => parent.id === item.id));
  if (next.length) view.append(title('WHAT COMES NEXT', 'Continue the path', 'Each card shows its complete prerequisites.'), append(h('div', 'package-grid'), ...next.map(pack => packageTile(pack, catalogue, open))));
}

function spellReading(view, item, catalogue, open) {
  const doc = item.document;
  view.append(append(h('div', 'reader-stats'), stat(doc.energyCost ?? 0, 'energy'), stat(targetText(doc.targeting), 'target'), stat(`${Number(((doc.criticalChance ?? 0) * 100).toFixed(2))}%`, 'critical bonus')));
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
