import { askedKey } from './slot.js';
import { SPEEDS } from './terms.js';

export const tempoQuestion = view => Boolean(view && !view.over && !view.playedByBot && !view.waiting &&
  view.waitingFor === 'Speed' && Number.isInteger(view.waitingAsked) && Number.isInteger(view.waitingCreature));

// Only this seat's offers and receipts authorize a draft. Opposing private choices never enter this model.
export function tempoRoster(view) {
  if (!tempoQuestion(view)) return { missing: [], accepted: [] };
  const allies = new Set((view.board?.allies ?? []).map(one => one.id));
  const accepted = (view.board?.speedChoices ?? []).filter(one => allies.has(one.creature));
  const given = new Set(accepted.map(one => one.creature));
  const missing = [...new Set([view.waitingCreature, ...(view.options?.speed?.missing ?? [])])]
    .filter(id => allies.has(id) && !given.has(id));
  return { missing, accepted };
}

const sameIds = (left, right) => left.length === right.length && left.every(id => right.includes(id));

// A confirmed batch is frozen locally, then follows acknowledged, refreshed host questions one at a time.
// Acceptance is not atomic: every interruption relinquishes the batch and requires another explicit Confirm.
export function createBattleTempo({ connection, onChange, visible = () => true }) {
  let seat = null, latest = null, scope = '', key = '', generation = 0, online = false, opened = false;
  let drafts = new Map(), focus = null, batch = null, pending = null, working = false, ack = null;
  let error = '', failed = false, needsRefresh = false;
  const roster = () => tempoRoster(latest);
  const available = () => tempoQuestion(latest) && online && roster().missing.length > 0;
  // A write still on the wire keeps the controls closed even after its batch was relinquished: the connection
  // drops a second write while one is in flight, which would read as a refusal of a tempo the host accepted.
  const editable = () => available() && opened && !batch && !working && !failed && !needsRefresh && visible();
  function model() {
    const { missing, accepted } = roster();
    const chosen = missing.filter(id => drafts.has(id)).length;
    return { latest, question: key, available: available(), opened, sending: Boolean(batch) || working, error,
      choice: drafts.has(focus) ? { speed: drafts.get(focus) } : null, tapped: [], accepted: false,
      canEdit: editable(), canSubmit: editable() && chosen === missing.length,
      tempo: { creatures: missing, accepted, focus, drafts: [...drafts], chosen, total: missing.length, needsRefresh } };
  }
  const emit = () => onChange(model());
  function clear() {
    generation++; drafts = new Map(); focus = null; batch = null; pending = null;
    ack = null; opened = false; error = ''; failed = false; needsRefresh = false;
  }
  function reset(nextSeat) { clear(); seat = nextSeat ?? null; latest = null; scope = ''; key = ''; online = false; emit(); }
  function interrupt(message, reconcile = false) {
    generation++; batch = null; pending = null; ack = null;
    error = message; needsRefresh = reconcile; emit();
    if (reconcile) void connection.refresh();
  }
  // Between two of this seat's Speed askings the host serves a snapshot that asks nothing: the question is
  // dropped as soon as the seat answers and the next one is set once the match runs again. While creatures of
  // the round's roster still have no receipt, that snapshot is a gap to wait through, not the end of the round.
  function gapBefore(next) {
    if (!scope || !next || next.over || next.playedByBot || next.waitingFor != null) return false;
    if (`${seat}/${next.board?.roundNumber}` !== scope) return false;
    const given = new Set((next.board?.speedChoices ?? []).map(one => one.creature));
    return roster().missing.some(id => !given.has(id));
  }
  // A batch still frozen carries on from wherever the gap left it; without one there is nothing to resume.
  function resume() { if (batch) void pump(); }
  function receive(next) {
    if (gapBefore(next)) return resume();
    const nextScope = tempoQuestion(next) ? `${seat}/${next.board?.roundNumber}` : '';
    const nextKey = askedKey(seat, next), previous = roster().missing;
    if (nextScope !== scope || !nextScope) clear();
    const changed = nextKey !== key;
    latest = next; scope = nextScope; key = nextKey;
    const { missing } = roster();
    drafts = new Map([...drafts].filter(([id]) => missing.includes(id)));
    if (!missing.includes(focus)) focus = missing.find(id => !drafts.has(id)) ?? missing[0] ?? null;
    const reconciled = needsRefresh;
    needsRefresh = false;
    if (changed) { ack = null; failed = false; }
    if (batch && (!visible() || !online)) interrupt('Sending paused. Review the remaining tempos and confirm to continue.');
    else if (batch && !pending && (changed || !sameIds(previous, missing))) interrupt('The choices changed elsewhere. Review the remaining tempos.');
    // A new asking outdates what an interruption said about the old one, except the refresh that interruption
    // asked for: its message is what explains the roster that refresh brings ("this tempo may have arrived").
    else if (!batch && opened && changed && !reconciled) error = 'The choices changed. Accepted tempos are kept; review the remaining draft.';
    emit();
    if (batch) void pump();
  }
  const engaged = () => scope !== '';
  function setOnline(value) {
    online = value;
    if (!value && batch) interrupt('Connection interrupted. Accepted tempos stay committed; review the refreshed board before continuing.', true);
    else emit();
  }
  async function shown() {
    if (!opened || !available() || !visible()) return null;
    if (ack) return ack;
    const context = generation, question = key;
    ack = (async () => {
      const answer = await connection.shown(latest.waitingAsked).catch(() => null);
      if (context !== generation || question !== key || !opened) return null;
      if (!answer?.ok) {
        failed = true; interrupt('Could not confirm the connection. Close and reopen to retry.', true); return null;
      }
      if (askedKey(seat, answer.body) !== question || !sameIds(tempoRoster(answer.body).missing, roster().missing)) {
        interrupt('The question changed. Review the refreshed tempos before confirming.', true); return null;
      }
      return answer;
    })();
    return ack;
  }
  function open() {
    if (!available() || batch || !visible()) return;
    opened = true; ack = null; failed = false; error = ''; emit(); void shown();
  }
  function close() { if (!batch) { opened = false; ack = null; generation++; emit(); } }
  function select(id) { if (editable() && roster().missing.includes(id)) { focus = id; emit(); } }
  function choose(value) {
    if (!editable() || !roster().missing.includes(focus) || !SPEEDS.includes(value?.speed)) return;
    drafts.set(focus, value.speed); error = '';
    focus = roster().missing.find(id => !drafts.has(id)) ?? focus; emit();
  }
  function reconcilePending(missing, accepted) {
    if (!pending) return true;
    if (!pending.succeeded || key === pending.key) return false;
    const receipt = accepted.find(one => one.creature === pending.creature);
    if (receipt?.speed !== pending.speed || !sameIds(missing, pending.missing.filter(id => id !== pending.creature))) {
      interrupt('The choices changed elsewhere. Review the remaining tempos.'); return false;
    }
    pending = null; return true;
  }
  async function pump() {
    if (!batch || working) return;
    if (!online || !visible() || !opened) { interrupt('Sending paused. Review the remaining tempos and confirm to continue.'); return; }
    const { missing, accepted } = roster();
    if (!reconcilePending(missing, accepted)) return;
    const speed = batch.get(latest.waitingCreature);
    if (!missing.includes(latest.waitingCreature) || !SPEEDS.includes(speed)) {
      interrupt('The available creatures changed. Review the remaining tempos.'); return;
    }
    const context = generation, question = key, creature = latest.waitingCreature, asked = latest.waitingAsked;
    working = true;
    try {
      const answer = await shown();
      if (context !== generation || !batch) return;
      if (!answer?.ok || key !== question || !online || !visible()) {
        interrupt('Sending paused. Review the refreshed tempos before continuing.', true); return;
      }
      pending = { key: question, creature, speed, missing: [...missing], succeeded: false };
      const result = await connection.submit({ kind: 'Speed', creature, speed, asked });
      if (context !== generation || !batch) return;
      if (!result?.ok) { interrupt(result?.body?.message ?? 'The host could not confirm this tempo. Review the refreshed board.', true); return; }
      pending.succeeded = true;
    } catch {
      if (context === generation) interrupt('Connection lost. This tempo may have arrived; review the refreshed board before continuing.', true);
    } finally {
      // The wire is free whatever became of the batch meanwhile; a batch confirmed since then starts here.
      working = false; emit(); if (batch) void pump();
    }
  }
  function submit() {
    if (!model().canSubmit) return Promise.resolve();
    batch = new Map(drafts); error = ''; emit(); return pump();
  }
  return { model, engaged, reset, receive, setOnline, open, close, choose, select, submit };
}
