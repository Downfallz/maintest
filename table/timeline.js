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
