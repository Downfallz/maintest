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

/**
 * Whose turn it is at an unfinished table, from the seats the host says are being asked something now, and
 * whether it is this browser's: null before the match begins, once it is over, or while nobody is asked (the
 * bots are thinking or a round is resolving).
 */
export function turnOf(table, held = [], now = Date.now()) {
  if (table.over || table.waiting || !Array.isArray(table.seats)) return null;
  const asked = table.seats.filter(seat => seat.turn);
  if (asked.length === 0) return null;
  // A game between two players says how long whoever is asked has left (ADR 0169).
  const left = timeLeft(table.answerBy, now);
  const until = left ? ` · ${left} left` : '';
  if (asked.some(seat => held.includes(seat.seat))) {
    return { mine: true, text: `${asked.length > 1 ? 'Your turn · both players choose' : 'Your turn'}${until}` };
  }
  if (asked.every(seat => seat.kind === 'open')) return { mine: false, text: 'Waiting for a player to take the open seat' };
  if (asked.length > 1) return { mine: false, text: `Both players to play${until}` };
  const name = asked[0].name && asked[0].name !== 'Player' ? asked[0].name : 'The other player';
  return { mine: false, text: `${name}'s turn${until}` };
}

/**
 * How long is left before `answerBy` (an ISO time), as a card says it: days and hours, hours, or minutes under
 * the last hour. Null without a time, or once it has passed: the hourly job ends the game then (ADR 0169).
 */
export function timeLeft(answerBy, now = Date.now()) {
  const at = answerBy ? Date.parse(answerBy) : Number.NaN;
  if (!Number.isFinite(at) || at <= now) return null;
  const minutes = Math.floor((at - now) / 60000);
  if (minutes < 60) return `${Math.max(1, minutes)} min`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours} h`;
  return hours % 24 === 0 ? `${hours / 24} d` : `${Math.floor(hours / 24)} d ${hours % 24} h`;
}
