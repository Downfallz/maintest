import { test } from 'node:test';
import assert from 'node:assert/strict';
import { activeSeat, holdsOn, isAsked, needsPass } from './seats.js';

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

test('of two seats asked at once, the one holding the device stays on screen', () => {
  const both = [askedSeat('player1'), askedSeat('player2')];

  assert.equal(activeSeat(both, 'player2').seat, 'player2');
  assert.equal(activeSeat(both, 'player1').seat, 'player1');
});

test('between two of the holder\'s own questions the device is not handed over for one poll', () => {
  const asking = [askedSeat('player1'), askedSeat('player2')];
  const between = [waiting('player1'), askedSeat('player2')];

  const holding = holdsOn(asking, between, 'player1');

  assert.equal(holding, true);
  assert.equal(activeSeat(between, 'player1', holding).seat, 'player1');
  assert.equal(needsPass(activeSeat(between, 'player1', holding), 'player1'), false);
});

test('a holder who has answered everything hands the device over on the next poll', () => {
  const between = [waiting('player1'), askedSeat('player2')];

  const holding = holdsOn(between, between, 'player1');

  assert.equal(holding, false);
  assert.equal(activeSeat(between, 'player1', holding).seat, 'player2');
  assert.equal(needsPass(activeSeat(between, 'player1', holding), 'player1'), true);
});

test('nothing is held when nobody else is asked or nobody holds the device', () => {
  const asking = [askedSeat('player1'), waiting('player2')];
  const idle = [waiting('player1'), waiting('player2')];

  assert.equal(holdsOn(asking, idle, 'player1'), false);
  assert.equal(holdsOn([askedSeat('player1'), askedSeat('player2')], [waiting('player1'), askedSeat('player2')], null), false);
  assert.equal(holdsOn([], [askedSeat('player2')], 'player1'), false);
});
