import { askedKey } from './slot.js';
import { SPEEDS } from './terms.js';
import { isSettled, orderOf, tap } from './ties.js';

export function planningQuestion(view) {
  return Boolean(view && !view.over && !view.playedByBot && Number.isInteger(view.waitingAsked) &&
    ['Evolution', 'Speed', 'TieOrder'].includes(view.waitingFor));
}

// The catalogue explains mutations; only the current seat's offers authorize a purchase.
export function planningDecision(view, choice, tapped = []) {
  if (!planningQuestion(view)) return null;
  if (view.waitingFor === 'Evolution') {
    if (choice?.pass === true) return { kind: 'Evolution', pass: true };
    const offer = view.options?.evolution?.creatures?.find(one => one.creature === choice?.creature);
    return offer?.availableTiers?.includes(choice?.tier) ? { kind: 'Evolution', creature: choice.creature, tier: choice.tier } : null;
  }
  if (view.waitingFor === 'Speed') return SPEEDS.includes(choice?.speed)
    ? { kind: 'Speed', creature: view.waitingCreature, speed: choice.speed } : null;
  const groups = view.options?.tieOrder?.ties ?? [];
  return groups.length && (choice?.keep || isSettled(groups, tapped))
    ? { kind: 'TieOrder', order: orderOf(groups, choice?.keep ? [] : tapped) } : null;
}

// A separate draft for planning: a poll, a late acknowledgement or a seat switch cannot turn an old
// selection into a new command. Each tempo is confirmed for the creature the host actually asks about.
export function createArenaPlanning({ connection, onChange, visible = () => true }) {
  let seat = null, latest = null, key = '', generation = 0, revision = 0, online = false;
  let opened = false, sending = false, answered = null, choice = null, tapped = [], error = '', ack = null, failed = false;
  const available = () => planningQuestion(latest) && online && answered !== key;
  const editable = () => available() && opened && !sending && !failed && visible();
  const command = () => planningDecision(latest, choice, tapped);
  const model = () => ({ latest, question: key, available: available(), opened, sending, choice, tapped: [...tapped], error,
    canEdit: editable(), canSubmit: editable() && Boolean(command()), accepted: answered === key });
  const emit = () => onChange(model());
  function clear() { revision++; opened = false; choice = null; tapped = []; ack = null; failed = false; }
  function reset(nextSeat) {
    generation++; seat = nextSeat ?? null; latest = null; key = ''; online = false; sending = false; answered = null; error = ''; clear(); emit();
  }
  function receive(next) {
    const nextKey = askedKey(seat, next);
    if (nextKey !== key || !planningQuestion(next)) { key = nextKey; clear(); }
    latest = next;
    if (choice && !command()) choice = null;
    const groups = latest?.options?.tieOrder?.ties ?? [];
    tapped = tapped.filter(id => groups.some(group => group.includes(id)));
    emit();
  }
  function setOnline(value) { online = value; emit(); }
  function shown() {
    if (!opened || !available() || !visible()) return Promise.resolve(null);
    if (ack) return ack;
    const context = generation, question = key, attempt = revision;
    ack = (async () => {
      const answer = await connection.shown(latest.waitingAsked).catch(() => null);
      if (context !== generation || question !== key || attempt !== revision || !opened) return null;
      if (!answer?.ok) { failed = true; error = 'Could not confirm the connection. Close and reopen to retry.'; emit(); return null; }
      if (askedKey(seat, answer.body) !== key) {
        clear(); error = 'The question changed. Refreshing the board…'; emit(); void connection.refresh(); return null;
      }
      return answer;
    })();
    return ack;
  }
  function open() {
    if (!available() || sending || !visible()) return;
    clear(); opened = true; error = ''; emit(); if (visible()) void shown();
  }
  function close() { if (!sending) { clear(); error = ''; emit(); } }
  function choose(value) { if (editable()) { choice = value; error = ''; emit(); } }
  function order(id) {
    if (!editable()) return;
    choice = null; tapped = tap(latest?.options?.tieOrder?.ties, tapped, id); emit();
  }
  function restart() { if (editable()) { choice = null; tapped = []; emit(); } }
  async function submit() {
    if (!editable() || !command()) return;
    const decision = command(), question = key, context = generation, asked = latest.waitingAsked;
    sending = true; error = ''; emit();
    try {
      const answer = await shown();
      if (context !== generation || question !== key || !opened || !online || !visible() || !answer?.ok) return;
      if (JSON.stringify(decision) !== JSON.stringify(command())) { error = 'The available choices changed. Review your selection.'; return; }
      const result = await connection.submit({ ...decision, asked });
      if (context !== generation) return;
      if (result?.ok) { if (question === key) { answered = key; clear(); } }
      else { clear(); error = result?.body?.message ?? 'The host could not confirm this choice. Check the refreshed board.'; }
    } catch {
      if (context === generation) { clear(); error = 'Connection lost. The choice may have arrived; check the refreshed board before trying again.'; }
    } finally { if (context === generation) { sending = false; emit(); } }
  }
  return { model, reset, receive, setOnline, open, close, choose, order, restart, submit };
}
