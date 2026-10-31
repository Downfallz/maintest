// What a page does with the keys a link carries (ADR 0116).
//
// A link or a typed code carries a seat's invitation, not its token. The host trades an invitation for the
// token once, to the first browser that accepts it, so the page asks before it sits down: a player who opens
// the link they meant to send, or a second phone opening the same link, would otherwise take a seat somebody
// else is about to play. A link may also carry a seat's own token -- one written before invitations, or the
// address a browser that keeps no storage plays from -- and that one is a seat already: the page goes in.

const NAMES = { player1: 'Player 1', player2: 'Player 2' };

const names = seats => seats.map(one => NAMES[one.seat] ?? one.seat).join(' and ');

/**
 * What to do, from the host's answer about each key (`invitations`) and the seats this browser holds at the
 * tables it saved (`held`, the lobby's resume answer): go in with seats already held, ask before accepting an
 * invitation, or say the invitation is spent. `own` is what this browser already holds at the invited table,
 * which is the player who opened the link meant for somebody else.
 */
export function invitationPlan(invitations, held) {
  const seats = (invitations ?? []).filter(one => one.kind === 'seat').map(({ seat, token }) => ({ seat, token }));
  const offered = (invitations ?? []).filter(one => one.kind === 'invitation');
  // A spent link is most often the player's own, tapped again in the chat to get back to the game: this
  // browser kept the seat it took, and is offered it rather than told somebody else has the place.
  if (offered.length === 0) return seats.length > 0 ? { kind: 'enter', seats } : { kind: 'spent', resumable: (held ?? []).length > 0 };

  const table = offered[0].table;
  const own = ((held ?? []).find(entry => entry.table?.id === table?.id)?.seats ?? [])
    .filter(mine => !offered.some(one => one.seat === mine.seat) && !seats.some(one => one.seat === mine.seat));
  // `seats` are the link's own seat tokens beside its invitation, which only an older table's shared-browser
  // link can carry: they are kept with whatever is accepted.
  return { kind: 'confirm', table, offered: offered.map(({ seat, token }) => ({ seat, token })), own, seats };
}

/** Where the table stands, in a few words: a link should say which game it opens before anybody accepts it. */
function standing(table) {
  if (!table) return '';
  const where = table.waiting ? 'waiting for its players' : table.round ? `round ${table.round}` : 'under way';
  return `${table.format ?? ''} table, ${where}.`.trim();
}

/** The words and the buttons for a plan: what accepting would do, and the way out that keeps things as they are. */
export function invitationText(plan) {
  if (plan.kind === 'unreachable') {
    return {
      title: 'The table could not be reached',
      body: 'Nothing was accepted and nothing was kept. Check the connection, then try the link again.',
      take: null, here: null, keep: 'Try again',
    };
  }

  // Already accepted, replaced, or mistyped: the host does not say which, so neither does the page.
  if (plan.kind === 'spent') {
    return {
      title: 'This invitation can no longer be used',
      body: plan.resumable
        ? 'It was already accepted, replaced, or mistyped. If this browser took the place, your game is kept here: continue it. Otherwise ask the other player for a new code.'
        : 'It was already accepted, replaced, or mistyped. Ask the other player for a new code, or find another table.',
      take: null, here: null, keep: plan.resumable ? 'Continue my game' : null,
    };
  }

  const invited = names(plan.offered);
  if (plan.own.length > 0) {
    return {
      title: `You already play ${names(plan.own)} at this table`,
      body: `This link invites ${invited}. Send it to the other player rather than opening it here: an invitation can be accepted once. Playing ${invited} here too makes this a shared device for both seats.`,
      take: null, here: `Play ${invited} here too`, keep: `Back to ${names(plan.own)}`,
    };
  }

  return {
    title: `Join as ${invited}?`,
    body: `${standing(plan.table)} An invitation can be accepted once: once you take the place, this link and its code stop working for anyone else.`.trim(),
    take: `Take the place as ${invited}`, here: null, keep: null,
  };
}

/** Whether the address is where a code that names no open seat sent the browser (`JoinCodes.Spent`). */
export function spentLink(search) {
  return new URLSearchParams(search ?? '').get('invitation') === 'spent';
}

/**
 * The address once the page holds its seats. Normally the table's own path with no key in it, which on two
 * devices is the difference between a seat and a seat anyone behind you can read. A browser that keeps no
 * storage keeps the seats' tokens in it instead, or a reload would lose a seat whose invitation is spent.
 */
export function addressAfter(path, seats, kept) {
  if (kept) return path;
  const query = new URLSearchParams(seats.map(({ seat, token }) => [seat, token]));
  return `${path}?${query}`;
}
