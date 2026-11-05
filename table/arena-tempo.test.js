import test from 'node:test';
import assert from 'node:assert/strict';
import { createArenaPlanning } from './arena-planning.js';
import { tempoRoster } from './arena-tempo.js';

const ok = body => ({ ok: true, body });
const deferred = () => { let resolve; const promise = new Promise(done => { resolve = done; }); return { promise, resolve }; };
const flush = async () => { for (let i = 0; i < 8; i++) await new Promise(resolve => setImmediate(resolve)); };
const view = (ids = [1, 2, 3]) => ({ waitingFor: 'Speed', waitingAsked: 11, waitingCreature: ids[0],
  board: { roundNumber: 1, allies: ids.map(id => ({ id })), enemies: [{ id: 4 }], speedChoices: [] }, options: { speed: { missing: ids } } });
function harness(ids) {
  let current = view(ids), visible = true;
  const writes = [], acknowledgements = [];
  const receive = next => { current = structuredClone(next); control.receive(current); };
  const accept = command => {
    const missing = current.options.speed.missing.filter(id => id !== command.creature);
    receive({ ...current, waitingAsked: current.waitingAsked + 1, waitingCreature: missing[0] ?? null, waitingFor: missing.length ? 'Speed' : null,
      board: { ...current.board, speedChoices: [...current.board.speedChoices, { creature: command.creature, speed: command.speed }] }, options: { speed: { missing } } });
  };
  const connection = { shown: async asked => { acknowledgements.push(asked); return ok(current); },
    submit: async command => { writes.push(command); accept(command); return ok(null); }, refresh: async () => receive(current) };
  const control = createArenaPlanning({ connection, onChange: () => {}, visible: () => visible });
  control.reset('a/player1'); receive(current); control.setOnline(true);
  const fill = () => { for (const id of control.model().tempo.creatures) { control.selectTempo(id); control.choose({ speed: 'Standard' }); } };
  return { control, connection, writes, acknowledgements, receive, accept, fill, current: () => current, hide: () => { visible = false; } };
}
for (const ids of [[1], [1, 3], [1, 2, 3]]) test(`${ids.length} eligible creatures draft privately, edit the first, and confirm each acknowledged asking once`, async () => {
  const h = harness(ids); h.control.open(); h.fill();
  h.control.selectTempo(ids[0]); h.control.choose({ speed: 'Quick' });
  h.receive(h.current()); assert.equal(h.control.model().tempo.chosen, ids.length); assert.deepEqual(h.writes, []);
  await h.control.submit(); await flush();
  assert.deepEqual(h.writes, ids.map((creature, index) => ({ kind: 'Speed', creature, speed: index ? 'Standard' : 'Quick', asked: 11 + index })));
  assert.deepEqual(h.acknowledgements, ids.map((_, index) => 11 + index));
});
test('a missing choice disables confirmation and selecting advances to the next incomplete tab', async () => {
  const h = harness(); h.control.open(); h.control.choose({ speed: 'Quick' });
  assert.equal(h.control.model().tempo.focus, 2); assert.equal(h.control.model().canSubmit, false);
  await h.control.submit(); assert.deepEqual(h.writes, []);
  h.control.close(); h.control.open(); assert.deepEqual(h.control.model().tempo.drafts, [[1, 'Quick']]);
});
test('a frozen pending write ignores edits and duplicate confirmations, and waits for the authoritative next question', async () => {
  const h = harness(), write = deferred();
  h.connection.submit = command => { h.writes.push(command); return write.promise; };
  h.control.open(); h.fill(); const sending = h.control.submit(); await flush();
  h.control.selectTempo(1); h.control.choose({ speed: 'Quick' }); await h.control.submit();
  write.resolve(ok(null)); await sending; await flush();
  assert.equal(h.writes.length, 1); assert.equal(h.writes[0].speed, 'Standard');
  h.receive(h.current()); await flush(); assert.equal(h.writes.length, 1);
  h.connection.submit = async command => { h.writes.push(command); h.accept(command); return ok(null); };
  h.accept(h.writes[0]); await flush(); assert.equal(h.writes.length, 3);
});
test('an intermediate refusal keeps accepted choices and requires explicit continuation', async () => {
  const h = harness();
  h.connection.submit = async command => { h.writes.push(command); if (command.creature === 2) return { ok: false, body: { message: 'Refused tempo' } }; h.accept(command); return ok(null); };
  h.control.open(); h.fill(); await h.control.submit(); await flush();
  assert.equal(h.writes.length, 2); assert.equal(h.control.model().error, 'Refused tempo');
  assert.deepEqual(h.control.model().tempo.accepted, [{ creature: 1, speed: 'Standard' }]);
  h.receive(h.current()); await flush(); assert.equal(h.writes.length, 2);
  h.connection.submit = async command => { h.writes.push(command); h.accept(command); return ok(null); };
  await h.control.submit(); await flush(); assert.deepEqual(h.writes.map(one => one.creature), [1, 2, 2, 3]);
});
for (const accepted of [true, false]) test(`a lost response reconciles ${accepted ? 'an accepted' : 'an unaccepted'} choice without automatic retries`, async () => {
  const h = harness();
  h.connection.submit = async command => { h.writes.push(command); if (accepted) h.accept(command); throw new Error('lost'); };
  h.control.open(); h.fill(); await h.control.submit(); await flush();
  assert.equal(h.writes.length, 1); assert.equal(h.control.model().sending, false);
  assert.deepEqual(h.control.model().tempo.creatures, accepted ? [2, 3] : [1, 2, 3]);
  h.connection.submit = async command => { h.writes.push(command); h.accept(command); return ok(null); };
  await h.control.submit(); await flush(); assert.deepEqual(h.writes.map(one => one.creature), accepted ? [1, 2, 3] : [1, 1, 2, 3]);
});
for (const change of ['hidden', 'offline', 'seat', 'round', 'external']) test(`a delayed acknowledgement is fenced after ${change}`, async () => {
  const h = harness(), ack = deferred(); h.connection.shown = () => ack.promise;
  h.control.open(); h.fill(); const sending = h.control.submit();
  const original = structuredClone(h.current());
  if (change === 'hidden') h.hide();
  if (change === 'offline') h.control.setOnline(false);
  if (change === 'seat') h.control.reset('b/player2');
  if (change === 'round') h.receive({ ...h.current(), board: { ...h.current().board, roundNumber: 2 } });
  if (change === 'external') h.accept({ creature: 1, speed: 'Quick' });
  ack.resolve(ok(original)); await sending; await flush(); assert.deepEqual(h.writes, []);
});
test('an external answer to another creature during a write stops the frozen batch', async () => {
  const h = harness(), write = deferred(); h.connection.submit = command => { h.writes.push(command); return write.promise; };
  h.control.open(); h.fill(); const sending = h.control.submit(); await flush();
  h.accept(h.writes[0]); h.accept({ creature: 2, speed: 'Quick' }); write.resolve(ok(null)); await sending; await flush();
  assert.equal(h.writes.length, 1); assert.equal(h.control.model().sending, false); assert.deepEqual(h.control.model().tempo.creatures, [3]);
});
test('reconnection retains remaining drafts but never resumes the old batch', async () => {
  const h = harness(), write = deferred(); h.connection.submit = command => { h.writes.push(command); return write.promise; };
  h.control.open(); h.fill(); const sending = h.control.submit(); await flush();
  h.control.setOnline(false); h.accept(h.writes[0]); write.resolve(ok(null)); await sending;
  h.control.setOnline(true); h.receive(h.current()); await flush();
  assert.equal(h.writes.length, 1); assert.equal(h.control.model().canSubmit, true);
});
test('accepted, unknown and opposing creatures are excluded from the editable roster', () => {
  const current = view(); current.board.speedChoices = [{ creature: 1, speed: 'Quick' }, { creature: 4, speed: 'Standard' }];
  current.waitingCreature = 2; current.options.speed.missing = [1, 2, 3, 4, 99];
  assert.deepEqual(tempoRoster(current), { missing: [2, 3], accepted: [{ creature: 1, speed: 'Quick' }] });
});
// The host drops the question as soon as a creature's tempo is accepted and sets the next one a moment later:
// a poll served in between asks nothing, and the batch waits through it rather than taking it for the round's end.
test('a snapshot asking nothing between two askings keeps the batch, the drafts and the open dialog', async () => {
  const h = harness();
  h.connection.submit = async command => {
    h.writes.push(command);
    const missing = h.current().options.speed.missing.filter(id => id !== command.creature);
    const given = [...h.current().board.speedChoices, { creature: command.creature, speed: command.speed }];
    h.receive({ ...h.current(), waitingFor: null, waitingAsked: null, waitingCreature: null, board: { ...h.current().board, speedChoices: given }, options: {} });
    assert.equal(h.control.model().opened, true); assert.equal(h.control.model().sending, true);
    h.receive({ ...h.current(), waitingFor: missing.length ? 'Speed' : null, waitingAsked: 11 + given.length, waitingCreature: missing[0] ?? null,
      board: { ...h.current().board, speedChoices: given }, options: { speed: { missing } } });
    return ok(null);
  };
  h.control.open(); h.fill(); await h.control.submit(); await flush();
  assert.deepEqual(h.writes.map(one => one.creature), [1, 2, 3]); assert.equal(h.control.model().opened, false);
});
test('the same gap with no batch keeps the drafts, and the round ending through it clears them', () => {
  const h = harness(); h.control.open(); h.control.choose({ speed: 'Quick' }); h.control.choose({ speed: 'Standard' });
  h.receive({ ...h.current(), waitingFor: null, waitingAsked: null, waitingCreature: null, options: {} });
  assert.deepEqual(h.control.model().tempo.drafts, [[1, 'Quick'], [2, 'Standard']]); assert.equal(h.control.model().opened, true);
  const given = [1, 2, 3].map(creature => ({ creature, speed: 'Standard' }));
  h.receive({ ...h.current(), waitingFor: null, waitingAsked: null, waitingCreature: null, board: { ...h.current().board, speedChoices: given }, options: {} });
  assert.deepEqual(h.control.model().tempo, null); assert.equal(h.control.model().opened, false);
});
test('a confirmation after an interrupted write waits for that write instead of racing it on the wire', async () => {
  const h = harness(), write = deferred(); let inFlight = 0;
  h.connection.submit = command => { h.writes.push(command); inFlight++; return write.promise.finally(() => inFlight--); };
  h.control.open(); h.fill(); const sending = h.control.submit(); await flush();
  h.hide(); h.receive(h.current());
  assert.equal(h.control.model().error, 'Sending paused. Review the remaining tempos and confirm to continue.');
  assert.equal(h.control.model().sending, true); assert.equal(h.control.model().canSubmit, false);
  write.resolve(ok(null)); await sending; await flush();
  assert.equal(inFlight, 0); assert.equal(h.control.model().sending, false);
  h.connection.submit = async command => { h.writes.push(command); h.accept(command); return ok(null); };
  h.accept(h.writes[0]); assert.equal(h.control.model().canSubmit, false);
});
test('a new asking after an interruption replaces its message with the refreshed roster', async () => {
  const h = harness(), write = deferred(); h.connection.submit = command => { h.writes.push(command); return write.promise; };
  h.control.open(); h.fill(); const sending = h.control.submit(); await flush();
  h.control.setOnline(false); h.receive(h.current()); write.resolve(ok(null)); await sending; h.control.setOnline(true);
  assert.match(h.control.model().error, /^Sending paused|^Connection interrupted/);
  h.accept(h.writes[0]); await flush();
  assert.equal(h.control.model().error, 'The choices changed. Accepted tempos are kept; review the remaining draft.');
  assert.deepEqual(h.control.model().tempo.creatures, [2, 3]);
});
test('the facade tells the page once of a view, a connection state and a reset', () => {
  let changes = 0;
  const control = createArenaPlanning({ connection: {}, onChange: () => changes++ });
  control.reset('a/player1'); assert.equal(changes, 1);
  control.receive(view()); assert.equal(changes, 2);
  control.setOnline(true); assert.equal(changes, 3);
  control.receive({ ...view(), waitingFor: 'TieOrder', options: { tieOrder: { ties: [[1, 2]] } } }); assert.equal(changes, 4);
});
test('the refresh a lost response asks for keeps its message, and a later asking replaces it', async () => {
  const h = harness();
  h.connection.submit = async command => { h.writes.push(command); h.accept(command); throw new Error('lost'); };
  h.control.open(); h.fill(); await h.control.submit(); await flush();
  assert.match(h.control.model().error, /may have arrived/); assert.deepEqual(h.control.model().tempo.creatures, [2, 3]);
  h.accept({ creature: 2, speed: 'Quick' });
  assert.equal(h.control.model().error, 'The choices changed. Accepted tempos are kept; review the remaining draft.');
});
