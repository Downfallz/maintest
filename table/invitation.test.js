import { test } from 'node:test';
import assert from 'node:assert/strict';
import { addressAfter, invitationPlan, invitationText, spentLink } from './invitation.js';

const table = { id: 't1', format: '3v3', waiting: true, round: null };
const invitation = (seat, token, at = table) => ({ token, kind: 'invitation', seat, table: at });

test('a link of seats already held goes straight in, as an older link or a storage-less reload does', () => {
  const plan = invitationPlan([{ token: 'cafe', kind: 'seat', seat: 'player1', table }], []);

  assert.deepEqual(plan, { kind: 'enter', seats: [{ seat: 'player1', token: 'cafe' }] });
});

test('a spent invitation is said to be spent, and nothing is held', () => {
  const plan = invitationPlan([{ token: 'gone', kind: 'spent', seat: null, table: null }], []);

  assert.deepEqual(plan, { kind: 'spent', resumable: false });
  assert.equal(invitationText(plan).take, null);
  assert.equal(invitationText(plan).keep, null);
  assert.equal(invitationText(plan).title, 'This invitation can no longer be used');
});

test('a link the player already accepted, tapped again, offers the game this browser kept', () => {
  const held = [{ table: { id: 't1' }, seats: [{ seat: 'player2', token: 'mine' }] }];

  const plan = invitationPlan([{ token: 'gone', kind: 'spent', seat: null, table: null }], held);

  assert.deepEqual(plan, { kind: 'spent', resumable: true });
  assert.equal(invitationText(plan).keep, 'Continue my game');
  assert.match(invitationText(plan).body, /If this browser took the place/);
  assert.match(invitationText(plan).body, /already accepted, replaced, or mistyped/);
});

test('a host that cannot be reached says nothing was accepted, and offers to try again', () => {
  const text = invitationText({ kind: 'unreachable' });

  assert.match(text.body, /Nothing was accepted/);
  assert.equal(text.keep, 'Try again');
  assert.equal(text.take, null);
});

test('a seat token beside an invitation, from an older shared-browser link, is kept with it', () => {
  const plan = invitationPlan([{ token: 'old', kind: 'seat', seat: 'player1', table }, invitation('player2', 'inv')], [
    { table: { id: 't1' }, seats: [{ seat: 'player1', token: 'old' }] },
  ]);

  assert.equal(plan.kind, 'confirm');
  assert.deepEqual(plan.seats, [{ seat: 'player1', token: 'old' }]);
  assert.deepEqual(plan.own, [], 'the link\'s own seat is not offered back as one held elsewhere');
});

test('an invitation is asked about before it is accepted, and says it can be accepted once', () => {
  const plan = invitationPlan([invitation('player2', 'inv')], []);

  assert.equal(plan.kind, 'confirm');
  assert.deepEqual(plan.offered, [{ seat: 'player2', token: 'inv' }]);
  assert.deepEqual(plan.own, []);
  const text = invitationText(plan);
  assert.equal(text.title, 'Join as Player 2?');
  assert.match(text.body, /3v3 table, waiting for its players/);
  assert.match(text.body, /accepted once/);
  assert.equal(text.take, 'Take the place as Player 2');
  assert.equal(text.here, null);
});

test('the player who opens the link they meant to send is offered their own seat back first', () => {
  const held = [
    { table: { id: 'other' }, seats: [{ seat: 'player2', token: 'elsewhere' }] },
    { table: { id: 't1' }, seats: [{ seat: 'player1', token: 'mine' }] },
  ];

  const plan = invitationPlan([invitation('player2', 'inv')], held);

  assert.deepEqual(plan.own, [{ seat: 'player1', token: 'mine' }]);
  const text = invitationText(plan);
  assert.equal(text.title, 'You already play Player 1 at this table');
  assert.match(text.body, /Send it to the other player/);
  assert.equal(text.take, null);
  assert.equal(text.keep, 'Back to Player 1');
  assert.equal(text.here, 'Play Player 2 here too');
});

test('a seat the link itself invites is not counted as one this browser already holds', () => {
  const held = [{ table: { id: 't1' }, seats: [{ seat: 'player2', token: 'old' }] }];

  assert.deepEqual(invitationPlan([invitation('player2', 'inv')], held).own, []);
});

test('one link for a shared browser invites both seats together', () => {
  const plan = invitationPlan([invitation('player1', 'a'), invitation('player2', 'b')], []);

  assert.equal(invitationText(plan).take, 'Take the place as Player 1 and Player 2');
});

test('the address keeps no key once the seats are kept, and keeps them when nothing can be', () => {
  const seats = [{ seat: 'player2', token: 'beef' }];

  assert.equal(addressAfter('/play', seats, true), '/play');
  assert.equal(addressAfter('/play', seats, false), '/play?player2=beef');
});

test('the address a spent code sends the browser to is told from any other', () => {
  assert.equal(spentLink('?invitation=spent'), true);
  assert.equal(spentLink('?player2=beef'), false);
  assert.equal(spentLink(''), false);
});
