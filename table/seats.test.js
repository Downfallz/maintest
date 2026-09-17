import { test } from 'node:test';
import assert from 'node:assert/strict';
import { activeSeat, isAsked, needsPass } from './seats.js';

const waiting = seat => ({ seat, view: { seat, waitingFor: null } });
const askedSeat = (seat, kind = 'Speed') => ({ seat, view: { seat, waitingFor: kind } });

test('the seat on screen is the one the match is asking, whichever seat the link was opened for', () => {
  const views = [waiting('player1'), askedSeat('player2')];

  assert.equal(activeSeat(views, 'player1').seat, 'player2');
});

test('the active seat follows the question from one seat to the other', () => {
  const first = [askedSeat('player1'), waiting('player2')];
  const then = [waiting('player1'), askedSeat('player2')];

  assert.equal(activeSeat(first, 'player1').seat, 'player1');
  assert.equal(activeSeat(then, 'player1').seat, 'player2');
});

test('with nobody asked the screen stays on the seat holding the device', () => {
  const views = [waiting('player1'), waiting('player2')];

  assert.equal(activeSeat(views, 'player2').seat, 'player2');
  assert.equal(activeSeat(views, null).seat, 'player1');
});

test('a page holding one seat shows that seat, asked or not', () => {
  assert.equal(activeSeat([waiting('player2')], null).seat, 'player2');
  assert.equal(activeSeat([], null), null);
});

test('the device is passed when the seat being asked is not the one holding it', () => {
  const asking = askedSeat('player2');

  assert.equal(needsPass(asking, 'player1'), true);
  assert.equal(needsPass(asking, 'player2'), false);
  assert.equal(needsPass(waiting('player2'), 'player1'), false);
});

test('a seat is asked only when the host named a question for it', () => {
  assert.equal(isAsked({ waitingFor: 'Target' }), true);
  assert.equal(isAsked({ waitingFor: null }), false);
  assert.equal(isAsked({}), false);
});
