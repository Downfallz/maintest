import { test } from 'node:test';
import assert from 'node:assert/strict';
import { doorPictures, doorWords, invitationKeys, invitationOf, nameNext } from './door.js';

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
