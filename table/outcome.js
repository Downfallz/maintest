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
  if (table.casual === true) params.set('ranked', '0');
  return `/lobby?${params}`;
}

/**
 * What the battle's end card offers to play again (ADR 0153): the same kind of game, in one tap where it can be.
 * Against a bot or on a shared device it opens the same table again (`request`, POST /api/lobby/tables). Against
 * a friend it keeps their seat, the friend's invitation. Against a player who is not a friend, or whose
 * friendship this page could not read, it opens the lobby on them (`link`), which says what is missing. Null when
 * there is nobody to play again: a replay, a page holding no seat, an opponent with no account.
 * @param {any} table
 * @param {string[]} held
 * @param {string | null} [friendship] how the opponent stands to this player: 'friend', 'asked', 'asking', 'none'
 */
export function rematchPlan(table, held, friendship = null) {
  const link = rematchLink(table, held);
  if (!link) return null;
  const theirs = table.seats.find(seat => !held.includes(seat.seat));
  const format = table.format || undefined;
  if (!theirs) return { label: 'Rematch', request: { opponent: 'hotseat', format } };
  if (theirs.kind === 'bot') return { label: 'Rematch', request: { opponent: 'bot', format } };
  if (friendship === 'friend') return { label: 'Invite to rematch', request: { opponent: 'friend', friend: theirs.account, format, ...(table.casual === true ? { ranked: false } : {}) } };
  return { label: 'Invite to rematch', link };
}

// How the match ended, for the seat reading it: who won and why, off the outcome the host serves. A page on
// an older host that serves none says only that it is over. The table and the battle say it in the same words.
export function outcomeText(view, seat) {
  const outcome = view?.outcome;
  if (!outcome) return 'The match is over.';
  const because = { Elimination: outcome.winner ? 'the last team standing' : 'neither team survived', RoundCap: outcome.winner ? 'more health at the round cap' : 'equal health at the round cap', Concession: 'a concession', TimedOut: outcome.winner ? 'the time to answer ran out' : 'both players let the time to answer run out' }[outcome.reason] ?? outcome.reason ?? 'reason not recorded';
  if (!outcome.winner) return `The match is a draw · ${because}.`;
  const won = outcome.winner === seat;
  const winner = seatLabel(outcome.winner);
  // A replay opened from the admin panel is nobody's seat: it names the winner rather than saying "you".
  if (view.replay) return `${winner} wins · ${because}.`;
  // A timeout (ADR 0169) says whose time ran out, from this seat's side, as a concession does.
  if (outcome.reason === 'TimedOut') {
    const other = seatLabel(seat === 'player1' ? 'player2' : 'player1');
    return won ? `${other} let their time to answer run out · you win.` : `Your time to answer ran out · ${winner} wins.`;
  }
  if (outcome.reason === 'Concession') {
    const other = seatLabel(seat === 'player1' ? 'player2' : 'player1');
    return won ? `${other} conceded · you win.` : `You conceded · ${winner} wins.`;
  }
  return won ? `You win · ${because}.` : `${winner} wins · ${because}.`;
}

function resultHeading(view, seat) {
  const outcome = view.outcome;
  if (!outcome) return { title: 'Match complete', tone: 'unknown' };
  if (!outcome.winner) return { title: 'Draw', tone: 'draw' };
  const won = outcome.winner === seat;
  const title = won ? 'Victory' : 'Defeat';
  return { title: view.replay ? `${seatLabel(outcome.winner)} wins` : title, tone: won ? 'victory' : 'defeat' };
}

function teamResult(label, creatures) {
  const complete = Array.isArray(creatures) && creatures.length > 0;
  const alive = complete && creatures.every(one => typeof one.isAlive === 'boolean')
    ? `${creatures.filter(one => one.isAlive).length}/${creatures.length} standing` : 'Survivors unavailable';
  const counted = complete && creatures.every(one => Number.isFinite(one.health));
  const health = counted ? `${creatures.reduce((sum, one) => sum + one.health, 0)} HP` : 'HP unavailable';
  // The numbers the card draws, each null when the host did not serve it.
  const standing = complete && creatures.every(one => typeof one.isAlive === 'boolean') ? creatures.filter(one => one.isAlive).length : null;
  const hp = counted ? creatures.reduce((sum, one) => sum + Math.max(0, one.health), 0) : null;
  const maxHp = counted && creatures.every(one => Number.isFinite(one.maxHealth) && one.maxHealth > 0)
    ? creatures.reduce((sum, one) => sum + one.maxHealth, 0) : null;
  return { label, text: `${alive} · ${health}`, standing, total: complete ? creatures.length : null, hp, maxHp };
}

// A result never elects its own winner from health. Missing legacy fields stay unavailable.
export function matchResultSummary(view, seat) {
  if (!view?.over) return null;
  const other = seat === 'player1' ? 'player2' : 'player1';
  const teams = [teamResult(view.replay ? seatLabel(seat) : 'Your team', view.board?.allies),
    teamResult(view.replay ? seatLabel(other) : 'Opponent', view.board?.enemies)];
  return { ...resultHeading(view, seat), reason: outcomeText(view, seat), teams, round: view.board?.roundNumber };
}
