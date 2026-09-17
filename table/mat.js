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
