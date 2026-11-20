// The game's door (docs/art/journeys/doors.md, UX7): the signed-out panel's words, plain or invited, its paintings,
// and the line the name window carries to where the press leads. A visitor who came by a friend's invitation never
// saw the showcase, so the panel says who invites them and to what. The host says it: the page asks the same route
// the table's page asks before a seat is taken (`/api/lobby/invitations`, ADR 0116), which needs the link's key and
// no account, spends nothing, and answers a seat kept for a friend as spent to anybody else (ADR 0129).
import { inviterOf, seatLabel } from './invitation.js';
import { pictureElement } from './picture.js';
import { seatsFromLocation } from './session.js';

const COUNTS = { 1: null, 2: 'two', 3: 'three' };

/** The line on the game, counting the creatures of the format: three for the plain door, which plays 3v3. */
function gameLine(format) {
  const per = Number(/^(\d+)v\1$/.exec(format ?? '3v3')?.[1]);
  if (per === 1) return 'A turn-based duel: your creature mutates against another player\'s.';
  const count = COUNTS[per];
  return count
    ? `A turn-based duel: your ${count} creatures mutate against another player's ${count}.`
    : 'A turn-based duel: your creatures mutate against another player\'s.';
}

/**
 * What the panel says. `invitation` is null for a plain visit; an object for an invitation, holding `inviter` and
 * `format` once the host has described it, and nothing while it has not or cannot.
 */
export function doorWords(invitation) {
  if (!invitation) {
    return { eyebrow: 'Beta', title: 'Sign in to play.', game: gameLine('3v3'), account: 'Your games follow you on every device.' };
  }
  if (invitation.spent) {
    return { eyebrow: 'Invitation', title: 'This invitation can no longer be used.', game: gameLine('3v3'), account: 'Sign in to play. Your games follow you on every device.' };
  }
  const battle = invitation.format ? `a ${invitation.format} battle` : 'a battle';
  return {
    eyebrow: 'Invitation',
    title: invitation.inviter ? `${invitation.inviter} invited you to ${battle}.` : `You are invited to ${battle}.`,
    game: gameLine(invitation.format),
    account: 'Sign in to take your place. Your games follow you everywhere.',
  };
}

/** The keys an invitation carries in the page a visitor is sent back to after signing in, and only this host's. */
export function invitationKeys(search) {
  const next = new URLSearchParams(search ?? '').get('next');
  if (!next || !(next.startsWith('/?') || next.startsWith('/play?'))) return [];
  return seatsFromLocation(next.slice(next.indexOf('?'))).map(one => one.token);
}

/**
 * The invitation as the host described it: its inviter, its format and the seat. Null when the link holds the
 * visitor's own seat, which is a way back to their game; spent when the host offers nothing (ADR 0116, 0129).
 */
export function invitationOf(answer) {
  const answers = answer?.invitations ?? [];
  const offered = answers.find(one => one.kind === 'invitation' && one.table);
  if (!offered) return answers.some(one => one.kind === 'seat') ? null : { spent: true };
  return { inviter: inviterOf(offered.table, [offered]), format: offered.table.format ?? null, seat: offered.seat };
}

/** Where the name window's press leads, said in it so the cue survives the page that changes after it. */
export function nameNext({ invitation, lesson } = {}) {
  if (invitation?.spent) return null;
  if (invitation) {
    return invitation.inviter && invitation.seat
      ? `Next: your place as ${seatLabel(invitation.seat)} at ${invitation.inviter}'s table.`
      : 'Next: the battle you were invited to.';
  }
  if (lesson?.title) return `Next: Tutorial ${lesson.order ?? 1}, ${lesson.title}.`;
  return null;
}

const asked = tokens => globalThis.fetch('/api/lobby/invitations', {
  method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ tokens }),
}).then(response => (response.ok ? response.json() : null));

/**
 * Draws the panel's words, and asks the host about an invitation when the address carries one. Returns
 * `signedOut()`, which places the paintings once the host has said nobody is signed in, so a player's home never
 * loads them; and `lesson(card)`, which the lobby calls when the first tutorial is about to open, to say so in the
 * name window.
 */
export function mountDoor(doc, { search = globalThis.location?.search ?? '', ask = asked } = {}) {
  const el = id => doc.getElementById(id);

  let invitation = null;
  const draw = () => {
    const words = doorWords(invitation);
    el('sign-in-eyebrow').textContent = words.eyebrow;
    el('sign-in-heading').textContent = words.title;
    el('sign-in-game').textContent = words.game;
    el('sign-in-account').textContent = words.account;
    el('sign-in-panel').dataset.door = invitation ? 'invited' : 'plain';
    const next = nameNext({ invitation });
    // Never hidden, only empty: a live region shown and filled in one step is often not announced.
    el('name-next').textContent = next ?? '';
  };

  const keys = invitationKeys(search);
  if (keys.length) {
    invitation = {};
    // Not knowing who invites keeps the plain invitation's words: the panel still says it is one.
    ask(keys).then(answer => { if (answer) { invitation = invitationOf(answer); draw(); } }, () => {});
  }
  draw();

  return {
    signedOut() {
      const art = el('sign-in-art');
      if (!art || art.childElementCount) return;
      art.append(
        pictureElement(doc, 'eclipse-ground', { page: 'table/sign-in', class: 'sign-in-ground', sizes: '(min-width: 701px) 100vw, 533px' }),
        pictureElement(doc, 'revenant', { page: 'table/sign-in', class: 'sign-in-creature', sizes: '(min-width: 701px) 550px, 107px' }),
      );
    },
    lesson(card) {
      const next = nameNext({ lesson: card });
      if (!next) return;
      el('name-next').textContent = next;
    },
  };
}
