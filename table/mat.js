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

// The host supplies tiers and prerequisite wording. Group them for reading, without inferring edges
// from prose or treating a tier as an unlock rule. Only Evolution options can say "available now".
export function talentClasses(catalogue, cards, creature, evolution) {
  const known = new Set(creature?.knownSpells ?? []);
  const offered = new Set(evolution?.creatures?.find(one => one.creature === creature?.id)?.unlockableSpells ?? []);
  const classes = new Map();
  const seen = new Set();
  for (const band of catalogue?.trees ?? []) {
    for (const spell of band.spells ?? []) {
      if (seen.has(spell)) continue;
      seen.add(spell);
      const face = cards?.get(spell);
      const name = face?.creatureClass || band.name || 'Unclassified';
      if (!classes.has(name)) classes.set(name, new Map());
      const tier = Number.isInteger(face?.tier) && face.tier > 0 ? face.tier : 0;
      const tiers = classes.get(name);
      if (!tiers.has(tier)) tiers.set(tier, []);
      tiers.get(tier).push({ spell, status: known.has(spell) ? 'known' : offered.has(spell) ? 'available' : 'future' });
    }
  }
  return [...classes].map(([name, tiers]) => ({
    name,
    tiers: [...tiers].sort(([a], [b]) => a - b).map(([tier, spells]) => ({ tier, spells })),
  }));
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
  return palette;
}
