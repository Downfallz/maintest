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
  const because = { Elimination: outcome.winner ? 'the last team standing' : 'neither team survived', RoundCap: outcome.winner ? 'more health at the round cap' : 'equal health at the round cap', Concession: 'a concession' }[outcome.reason] ?? outcome.reason ?? 'reason not recorded';
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

// A result never elects its own winner from health. Missing legacy fields stay unavailable.
export function matchResultSummary(view, seat) {
  if (!view?.over) return null;
  const outcome = view.outcome, won = outcome?.winner === seat;
  const tone = !outcome ? 'unknown' : !outcome.winner ? 'draw' : won ? 'victory' : 'defeat';
  const title = !outcome ? 'Match complete' : !outcome.winner ? 'Draw'
    : view.replay ? `${seatLabel(outcome.winner)} wins` : won ? 'Victory' : 'Defeat';
  const teams = ['allies', 'enemies'].map((side, index) => {
    const creatures = view.board?.[side];
    const label = view.replay ? seatLabel(index ? (seat === 'player1' ? 'player2' : 'player1') : seat)
      : index ? 'Opponent' : 'Your team';
    const complete = Array.isArray(creatures) && creatures.length > 0;
    const alive = complete && creatures.every(one => typeof one.isAlive === 'boolean')
      ? `${creatures.filter(one => one.isAlive).length}/${creatures.length} standing` : 'Survivors unavailable';
    const health = complete && creatures.every(one => Number.isFinite(one.health))
      ? `${creatures.reduce((sum, one) => sum + one.health, 0)} HP` : 'HP unavailable';
    return { label, text: `${alive} · ${health}` };
  });
  return { title, tone, reason: outcomeText(view, seat), teams, round: view.board?.roundNumber };
}
