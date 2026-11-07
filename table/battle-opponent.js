// Who the player is up against, as the battle's header says it: a person by their player name, with their place on
// the ladder (ADR 0124) and whether they are a friend (ADR 0129), or a bot by its name. Read from the table's own
// seat summaries, the leaderboard and the friends list; nothing here is private to the other seat.

/** The other seat at the table, as the lobby describes it, or null when the table names nobody else. */
export function opponentOf(table, seat) {
  return (table?.seats ?? []).find(one => one.seat !== seat) ?? null;
}

/**
 * What the header and its card say of the opponent. `ladder` is GET /api/leaderboard's answer and `friends`
 * GET /api/friends's, each null when it could not be read (signed out, accounts off, the host away): the card then
 * says nothing about ranks or friendship rather than guessing.
 */
export function opponentCard(opponent, ladder = null, friends = null) {
  if (!opponent) return { kind: 'unknown', name: 'Opponent', badge: '', rank: null, rankText: '', friend: null };
  if (opponent.kind === 'bot') {
    return { kind: 'bot', name: opponent.name, badge: 'BOT', rank: null, rankText: 'A bot · bots are not ranked', friend: null };
  }
  if (opponent.kind === 'open' || opponent.kind === 'invited') {
    return { kind: 'waiting', name: opponent.kind === 'invited' ? opponent.name : 'Open seat', badge: '', rank: null,
      rankText: opponent.kind === 'invited' ? 'Invited · not seated yet' : 'Waiting for a player to take the seat', friend: null };
  }
  const row = ladder?.players?.find(one => one.username === opponent.name) ?? null;
  const rank = row ? { position: row.rank, rating: row.rating, provisional: Boolean(row.provisional), wins: row.wins, losses: row.losses, draws: row.draws } : null;
  const rankText = !ladder ? '' : !row ? 'Unranked · no rated game yet'
    : `${row.provisional ? 'Provisional' : `#${row.rank}`} · ${row.rating} rating · ${row.wins}W ${row.losses}L${row.draws ? ` ${row.draws}D` : ''}`;
  const id = opponent.account ?? null;
  const has = list => Boolean(id) && (list ?? []).some(one => one.id === id);
  const friend = !friends || !id ? null
    : has(friends.friends) ? 'friend' : has(friends.outgoing) ? 'asked' : has(friends.incoming) ? 'asking' : 'none';
  return { kind: 'person', name: opponent.name, badge: rank && !rank.provisional ? `#${rank.position}` : '', rank, rankText, friend };
}

/** The friend action the card offers: asking, or accepting by asking back (ADR 0129); none once asked or friends. */
export function friendAction(friend) {
  if (friend === 'none') return { label: 'Add friend', note: '' };
  if (friend === 'asking') return { label: 'Accept friend request', note: 'Wants to be your friend' };
  if (friend === 'asked') return { label: '', note: 'Friend request sent' };
  if (friend === 'friend') return { label: '', note: 'Your friend' };
  return null;
}
