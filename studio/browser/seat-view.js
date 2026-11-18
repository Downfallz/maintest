// The seat's view as a journey last read it from the host. A response's body is read asynchronously, so an
// earlier poll can finish reading after a later one (#787): a view only takes the place of one it follows, by
// the end of the journal it read (`feedNext`), then by the asking it carries (`waitingAsked`, counted by the seat).
const position = view => [view.feedNext ?? -1, view.waitingAsked ?? -1];
function follows(next, previous) {
  const [nextFeed, nextAsked] = position(next), [previousFeed, previousAsked] = position(previous);
  return nextFeed > previousFeed || (nextFeed === previousFeed && nextAsked >= previousAsked);
}
export function readSeatViews() {
  const seat = { latest: null };
  seat.receive = value => {
    if (!value?.board || (seat.latest && !follows(value, seat.latest))) return false;
    seat.latest = value; return true;
  };
  return seat;
}
