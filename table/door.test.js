import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { doorName, doorPictures, doorWords, invitationKeys, invitationOf, lessonAsked, lessonWay, nameNext } from './door.js';

const offered = (table, seat = 'player2') => ({ invitations: [{ token: 'inv', kind: 'invitation', seat, table }] });
const bastiens = { format: '3v3', seats: [{ seat: 'player1', kind: 'person', name: 'bastien' }, { seat: 'player2', kind: 'open', name: 'Open seat' }] };

test('a visitor who came from the showcase is asked to sign in, told the game in one line and what an account gives', () => {
  assert.deepEqual(doorWords(null), {
    eyebrow: 'Beta',
    title: 'Sign in to play.',
    game: 'A turn-based duel: your three creatures mutate against another player\'s three.',
    account: 'Your games follow you on every device.',
  });
});

test('an invited friend is told who invites them and to what, and that signing in takes their place', () => {
  const words = doorWords(invitationOf(offered(bastiens)));

  assert.equal(words.eyebrow, 'Invitation');
  assert.equal(words.title, 'bastien invited you to a 3v3 battle.');
  assert.equal(words.account, 'Sign in to take your place. Your games follow you everywhere.');
});

test('an invitation the host has not described yet, or could not be asked about, still says it is one', () => {
  assert.equal(doorWords({}).title, 'You are invited to a battle.');
  assert.equal(doorWords(invitationOf(offered({ format: '2v2', seats: [{ seat: 'player1', kind: 'bot', name: 'Hard' }] }, 'player2'))).title, 'You are invited to a 2v2 battle.');
});

test('the line on the game counts the creatures of the format the friend is invited to', () => {
  assert.equal(doorWords(invitationOf(offered({ ...bastiens, format: '2v2' }))).game, 'A turn-based duel: your two creatures mutate against another player\'s two.');
  assert.equal(doorWords(invitationOf(offered({ ...bastiens, format: '1v1' }))).game, 'A turn-based duel: your creature mutates against another player\'s.');
  assert.equal(doorWords(invitationOf(offered({ ...bastiens, format: '5v5' }))).game, 'A turn-based duel: your creatures mutate against another player\'s.');
});

test('the keys of an invitation are read off the page a visitor is sent back to, and nothing else is', () => {
  assert.deepEqual(invitationKeys('?next=%2F%3Fplayer2%3Dabc'), ['abc']);
  assert.deepEqual(invitationKeys('?next=%2Fplay%3Fplayer1%3Da%26player2%3Db'), ['a', 'b']);
  assert.deepEqual(invitationKeys('?next=%2Fbattle%3Ftable%3Dt1'), []);
  assert.deepEqual(invitationKeys('?next=https%3A%2F%2Felsewhere.example%2F%3Fplayer2%3Dabc'), []);
  assert.deepEqual(invitationKeys(''), []);
});

test('the name window says where the press leads: the invited place, or the first lesson once the host names it', () => {
  assert.equal(nameNext({ invitation: invitationOf(offered(bastiens)) }), 'Next: your place as Player 2 at bastien\'s table.');
  assert.equal(nameNext({ invitation: {} }), 'Next: the battle you were invited to.');
  assert.equal(nameNext({ lesson: { order: 1, title: 'Your first combat' } }), 'Next: Tutorial 1, Your first combat.');
  assert.equal(nameNext({}), null);
});

test('a link the host says is spent is not called an invitation that waits: the visitor is asked to sign in to play', () => {
  const spent = invitationOf({ invitations: [{ token: 'inv', kind: 'spent', seat: null, table: null }] });

  assert.deepEqual(doorWords(spent), {
    eyebrow: 'Invitation',
    title: 'This invitation can no longer be used.',
    game: doorWords(null).game,
    account: 'Sign in to play. Your games follow you on every device.',
  });
  assert.equal(nameNext({ invitation: spent }), null);
});

test('a link that carries the visitor\'s own seat is a way back to their game, not an invitation', () => {
  assert.equal(invitationOf({ invitations: [{ token: 'mine', kind: 'seat', seat: 'player1', table: bastiens }] }), null);
});

