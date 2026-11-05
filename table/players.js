// Who plays whom, as the game page's masthead says it (ADR 0122): the names the lobby gives each seat, read from
// the host, said from the point of view of the seats this page holds.

/** A side as the line names it: a person by their name, a bot by its name, an open place as a place. */
function sideName(seat) {
  if (seat.kind === 'open') return 'an open seat';
  if (seat.kind === 'bot') return `${seat.name} (bot)`;
  return seat.name && seat.name !== 'Player' ? seat.name : 'another player';
}

/**
 * The masthead's line for a table's `seats` ({seat, kind, name}) and the seat names this page holds, or null
 * when there is nothing to say (an older host that names no seats, or a page that holds none of them).
 */
export function versusLine(seats, held) {
  if (!Array.isArray(seats) || seats.length === 0) return null;
  const mine = seats.filter(seat => held.includes(seat.seat));
  const theirs = seats.filter(seat => !held.includes(seat.seat));
  if (mine.length === 0) return null;
  const me = mine.find(seat => seat.kind === 'person' && seat.name && seat.name !== 'Player')?.name;
  if (theirs.length === 0) return me ? `${me} · both seats on this device` : 'Both seats on this device';
  return `${me ? `You: ${me}` : 'You'} · against ${theirs.map(sideName).join(' and ')}`;
}

/** The table these tokens belong to, as the host's resume describes it, or null. */
export function tableOf(answer, tokens) {
  const entry = (answer?.tables ?? []).find(table => (table.seats ?? []).some(seat => tokens.includes(seat.token)));
  return entry?.table ?? null;
}

/** The seats of the table these tokens belong to, as the host's resume describes them, or null. */
export function seatsOf(answer, tokens) {
  return tableOf(answer, tokens)?.seats ?? null;
}
