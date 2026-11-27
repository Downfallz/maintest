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

// Which slot the strip points at, or -1 when it points at none. Combat walks the timeline once (ADR 0083): the
// activation cursor is the slot whose spell is revealed, targeted and resolved next. Outside Activation the
// round is on no slot at all -- the timeline is built before intents are even declared -- and a lit slot would
// be pointing at a creature nobody is playing.
export function cursorOf(board) {
  return board?.subPhase === 'Activation' ? index(board.activationCursor) : -1;
}

function index(cursor) {
  return Number.isInteger(cursor) ? cursor : -1;
}

/** What a slot says of a tie between the two teams that placed it (ADR 0208). */
export const RANDOM_TIE = 'Tie settled at random';

// Whether a tie between the two teams placed a slot: the engine drew for it (ADR 0063), and the app says only that
// the tie was settled at random (ADR 0208), never the faces the record keeps. Empty for a creature nothing was
// drawn for -- a tie held by one side alone, or no tie at all -- which is most of them.
export function tieText(rollOffs, creature) {
  const rolls = (rollOffs ?? []).find(rollOff => rollOff?.creature === creature)?.rolls ?? [];
  return rolls.length === 0 ? '' : RANDOM_TIE;
}

// The round's order once the speeds are revealed, as the pop-up reads it: each side's speeds, and every slot in
// the order it is played with its initiative and whether a tie placed it. Read off the host's timeline and nothing else -- the
// opponent's speeds are public from the moment the timeline exists, and never before it.
export function speedReveal(board) {
  const timeline = board?.timeline ?? [];
  const cursor = cursorOf(board);
  const order = timeline.map((slot, index) => ({
    position: index + 1, creature: slot.creature, side: side(slot, board.slot), speed: slot.speed ?? '',
    initiative: slot.initiative, tie: tieText(board.rollOffs, slot.creature), isNow: index === cursor,
  }));
  const of = which => order.filter(slot => slot.side === which).map(({ creature, speed }) => ({ creature, speed }));
  return { order, theirs: of('enemy'), mine: of('ally') };
}
