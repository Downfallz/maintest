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
// owning its package, and prerequisite satisfaction alone cannot override the one-purchase cap.
export function talentClasses(catalogue, cards, creature, evolution) {
  const known = new Set(creature?.knownSpells ?? []);
  const owned = new Set(creature?.acquiredTiers ?? []);
  const offered = new Set(evolution?.creatures?.find(one => one.creature === creature?.id)?.availableTiers ?? []);
  return (catalogue?.packages ?? []).map(pack => ({
    ...pack,
    status: owned.has(pack.id) ? 'known' : offered.has(pack.id) ? 'available' : 'future',
    tiers: [{ tier: pack.level, spells: (pack.spells ?? []).map(spell => ({ spell, status: known.has(spell) ? 'known' : 'future' })) }],
  }));
}

// Every edge is an authored package prerequisite. Multiple parents are drawn under each parent; levels
// must increase, so malformed content cannot make the reference recurse forever.
export function packageForest(catalogue) {
  const packs = catalogue?.packages ?? [];
  const build = pack => ({ ...pack, key: pack.id, depth: pack.level,
    children: packs.filter(child => child.level > pack.level && child.prerequisites?.includes(pack.id)).map(build),
  });
  return packs.filter(pack => !pack.prerequisites?.some(id => packs.some(parent => parent.id === id && parent.level < pack.level))).map(build);
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
  for (const pack of catalogue?.packages ?? []) {
    const name = cards?.get(pack.spells?.[0])?.creatureClass;
    palette.set(pack.id, classColour(name ?? pack.name, palette));
  }
  return palette;
}
