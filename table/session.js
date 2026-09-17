// Which seats this browser holds, and where that survives a reload.
//
// A token reaches a device once: on the link the host printed, or on the redirect a typed join code answers
// with. After that it lives in this browser's storage, because the alternative is a player whose phone locked
// mid-match walking back to the console to read a code again -- and because the address bar is then free of
// the token, which on two devices is the difference between a seat and a seat anyone standing behind you can
// read.
//
// Storage is not guaranteed: a browser in private mode throws on the way in or hands back nothing. Every path
// here works without it, at the cost of needing the link again after a reload.

const KEY = 'downfall.table.seats';

const SEATS = ['player1', 'player2'];

// The seats the link carries a token for, in board order. Hotseat is two people at one browser, so a page can
// hold both and follow whichever seat the match asks; a link with one names one seat, played on its own device.
export function seatsFromLocation(search) {
  const query = new URLSearchParams(search);
  return SEATS.map(seat => ({ seat, token: query.get(seat) })).filter(held => held.token);
}

// What the link brings plus what this browser already had, with the link winning: opening a fresh code is how
// a player says they are somebody else now.
export function heldSeats(search, storage) {
  const held = { ...remembered(storage) };
  for (const { seat, token } of seatsFromLocation(search)) {
    held[seat] = token;
  }

  remember(storage, held);
  return SEATS.filter(seat => held[seat]).map(seat => ({ seat, token: held[seat] }));
}

// A token the host no longer knows is worse than none: it answers 403 to every poll, and the page would say so
// for ever. One seat is dropped, not the lot: the other seat on this page may be the code just typed for the
// table being played, and it is no longer in the address bar to be read back from.
export function forget(storage, seat) {
  const held = { ...remembered(storage) };
  delete held[seat];
  remember(storage, held);
}

function remembered(storage) {
  try {
    const stored = JSON.parse(storage?.getItem(KEY) ?? '{}');
    return stored && typeof stored === 'object' ? stored : {};
  } catch {
    return {};
  }
}

function remember(storage, held) {
  try {
    storage?.setItem(KEY, JSON.stringify(held));
  } catch {
    // A browser that keeps nothing still plays; it just needs the link again after a reload.
  }
}
