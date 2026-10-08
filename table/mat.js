// The talent mat: every band of the tree, with a pip per creature that knows the spell.
//
// It is a tab rather than a panel because it is touched once a round, during Evolution, and a phone has one
// screen (docs/tabletop/app-roadmap.md, stage 4). The bands and the spell names come from the catalogue the
// host serves; the pips come from each creature's own snapshot. Nothing here knows a tree or a spell by name.

export function matBands(catalogue, creatures, cards) {
  const known = (creatures ?? []).map(creature => ({
    creature: creature?.id,
    spells: new Set(creature?.knownSpells ?? []),
  }));

  return (catalogue?.trees ?? []).map(band => ({
    tree: band?.treeName ?? '',
    name: band?.name ?? '',
    depth: band?.depth ?? 0,
    spells: (band?.spells ?? []).map(spell => ({
      spell,
      name: cards?.get?.(spell)?.name ?? spell,
      // What has to be known before this one can be: the gate the printed mat carries on its band
      // (playtest-app.md §3.1). It is the card's own `requires`, the host's words, and it is the only place a
      // player can read why a node is out of reach -- the decision sheet offers what is unlocked and nothing
      // about what is not.
      requires: cards?.get?.(spell)?.requires ?? '',
      pips: known.map(one => ({ creature: one.creature, known: one.spells.has(spell) })),
    })),
  }));
}

// Which bands are worth drawing at all: a band nobody can reach yet is still on the printed mat, but an empty
// one is a row of nothing. The catalogue decides what exists; this only drops the blanks.
export function drawn(bands) {
  return (bands ?? []).filter(band => band.spells.length > 0);
}

// Class identity is shared by the hand, unlock picker and reference. Names remain the primary label;
// the accent is a stable visual cue, independent of which cards this creature has learned.
export function classColour(name, palette) {
  if (palette?.has(name)) return palette.get(name);
  let hash = 0;
  for (const letter of String(name ?? '')) hash = (hash * 31 + letter.codePointAt(0)) >>> 0;
  return `hsl(${hash % 360} 48% 68%)`;
}

// Package ownership and legal offers come from the host. Knowing every contained spell does not mean
// owning its package, and prerequisite satisfaction alone cannot override the one-purchase cap. A pick is
// face down until the Evolution sub-phase ends (ADR 0089), so a package this seat picked and has not been
// bought yet is 'picked': the player's own choices, which the host serves to that seat only.
export function talentClasses(catalogue, cards, creature, evolution, picks = []) {
  const known = new Set(creature?.knownSpells ?? []);
  const owned = new Set(creature?.acquiredTiers ?? []);
  const offered = new Set(evolution?.creatures?.find(one => one.creature === creature?.id)?.availableTiers ?? []);
  const picked = new Set((picks ?? []).filter(pick => pick.creature === creature?.id).map(pick => pick.tier));
  return (catalogue?.packages ?? []).map(pack => ({
    ...pack,
    status: packageStatus(pack.id, owned, picked, offered),
    tiers: [{ tier: pack.level, spells: (pack.spells ?? []).map(spell => ({ spell, status: known.has(spell) ? 'known' : 'future' })) }],
  }));
}

function packageStatus(id, owned, picked, offered) {
  if (owned.has(id)) return 'known';
  if (picked.has(id)) return 'picked';
  return offered.has(id) ? 'available' : 'future';
}

// Every package a pick has to own first: every one of `prerequisites`, or any one of `anyOf` (ADR 0101).
function parentsOf(pack) {
  return [...(Array.isArray(pack?.prerequisites) ? pack.prerequisites : []), ...(Array.isArray(pack?.anyOf) ? pack.anyOf : [])];
}

// Every edge is an authored package prerequisite, from either list: a capstone is drawn under each of its
// family's level-3 packages, because any one of them opens it. Multiple parents are drawn under each parent;
// levels must increase, so malformed content cannot make the reference recurse forever.
export function packageForest(catalogue) {
  const packs = catalogue?.packages ?? [];
  const build = pack => ({ ...pack, key: pack.id, depth: pack.level,
    children: packs.filter(child => child.level > pack.level && parentsOf(child).includes(pack.id)).map(build),
  });
  return packs.filter(pack => !parentsOf(pack).some(id => packs.some(parent => parent.id === id && parent.level < pack.level))).map(build);
}

