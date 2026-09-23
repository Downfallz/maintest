// The initiative track, as a strip.
//
// The host builds the order and the page never touches it: the timeline is the engine's own ranking, ties and
// all, and a page that sorted it would be showing an order nobody plays. Grouping is by speed band in the
// order the bands first appear -- Quick before Standard, because that is how the engine builds it -- so even
// the bands are read off the data rather than known here.

export function bands(timeline) {
  const order = [];
  const bySpeed = new Map();
  for (const slot of timeline ?? []) {
    const speed = slot?.speed ?? '';
    if (!bySpeed.has(speed)) {
      bySpeed.set(speed, []);
      order.push(speed);
    }

    bySpeed.get(speed).push(slot);
  }

  return order.map(speed => ({ speed, slots: bySpeed.get(speed) }));
}

// The same slots, each told whether it is the one the round is on. The cursor is an index into the timeline,
// so it is counted over the whole strip and not within a band.
export function withCursor(timeline, cursor) {
  return (timeline ?? []).map((slot, index) => ({ ...slot, isNow: index === cursor }));
}

// Whose slot it is, from the seat reading it: a strip at 360 px has room for "us" and "them" and no more.
export function side(slot, seat) {
  return slot?.owner === seat ? 'ally' : 'enemy';
}

// Which slot the strip points at, or -1 when it points at none. A round spends two cursors and they do not
// advance together: the reveal cursor while actions are being bound to their targets, the resolve cursor while
// they are resolved, and the resolve cursor sits at zero for the whole of RevealAndTarget. Reading it there
// would keep the first slot lit while three later creatures choose targets, which is the one thing the strip is
// for. Outside those two sub-phases the round is on no slot at all -- the timeline is built before intents are
// even declared -- and a lit slot would be pointing at a creature nobody is playing.
export function cursorOf(board) {
  if (board?.subPhase === 'RevealAndTarget') return index(board.revealCursor);
  if (board?.subPhase === 'ActionResolution') return index(board.resolveCursor);
  return -1;
}

function index(cursor) {
  return Number.isInteger(cursor) ? cursor : -1;
}

// The d20 rolls behind a slot's place, as the strip prints them (ADR 0063): "d20 17", or "d20 11 → 4" when the
// creature rolled again after the other side matched it. Empty for a creature that did not roll -- a tie held
// by one side alone, or no tie at all -- which is most of them, so the strip stays as short as it was.
export function rollText(rollOffs, creature) {
  const rolls = (rollOffs ?? []).find(rollOff => rollOff?.creature === creature)?.rolls ?? [];
  return rolls.length === 0 ? '' : `d20 ${rolls.join(' → ')}`;
}
