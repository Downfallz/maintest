// The game's door (docs/art/journeys/doors.md, UX7): the signed-out panel's words, plain or invited, its paintings,
// and the line the name window carries to where the press leads. A visitor who came by a friend's invitation never
// saw the showcase, so the panel says who invites them and to what. The host says it: the page asks the same route
// the table's page asks before a seat is taken (`/api/lobby/invitations`, ADR 0116), which needs the link's key and
// no account, spends nothing, and answers a seat kept for a friend as spent to anybody else (ADR 0129).
import { inviterOf, seatLabel } from './invitation.js';
import { accountRequest, takeSignedOut } from './account.js';
import { OFFERED_KEY, firstTutorial } from './lobby-tutorials.js';
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
export function doorWords(invitation, { left = false, lesson = false, guest = false } = {}) {
  // A guest is signed in: its home asks it to keep what it learned (UX15, ADR 0207).
  if (guest) {
    return {
      eyebrow: 'Playing as a guest', title: 'Keep your tutorials.', game: '',
      account: 'Your progress is kept in this browser only. An account keeps it on every device, and opens battles against bots and players, friends and the ladder.',
    };
  }
  // An invitation outranks the goodbye: it says why the visitor is here.
  if (!invitation && left) {
    return { eyebrow: 'Signed out', title: 'See you soon.', game: 'Your games wait for you.', account: 'Sign in again to resume them.' };
  }
  if (!invitation && lesson) {
    return { eyebrow: 'Beta', title: 'Learn the game first.', game: gameLine('3v3'), account: 'Tutorial 1, against a bot. No account needed.' };
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
export function doorPictures(invitation, { left = false, guest = false } = {}) {
  // A guest's keep panel stands under the featured lesson, whose painting says where (UX15).
  if (guest) return [];
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

/**
 * Whether the address asks for the lesson door: the showcase's Try a tutorial first (`/lobby?try=lesson`, UX15). Only the
 * address's own `try`; nothing is made on the page's load, the guest is made on the press.
 */
export function lessonAsked(search) {
  return new URLSearchParams(search ?? '').get('try') === 'lesson';
}

/**
 * Whether the door offers tutorial 1 as a guest (ADR 0207, UX15), and whether as its one filled action: on the lesson
 * door it is, on the plain door it is an outline under the ways in. A seat needs an account, a player who just signed out
 * has one, and a guest is already one: none is offered it.
 */
export function lessonWay(invitation, { left = false, lesson = false, guest = false } = {}) {
  const offered = !invitation && !left && !guest;
  return { offered, filled: offered && lesson };
}

/** Where the name window's press leads, said in it so the cue survives the page that changes after it. */
export function nameNext({ invitation, lesson, guest = false } = {}) {
  if (guest) return 'Your tutorials and settings stay with this name.';
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
export function mountDoor(doc, { search = globalThis.location?.search ?? '', ask = asked, session = tabStorage(), request = accountRequest, go = link => globalThis.location.assign(link) } = {}) {
  const el = id => doc.getElementById(id);

  let invitation = null;
  let signedOut = false;
  // This tab just signed out (account.js marks it once the host confirmed): the goodbye, once.
  let left = false;
  // The showcase's Try a tutorial first (UX15): the lesson first. A guest signed in: its keep panel.
  const lesson = lessonAsked(search);
  let guest = false;
  // The paintings follow the door: a link the host answers as the visitor's own seat turns the panel plain.
  const place = () => {
    const art = el('sign-in-art');
    const names = doorPictures(invitation, { left, guest });
    if (!art || !(signedOut || guest) || art.dataset.pictures === names.join(' ')) return;
    art.dataset.pictures = names.join(' ');
    art.replaceChildren(...names.map(name => drawn(doc, name)));
  };
  // The lesson's way (UX15): filled and first on the lesson door, an outline under the ways in on the plain door.
  const offer = () => {
    const way = el('lesson-way'), form = el('dev-sign-in'), start = el('guest-start');
    if (!way || !start) return;
    const { offered, filled } = lessonWay(invitation, { left, lesson, guest });
    way.hidden = !offered;
    start.classList.toggle('primary', filled);
    start.classList.toggle('secondary', !filled);
    el('guest-start-line').hidden = filled;
    el('sign-in-or').hidden = !filled;
    // The reading and the focus order follow what is filled: the lesson before the ways in on the lesson door.
    const or = el('sign-in-or');
    if (filled && or && way.nextElementSibling !== or) or.before(way);
    if (!filled && form && form.nextElementSibling !== way) form.after(way);
    // The developer's Sign in is the filled action only where nothing else is: an outline beside the lesson, and on a
    // guest's keep panel while a lesson is featured above it (the lobby says so with `data-lesson-ahead`).
    const quiet = filled || (guest && el('sign-in-panel').dataset.lessonAhead === 'true');
    el('dev-sign-in-submit')?.classList.toggle('primary', !quiet);
    el('dev-sign-in-submit')?.classList.toggle('secondary', quiet);
  };
  const draw = () => {
    const words = doorWords(invitation, { left, lesson, guest });
    el('sign-in-eyebrow').textContent = words.eyebrow;
    el('sign-in-title').textContent = words.title;
    // An invitation's title waits for the host to name who invites: its room is held for the longest it can be, a
    // name of twenty of the widest letters (account.js), so a late answer never moves the sign-in under 1280 px (#973).
    // The format is not known before the host answers either: the room assumes one. A spent link keeps the room too.
    el('sign-in-title-room').textContent = invitation ? doorWords({ inviter: LONGEST_NAME, format: invitation.format ?? '3v3' }).title : '';
    el('sign-in-game').textContent = words.game;
    el('sign-in-game').hidden = !words.game;
    el('sign-in-account').textContent = words.account;
    el('sign-in-panel').dataset.door = guest ? 'guest' : invitation ? 'invited' : left ? 'left' : lesson ? 'lesson' : 'plain';
    const next = nameNext({ invitation, guest });
    // Never hidden, only empty: a live region shown and filled in one step is often not announced.
    el('name-next').textContent = next ?? '';
    offer();
    place();
  };

  const keys = invitationKeys(search);
  if (keys.length) {
    invitation = {};
    // Not knowing who invites keeps the plain invitation's words: the panel still says it is one.
    ask(keys).then(answer => { if (answer) { invitation = invitationOf(answer); draw(); } }, () => {});
  }
  draw();

  // Play the first tutorial (ADR 0207, UX15): the host makes the guest on the press, never on the page's load, so a
  // crawler that follows the showcase's link spends none of the guests an hour; then the page goes to tutorial 1.
  const start = el('guest-start'), startWords = start?.textContent ?? '';
  start?.addEventListener('click', async () => {
    const words = startWords, problem = el('sign-in-problem');
    problem.hidden = true; problem.textContent = '';
    start.disabled = true; start.textContent = 'Opening the tutorial…'; start.setAttribute('aria-busy', 'true');
    let leaving = false;
    try {
      await request('/api/account/guest', {});
      const link = firstTutorial(await request('/api/tutorials').catch(() => null));
      // The guest is made: with no tutorial on this host, its home says so.
      leaving = true;
      if (link) { try { session?.setItem?.(OFFERED_KEY, '1'); } catch { /* Opened from the press all the same. */ } go(link); } else go('/');
    } catch (error) {
      problem.textContent = error?.status ? error.message : 'Could not reach the host. Check the connection and try again.';
      problem.hidden = false;
    } finally {
      // A page on its way to the lesson keeps the button as it is: a second press would ask again.
      if (!leaving) { start.disabled = false; start.textContent = words; start.removeAttribute('aria-busy'); }
    }
  });
  // A page brought back from the back-forward cache gives the button back.
  globalThis.addEventListener?.('pageshow', event => { if (event.persisted && start) { start.disabled = false; start.textContent = startWords; start.removeAttribute('aria-busy'); } });

  return {
    signedOut() {
      signedOut = true;
      if (guest) { guest = false; draw(); }
      if (takeSignedOut(session)) {
        left = true;
        draw();
        // The account menu that held the focus is gone: the goodbye's title takes it, once.
        if (!invitation) el('sign-in-heading').focus({ preventScroll: true });
        return;
      }
      place();
    },
    /** A guest is signed in: the panel becomes its keep panel, the ways in kept (UX15). */
    guest() {
      signedOut = false;
      if (!guest) { guest = true; left = false; draw(); }
    },
    /** A member is signed in: the panel is hidden by account.js; the door forgets the guest. */
    member() {
      if (guest) { guest = false; draw(); }
    },
    /** Whether a lesson is featured above a guest's keep panel: its sign-in is then an outline. */
    lessonAhead(ahead) {
      el('sign-in-panel').dataset.lessonAhead = String(Boolean(ahead));
      offer();
    },
    lesson(card) {
      const next = nameNext({ lesson: card });
      if (!next) return;
      el('name-next').textContent = next;
    },
  };
}
