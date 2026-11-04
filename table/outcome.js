import { seatLabel } from './invitation.js';

/**
 * Where "Rematch" sends a player (ADR 0129, stage 4): the lobby, with the same opponent and format chosen, from the
 * table's seats as the lobby names them and the seats this page holds. Null when there is nothing to offer: a
 * page holding no seat, an opponent with no account, a host that names no seats.
 */
export function rematchLink(table, held) {
  const seats = table?.seats;
  if (!Array.isArray(seats) || seats.length !== 2 || !Array.isArray(held) || held.length === 0) return null;
  const theirs = seats.filter(seat => !held.includes(seat.seat));
  const params = new URLSearchParams();
  if (theirs.length === 0) params.set('rematch', 'hotseat');
  else if (theirs[0].kind === 'bot') params.set('rematch', 'bot');
  else if (theirs[0].kind === 'person' && theirs[0].account) params.set('rematch', theirs[0].account);
  else return null;
  if (table.format) params.set('format', table.format);
  return `/lobby?${params}`;
}

// How the match ended, for the seat reading it: who won and why, off the outcome the host serves. A page on
// an older host that serves none says only that it is over. The table and the arena say it in the same words.
export function outcomeText(view, seat) {
  const outcome = view?.outcome;
  if (!outcome) return 'The match is over.';
  const because = { Elimination: 'the last team standing', RoundCap: 'more health at the round cap', Concession: 'a concession' }[outcome.reason] ?? outcome.reason;
  if (!outcome.winner) return `The match is a draw · ${because}.`;
  const won = outcome.winner === seat;
  const winner = seatLabel(outcome.winner);
  // A replay opened from the admin panel is nobody's seat: it names the winner rather than saying "you".
  if (view.replay) return `${winner} wins · ${because}.`;
  if (outcome.reason === 'Concession') {
    const other = seatLabel(seat === 'player1' ? 'player2' : 'player1');
    return won ? `${other} conceded · you win.` : `You conceded · ${winner} wins.`;
  }
  return won ? `You win · ${because}.` : `${winner} wins · ${because}.`;
}
