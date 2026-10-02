// The room a table is in before its match begins (ADR 0092): who is here, who is still to come, and the code
// and link that bring them. Pure, because what a waiting player is shown is worth a test without a browser.

const nameOf = seat => (seat === 'player1' ? 'Player 1' : 'Player 2');

// What the seat payload says about the wait, as the page draws it, or null once the match has begun. The
// seats listed are the ones nobody has reached; a bot's seat is never among them, so a table against a bot
// never shows a room at all.
export function waitingRoom(view, origin = '') {
  const missing = view?.waiting?.seats;
  if (!Array.isArray(missing) || missing.length === 0) return null;
  const names = missing.map(seat => nameOf(seat.slot));
  return {
    line: `Waiting for ${names.join(' and ')} to join…`,
    detail: 'The match begins the moment everyone is seated; you both pick your first package at the same time.',
    seats: missing.map(seat => ({
      slot: seat.slot,
      name: nameOf(seat.slot),
      code: seat.code ?? null,
      link: seat.join ? `${origin}${seat.join}` : null,
    })),
  };
}
