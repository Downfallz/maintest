import test from 'node:test';
import assert from 'node:assert/strict';
import { BUBBLE_MS, createBattleReactions, REACTIONS, reactionOf, saveSeenReaction, seenReaction, sentAgo } from './battle-reactions.js';

function memory() {
  const kept = new Map();
  return { getItem: key => kept.get(key) ?? null, setItem: (key, value) => kept.set(key, String(value)) };
}
// A clock the test turns by hand.
function clock(start = Date.parse('2026-11-13T20:00:00Z')) {
  const jobs = new Map(); let next = 0, at = start;
  return {
    now: () => at,
    schedule: (run, delay) => { jobs.set(++next, { run, due: at + delay }); return next; },
    cancel: id => jobs.delete(id),
    advance(ms) { at += ms; for (const [id, job] of [...jobs]) if (job.due <= at) { jobs.delete(id); job.run(); } },
    jobs,
  };
}
const view = (recent, extra = {}) => ({ reactions: { available: true, cooldownMs: 4000, recent }, ...extra });
const sent = (sequence, seat, reaction, at = '2026-11-13T20:00:00Z') => ({ sequence, seat, reaction, at });

function mounted({ transport = { react: async () => ({ ok: true, status: 200, body: { reaction: { sequence: 1 } } }) }, seat = 'player1', hotseat = false, storage = memory(), time = clock() } = {}) {
  let changes = 0;
  const reactions = createBattleReactions({ onChange: () => changes++, storage, ...time });
  reactions.reset(transport, `t1:${seat}`, seat, { hotseat });
  return { reactions, storage, time, changes: () => changes };
}

test('the set holds eight reactions, each a label and a glyph, and nothing else is a reaction', () => {
  assert.equal(REACTIONS.length, 8);
  assert.deepEqual(REACTIONS.map(one => one.id), ['hello', 'good-luck', 'well-played', 'nice-move', 'oops', 'thinking', 'thanks', 'good-game']);
  assert.equal(reactionOf('well-played').label, 'Well played!');
  assert.equal(reactionOf('you are bad'), null);
});

test('the other player\'s newest reaction this browser has not shown is the bubble, and only once', () => {
  const { reactions, storage } = mounted();

  reactions.receive(view([sent(1, 'player2', 'hello'), sent(2, 'player2', 'good-luck')]));

  assert.equal(reactions.model().theirs.label, 'Good luck!');
  assert.equal(seenReaction(storage, 't1:player1'), 2);
  const again = mounted({ storage });
  again.reactions.receive(view([sent(1, 'player2', 'hello'), sent(2, 'player2', 'good-luck')]));
  assert.equal(again.reactions.model().theirs, null);
});

test('a bubble goes after a few seconds', () => {
  const { reactions, time } = mounted();
  reactions.receive(view([sent(1, 'player2', 'oops')]));

  time.advance(BUBBLE_MS);

  assert.equal(reactions.model().theirs, null);
});

test('a reaction sent while this player was away says how long ago', () => {
  const { reactions, time } = mounted();
  time.advance(3 * 60 * 60 * 1000);

  reactions.receive(view([sent(1, 'player2', 'well-played')]));

  assert.equal(reactions.model().theirs.ago, '3 h ago');
  assert.equal(sentAgo('2026-11-13T20:00:00Z', Date.parse('2026-11-13T20:00:30Z')), '');
});

test('this seat\'s own reactions, sent from another device, are not shown as the other player\'s', () => {
  const { reactions } = mounted();

  reactions.receive(view([sent(1, 'player1', 'hello')]));

  assert.equal(reactions.model().theirs, null);
});

test('hidden reactions are counted as shown and never drawn', () => {
  const { reactions, storage } = mounted();
  reactions.setShown(false);

  reactions.receive(view([sent(1, 'player2', 'hello')]));

  assert.equal(reactions.model().theirs, null);
  assert.equal(seenReaction(storage, 't1:player1'), 1);
});

test('a shared device holding both seats offers no reactions, whatever the host says', () => {
  const { reactions } = mounted({ hotseat: true });

  reactions.receive(view([sent(1, 'player2', 'hello')]));

  assert.equal(reactions.model().available, false);
  assert.equal(reactions.model().theirs, null);
});

test('a replay offers no reactions', () => {
  const { reactions } = mounted();

  reactions.receive(view([], { replay: true }));

  assert.equal(reactions.model().available, false);
});

test('a sent reaction shows at once, then cools down for as long as the host says', async () => {
  const posted = [];
  const { reactions, time } = mounted({ transport: { react: async id => { posted.push(id); return { ok: true, status: 200, body: { reaction: { sequence: 3 } } }; } } });
  reactions.receive(view([]));

  const sending = reactions.send('nice-move');
  assert.equal(reactions.model().mine.label, 'Nice move!');
  assert.equal(await sending, true);

  assert.deepEqual(posted, ['nice-move']);
  assert.equal(reactions.model().coolingMs, 4000);
  assert.equal(await reactions.send('thanks'), false, 'a second tap during the cooldown sends nothing');
  time.advance(4000);
  assert.equal(reactions.model().coolingMs, 0);
});

test('a reaction the host says came too soon is taken back and waits as long as it says', async () => {
  const { reactions } = mounted({ transport: { react: async () => ({ ok: false, status: 429, body: { retryAfterMs: 1500 } }) } });
  reactions.receive(view([]));

  await reactions.send('hello');

  assert.equal(reactions.model().mine, null);
  assert.equal(reactions.model().coolingMs, 1500);
  assert.match(reactions.model().message, /Wait a moment/);
});

test('a reaction the host refuses for the table turns the reactions off with its reason', async () => {
  const { reactions } = mounted({ transport: { react: async () => ({ ok: false, status: 409, body: { message: 'Reactions are only sent between two players.' } }) } });
  reactions.receive(view([]));

  await reactions.send('hello');

  assert.equal(reactions.model().available, false);
  assert.equal(reactions.model().message, 'Reactions are only sent between two players.');
});

test('an answer for a seat the page has left changes nothing', async () => {
  let answer;
  const { reactions } = mounted({ transport: { react: () => new Promise(resolve => { answer = resolve; }) } });
  reactions.receive(view([]));
  const sending = reactions.send('hello');

  reactions.reset(null);
  answer({ ok: true, status: 200, body: { reaction: { sequence: 1 } } });

  assert.equal(await sending, false);
  assert.equal(reactions.model().available, false);
});

test('the browser remembers the last reaction of its latest games only', () => {
  const storage = memory();
  for (let game = 0; game < 35; game++) saveSeenReaction(storage, `t${game}:player1`, 1);

  assert.equal(seenReaction(storage, 't34:player1'), 1);
  assert.equal(seenReaction(storage, 't0:player1'), 0);
});
