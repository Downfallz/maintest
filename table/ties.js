// The tie order a seat builds before it declares (ADR 0063).
//
// The roll-off has already given each side its places in a tie; what is left is which of the seat's own
// creatures takes which of them. The options carry one group per tie, the seat's creatures in the order the
// rolls left them, and a creature only ever moves within its own group. The page builds the order one tap at
// a time -- the first tap takes the first place -- and the last creature of a group needs no tap: it takes
// the last place because there is no other.

// The creatures of each group not yet tapped, in the order the rolls left them.
export function untapped(groups, tapped) {
  const taken = new Set(tapped ?? []);
  return (groups ?? []).map(group => (group ?? []).filter(creature => !taken.has(creature)));
}

// The taps after one more. A creature the options do not offer, or one already tapped, changes nothing: a
// double tap on a phone is not a second decision.
export function tap(groups, tapped, creature) {
  const offered = (groups ?? []).some(group => (group ?? []).includes(creature));
  const already = (tapped ?? []).includes(creature);
  return offered && !already ? [...(tapped ?? []), creature] : [...(tapped ?? [])];
}

// Every group is settled once at most one of its creatures is left untapped.
export function isSettled(groups, tapped) {
  return untapped(groups, tapped).every(left => left.length <= 1);
}

// The order to submit: group by group, the tapped creatures in the order they were tapped, then whatever is
// left in the order the rolls left it. With no tap at all this is the order as rolled.
export function orderOf(groups, tapped) {
  const taps = tapped ?? [];
  return (groups ?? []).flatMap(group => {
    const members = group ?? [];
    const first = taps.filter(creature => members.includes(creature));
    return [...first, ...members.filter(creature => !first.includes(creature))];
  });
}