// What a package needs, as its card prints it. The all-of list is joined with "+" and the any-of list with "/",
// so "Requires one of" is never read as "requires all of".
export function requirementLine(pack, nameOf = id => id) {
  const all = (Array.isArray(pack?.prerequisites) ? pack.prerequisites : []).map(nameOf).join(' + ');
  const any = (Array.isArray(pack?.anyOf) ? pack.anyOf : []).map(nameOf).join(' / ');
  if (all && any) return `Requires: ${all}, and one of: ${any}`;
  if (any) return `Requires one of: ${any}`;
  return `Requires: ${all || 'No prerequisite package'}`;
}

// What a package gives for as long as it is owned, in the host's words (`passiveLines`, ADR 0101). Nothing
// here words a passive: a card that does not carry the lines has none to print.
export function passiveOf(pack) {
  return (Array.isArray(pack?.passiveLines) ? pack.passiveLines : []).filter(line => typeof line === 'string' && line !== '');
}

// The family a package belongs to, by its first parent of a lower level, up to the package that opens it.
// A capstone teaches no spell to take a class colour from, so it wears its family's.
function familyRoot(pack, byId, seen = new Set()) {
  if (seen.has(pack.id)) return pack;
  seen.add(pack.id);
  const parent = parentsOf(pack).map(id => byId.get(id)).find(one => one && one.level < pack.level);
  return parent ? familyRoot(parent, byId, seen) : pack;
}

// Parent codes are scoped to their tree; depth alone is not enough to recover ancestry after reordering.
export function talentForest(catalogue) {
  const nodes = (catalogue?.trees ?? []).map((band, index) => ({ ...band, key: `${band.tree ?? band.treeName ?? ''}/${band.code ?? index}`, children: [] }));
  const lookup = new Map(nodes.map(node => [node.key, node]));
  const roots = [];
  for (const node of nodes) {
    const parent = node.parentCode ? lookup.get(`${node.tree ?? node.treeName ?? ''}/${node.parentCode}`) : null;
    if (parent && parent.depth < node.depth) parent.children.push(node);
    else roots.push(node);
  }
  return roots;
}

// The authored first-level branches receive coherent cool, leaf and ember ranges. Descendants vary
// within that range. This is presentation, independent of spell tiers, legality and prerequisite wording.
export function talentPalette(catalogue, cards) {
  const palette = new Map();
  const ranges = [
    ['#89b8ee', '#72c9ce', '#aaa0e4', '#c69ee0'],
    ['#b6cb78', '#8fc588', '#d0cb78', '#c1b55f'],
    ['#e5a36f', '#e68573', '#bb957c', '#e4ba7a'],
  ];
  function paint(node, range, shade = 0) {
    const colour = range[shade % range.length];
    palette.set(node.key, colour);
    for (const spell of node.spells ?? []) {
      const name = cards?.get(spell)?.creatureClass;
      if (name) palette.set(name, colour);
    }
    node.children.forEach((child, index) => paint(child, range, index + 1));
  }
  for (const root of talentForest(catalogue)) {
    paint({ ...root, children: [] }, ['#c9c2a8']);
    root.children.forEach((branch, index) => paint(branch, ranges[index % ranges.length]));
  }
  const packs = catalogue?.packages ?? [];
  const byId = new Map(packs.map(pack => [pack.id, pack]));
  for (const pack of packs.filter(one => one.spells?.length)) {
    const name = cards?.get(pack.spells[0])?.creatureClass;
    palette.set(pack.id, classColour(name ?? pack.name, palette));
  }
  for (const pack of packs.filter(one => !one.spells?.length)) {
    const root = familyRoot(pack, byId);
    palette.set(pack.id, root !== pack && palette.has(root.id) ? palette.get(root.id) : classColour(pack.name, palette));
  }
  return palette;
}