test('the panel an invitation opens shows the duel, two teams of three, in place of the ground and the Revenant', () => {
  assert.deepEqual(doorPictures(null), ['eclipse-ground', 'revenant']);
  assert.deepEqual(doorPictures({}), ['duel'], 'before the host describes it');
  assert.deepEqual(doorPictures(invitationOf(offered(bastiens))), ['duel']);
  assert.deepEqual(doorPictures({ spent: true }), ['duel'], 'a spent link still came from a friend who never saw the showcase');
});

test('a player who just signed out here is told goodbye and that their games wait, and an invitation outranks it', () => {
  assert.deepEqual(doorWords(null, { left: true }), {
    eyebrow: 'Signed out',
    title: 'See you soon.',
    game: 'Your games wait for you.',
    account: 'Sign in again to resume them.',
  });
  assert.equal(doorWords(invitationOf(offered(bastiens)), { left: true }).title, 'bastien invited you to a 3v3 battle.');
});

test('the panel after a sign out shows the Predator at rest alone, in place of the ground and the Revenant', () => {
  assert.deepEqual(doorPictures(null, { left: true }), ['predator-at-rest']);
  assert.deepEqual(doorPictures({}, { left: true }), ['duel'], 'an invitation outranks the goodbye');
});

test('beside the words on a wide screen, the Predator at rest is cut on its dark left, so its jaw stays in the panel', () => {
  const source = readFileSync(new URL('./door.js', import.meta.url), 'utf8');
  assert.match(source, /'predator-at-rest': \{[^}]*focus: '90% 50%'/);
});

// UX15, a lesson before an account (docs/art/journeys/doors.md): the lesson door, the plain door's second way, and the
// guest's keep panel.
test('the showcase\'s Try a tutorial first asks for the lesson door, and nothing else does', () => {
  assert.equal(lessonAsked('?try=lesson'), true);
  assert.equal(lessonAsked('?try=Lesson'), false);
  assert.equal(lessonAsked('?next=%2F%3Ftry%3Dlesson'), false);
  assert.equal(lessonAsked(''), false);
});

test('the lesson door offers tutorial 1 first, against a bot, with no account, and sign-in after', () => {
  assert.deepEqual(doorWords(null, { lesson: true }), {
    eyebrow: 'Beta',
    title: 'Learn the game first.',
    game: 'A turn-based duel: your three creatures mutate against another player\'s three.',
    account: 'Tutorial 1, against a bot. No account needed.',
  });
});

test('a guest\'s home asks it to keep its tutorials, says where they are kept and what an account opens', () => {
  assert.deepEqual(doorWords(null, { guest: true }), {
    eyebrow: 'Playing as a guest',
    title: 'Keep your tutorials.',
    game: '',
    account: 'Your progress is kept in this browser only. An account keeps it on every device, and opens battles against bots and players, friends and the ladder.',
  });
});

test('the lesson is filled on the lesson door, an outline on the plain door, and not offered to an invited friend, a player who signed out or a guest', () => {
  assert.deepEqual(lessonWay(null), { offered: true, filled: false });
  assert.deepEqual(lessonWay(null, { lesson: true }), { offered: true, filled: true });
  assert.deepEqual(lessonWay({}, { lesson: true }), { offered: false, filled: false });
  assert.deepEqual(lessonWay(null, { left: true }), { offered: false, filled: false });
  assert.deepEqual(lessonWay(null, { guest: true }), { offered: false, filled: false });
});

test('a guest\'s keep panel draws no painting: the featured lesson above it says where', () => {
  assert.deepEqual(doorPictures(null, { guest: true }), []);
  assert.deepEqual(doorPictures(null, { lesson: true }), ['eclipse-ground', 'revenant']);
});

test('the name window tells a guest its tutorials and settings stay with the name it chooses', () => {
  assert.equal(nameNext({ guest: true }), 'Your tutorials and settings stay with this name.');
});

test('the panel names its door, a guest first, then an invitation, a goodbye, the lesson, and the plain door last', () => {
  assert.equal(doorName({ guest: true, invitation: {}, left: true, lesson: true }), 'guest');
  assert.equal(doorName({ invitation: {}, left: true, lesson: true }), 'invited');
  assert.equal(doorName({ left: true, lesson: true }), 'left');
  assert.equal(doorName({ lesson: true }), 'lesson');
  assert.equal(doorName(), 'plain');
});
