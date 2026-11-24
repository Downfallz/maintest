// The game's door (docs/art/journeys/doors.md, UX7): the signed-out panel's words, plain or invited, its paintings,
// and the line the name window carries to where the press leads. A visitor who came by a friend's invitation never
// saw the showcase, so the panel says who invites them and to what. The host says it: the page asks the same route
// the table's page asks before a seat is taken (`/api/lobby/invitations`, ADR 0116), which needs the link's key and
// no account, spends nothing, and answers a seat kept for a friend as spent to anybody else (ADR 0129).
import { inviterOf, seatLabel } from './invitation.js';
import { takeSignedOut } from './account.js';
import { pictureElement, sceneElement } from './picture.js';
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

/** The widest name a player can take: twenty characters (`nameProblem` in account.js) of the widest letter. */
const LONGEST_NAME = 'W'.repeat(20);

/**
 * What the panel says. `invitation` is null for a plain visit; an object for an invitation, holding `inviter` and
 * `format` once the host has described it, and nothing while it has not or cannot.
 */
export function doorWords(invitation, { left = false } = {}) {
  // An invitation outranks the goodbye: it says why the visitor is here.
  if (!invitation && left) {
    return { eyebrow: 'Signed out', title: 'See you soon.', game: 'Your games wait for you.', account: 'Sign in again to resume them.' };
  }
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

/**
 * The paintings the panel draws (docs/art/journeys/doors.md, the sign-in panel). The plain door repeats the
 * showcase's ground and a creature; a friend who came by an invitation never saw the showcase, so the duel shows
 * them the game at once: two teams of three facing across the ground. A spent link came from a friend all the same.
 * A player who just signed out here sees the Predator at rest, alone: the game waits for them (the panel after a
 * sign out).
 */
export function doorPictures(invitation, { left = false } = {}) {
  if (invitation) return ['duel'];
  return left ? ['predator-at-rest'] : ['eclipse-ground', 'revenant'];
}

// The duel's steps follow table/lobby.css: beside the words from 1280 px, its file drawn about 980 px wide at the
// panel's height; whole above them under it.
const PICTURES = {
  'eclipse-ground': { class: 'sign-in-ground', sizes: '(min-width: 701px) 100vw, 533px' },
  revenant: { class: 'sign-in-creature', sizes: '(min-width: 701px) 550px, 107px' },
  duel: { class: 'sign-in-ground sign-in-duel', sizes: '(min-width: 1280px) 980px, (min-width: 701px) 94vw, calc(100vw - 32px)' },
  // Laid out as the duel is, a plate of its own: whole above the words, beside them from 1280 px. There the panel cuts
  // its sides, so the cut keeps its right edge, where the Predator's jaw ends (at 91% of its width), and gives up the
  // dark left that the words' fade covers.
  'predator-at-rest': {
    class: 'sign-in-ground sign-in-duel', form: 'plate', alt: 'The Predator at rest in a dark hall, an ember on its shoulder.',
    sizes: '(min-width: 1280px) 980px, (min-width: 701px) 94vw, calc(100vw - 32px)', focus: '90% 50%',
  },
};

const drawn = (doc, name) => {
  const { form, ...options } = PICTURES[name];
  return form === 'plate'
    ? sceneElement(doc, name, { page: 'table/sign-in', form, ...options })
    : pictureElement(doc, name, { page: 'table/sign-in', ...options });
};

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

function tabStorage() {
  try { return globalThis.sessionStorage ?? null; } catch { return null; }
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
export function mountDoor(doc, { search = globalThis.location?.search ?? '', ask = asked, session = tabStorage() } = {}) {
  const el = id => doc.getElementById(id);

  let invitation = null;
  let signedOut = false;
  // This tab just signed out (account.js marks it once the host confirmed): the goodbye, once.
  let left = false;
  // The paintings follow the door: a link the host answers as the visitor's own seat turns the panel plain.
  const place = () => {
    const art = el('sign-in-art');
    const names = doorPictures(invitation, { left });
    if (!art || !signedOut || art.dataset.pictures === names.join(' ')) return;
    art.dataset.pictures = names.join(' ');
    art.replaceChildren(...names.map(name => drawn(doc, name)));
  };
  const draw = () => {
    const words = doorWords(invitation, { left });
    el('sign-in-eyebrow').textContent = words.eyebrow;
    el('sign-in-title').textContent = words.title;
    // An invitation's title waits for the host to name who invites: its room is held for the longest it can be, a
    // name of twenty of the widest letters (account.js), so a late answer never moves the sign-in under 1280 px (#973).
    // The format is not known before the host answers either: the room assumes one. A spent link keeps the room too.
    el('sign-in-title-room').textContent = invitation ? doorWords({ inviter: LONGEST_NAME, format: invitation.format ?? '3v3' }).title : '';
    el('sign-in-game').textContent = words.game;
    el('sign-in-account').textContent = words.account;
    el('sign-in-panel').dataset.door = invitation ? 'invited' : left ? 'left' : 'plain';
    const next = nameNext({ invitation });
    // Never hidden, only empty: a live region shown and filled in one step is often not announced.
    el('name-next').textContent = next ?? '';
    place();
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
      signedOut = true;
      if (takeSignedOut(session)) {
        left = true;
        draw();
        // The account menu that held the focus is gone: the goodbye's title takes it, once.
        if (!invitation) el('sign-in-heading').focus({ preventScroll: true });
        return;
      }
      place();
    },
    lesson(card) {
      const next = nameNext({ lesson: card });
      if (!next) return;
      el('name-next').textContent = next;
    },
  };
}
