import { seatLabel } from './invitation.js';

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
