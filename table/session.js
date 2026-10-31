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
const HISTORY_KEY = 'downfall.table.saved-seats';

const SEATS = ['player1', 'player2'];

// The seats the link carries a token for, in board order. Hotseat is two people at one browser, so a page can
// hold both and follow whichever seat the match asks; a link with one names one seat, played on its own device.
export function seatsFromLocation(search) {
  const query = new URLSearchParams(search);
  return SEATS.map(seat => ({ seat, token: query.get(seat) })).filter(held => held.token);
}

// The finished session a link asks to replay, and the seat it is seen from (Player 1 unless it names Player 2):
// the admin panel's way into a match's replay, which needs no seat at all.
export function replayOf(search) {
  const query = new URLSearchParams(search);
  const id = query.get('replay');
  return id ? { id, seat: query.get('seat') === 'player2' ? 'player2' : 'player1' } : null;
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
  const token = held[seat];
  const saved = savedSeats(storage).filter(saved => saved.token !== token);
  delete held[seat];
  try {
    storage?.setItem(KEY, JSON.stringify(held));
    storage?.setItem(HISTORY_KEY, JSON.stringify(saved));
  } catch { /* The current page still works when storage is unavailable. */ }
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
    const saved = new Map(savedSeats(storage).map(saved => [saved.token, saved]));
    for (const seat of SEATS) if (held[seat]) saved.set(held[seat], { seat, token: held[seat] });
    storage?.setItem(HISTORY_KEY, JSON.stringify([...saved.values()].slice(-128)));
    storage?.setItem(KEY, JSON.stringify(held));
  } catch {
    // A browser that keeps nothing still plays; it just needs the link again after a reload.
  }
}

// Whether this browser keeps what the page writes: private browsing can throw on the way in, or hand nothing
// back. A page that keeps nothing holds its seat in the address instead (invitation.js).
export function keeps(storage) {
  const probe = 'downfall.table.probe';
  try {
    storage?.setItem(probe, '1');
    const kept = storage?.getItem(probe) === '1';
    storage?.removeItem(probe);
    return kept;
  } catch {
    return false;
  }
}

// Keep every table's capabilities independently; choosing a new table only replaces the active pair.
export function savedSeats(storage) {
  let saved = [];
  try {
    const value = JSON.parse(storage?.getItem(HISTORY_KEY) ?? '[]');
    if (Array.isArray(value)) saved = value.filter(item => SEATS.includes(item?.seat) && typeof item.token === 'string' && item.token);
  } catch { /* Old browsers and private browsing can still use an invitation link. */ }
  const all = new Map(saved.map(item => [item.token, item]));
  const active = remembered(storage);
  for (const seat of SEATS) if (typeof active[seat] === 'string' && active[seat]) all.set(active[seat], { seat, token: active[seat] });
  return [...all.values()].slice(-128);
}

export function activateSeats(seats, storage) {
  remember(storage, Object.fromEntries(seats.filter(item => SEATS.includes(item.seat) && item.token).map(item => [item.seat, item.token])));
}

// The tokens a page asks the host to verify: the ones this browser saved, and the ones its link or its held
// seats carry. A browser whose storage is blocked saves nothing, and its link is still a seat (ADR 0115).
export function resumeTokens(saved, held) {
  return [...new Set([...(saved ?? []), ...(held ?? [])].map(seat => seat?.token).filter(Boolean))];
}

// Where the address bar goes once the token is out of it: the table itself. `/` is the lobby now (ADR 0115), so
// a page that came in on `/?player2=...` stays on `/play` and a reload returns to the game, not the lobby.
export function playPath(pathname) {
  return !pathname || pathname === '/' ? '/play' : pathname;
}

// The seats of one table among those this browser holds: the one a link's tokens name, or the one a notice
// named (`?table=`, ADR 0118), or none when neither is here any more.
export function selectTableSeats(matches, requested, table = null) {
  const chosen = table
    ? matches.find(match => match.table?.id === table)
    : matches.find(match => match.seats.some(seat => requested.some(wanted => wanted.token === seat.token)));
  return chosen?.seats ?? [];
}

// The table a notice sent the browser to, off `?table=`, or null.
export function tableFromLocation(search) {
  return new URLSearchParams(search ?? '').get('table');
}

// The operator's token, kept by the admin panel when a link brought it there (admin.js): what the table page
// sends to ask the host whether this browser is the operator's. Nothing for a player's browser, which is the
// ordinary case, and nothing in a browser that keeps nothing.
export const OPERATOR_KEY = 'downfall.table.operator';

export function operatorToken(storage) {
  try {
    return storage?.getItem(OPERATOR_KEY) || null;
  } catch {
    return null;
  }
}

// Whether the host said this browser is the operator's, from its answer alone: a `yes` is the one thing that
// shows the way back to the admin panel. Anything else, an error included, shows nothing -- a player's page
// must not grow a link to a page that would refuse them.
export function isOperator(answer) {
  return answer?.ok === true && answer.body?.operator === true;
}
