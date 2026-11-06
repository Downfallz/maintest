import { askedKey } from './slot.js';
import { isSettled, orderOf, tap } from './ties.js';
import { createBattleTempo } from './battle-tempo.js';

// The askings answered one at a time. Speed is not one of them: the round's tempos are drafted together and
// sent by battle-tempo.js, so no Speed command is ever built here.
export function planningQuestion(view) {
  return Boolean(view && !view.over && !view.playedByBot && Number.isInteger(view.waitingAsked) &&
    ['Evolution', 'TieOrder'].includes(view.waitingFor));
}

// The catalogue explains mutations; only the current seat's offers authorize a purchase.
export function planningDecision(view, choice, tapped = []) {
  if (!planningQuestion(view)) return null;
  if (view.waitingFor === 'Evolution') {
    if (choice?.pass === true) return { kind: 'Evolution', pass: true };
    const offer = view.options?.evolution?.creatures?.find(one => one.creature === choice?.creature);
    return offer?.availableTiers?.includes(choice?.tier) ? { kind: 'Evolution', creature: choice.creature, tier: choice.tier } : null;
  }
  const groups = view.options?.tieOrder?.ties ?? [];
  return groups.length && (choice?.keep || isSettled(groups, tapped))
    ? { kind: 'TieOrder', order: orderOf(groups, choice?.keep ? [] : tapped) } : null;
}

// Mutation and tie drafts remain scoped to one asking. Tempo owns its separate, round-scoped batch.
function createSinglePlanning({ connection, onChange, visible = () => true }) {
  let seat = null, latest = null, key = '', generation = 0, revision = 0, online = false;
  let opened = false, sending = false, answered = null, choice = null, tapped = [], error = '', ack = null, failed = false;
  const available = () => planningQuestion(latest) && online && answered !== key;
  const editable = () => available() && opened && !sending && !failed && visible();
  const command = () => planningDecision(latest, choice, tapped);
  const model = () => ({ latest, question: key, available: available(), opened, sending, choice, tapped: [...tapped], error, tempo: null,
    canEdit: editable(), canSubmit: editable() && Boolean(command()), accepted: answered === key });
  const emit = () => onChange(model());
  function clear() { revision++; opened = false; choice = null; tapped = []; ack = null; failed = false; }
  function reset(nextSeat) {
    generation++; seat = nextSeat ?? null; latest = null; key = ''; online = false; sending = false; answered = null; error = ''; clear(); emit();
  }
  function receive(next) {
    const nextKey = askedKey(seat, next);
    // A sent pick that left team picks kept the controls open: the seat's next Evolution asking continues there.
    const continues = opened && answered === key && nextKey !== key && planningQuestion(next) && next.waitingFor === 'Evolution';
    if (nextKey !== key || !planningQuestion(next)) { key = nextKey; clear(); }
    if (continues) { opened = true; error = ''; }
    latest = next;
    if (choice && !command()) choice = null;
    const groups = latest?.options?.tieOrder?.ties ?? [];
    tapped = tapped.filter(id => groups.some(group => group.includes(id)));
    emit();
    if (continues && visible()) void shown();
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
  // Keeping the dice order sets the taps aside too: the rows show the order the command will send.
  function choose(value) { if (editable()) { choice = value; if (value?.keep) tapped = []; error = ''; emit(); } }
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
      if (result?.ok) {
        if (question === key) {
          // Picks are spent one at a time: a pick with more left keeps the tree open for the next one.
          const more = decision.kind === 'Evolution' && !decision.pass && (latest.options?.evolution?.remainingPicks ?? 0) > 1;
          answered = key; clear(); opened = more;
        }
      }
      else { clear(); error = result?.body?.message ?? 'The host could not confirm this choice. Check the refreshed board.'; }
    } catch {
      if (context === generation) { clear(); error = 'Connection lost. The choice may have arrived; check the refreshed board before trying again.'; }
    } finally { if (context === generation) { sending = false; emit(); } }
  }
  return { model, reset, receive, setOnline, open, close, choose, order, restart, submit };
}

// One facade over the two controllers. The tempo controller speaks for the seat from the round's first Speed
// asking until its roster is answered, through the gaps between askings; the single controller otherwise.
export function createBattlePlanning(options) {
  let muted = false;
  const notify = () => { if (!muted) options.onChange(model()); };
  const single = createSinglePlanning({ ...options, onChange: notify });
  const tempo = createBattleTempo({ ...options, onChange: notify });
  const active = () => tempo.engaged() ? tempo : single;
  const model = () => active().model();
  // A view reaches both controllers, and the page is told once of what they made of it, not once each.
  function both(update) { muted = true; try { update(); } finally { muted = false; } notify(); }
  return { model,
    reset: seat => both(() => { single.reset(seat); tempo.reset(seat); }),
    receive: view => both(() => { single.receive(view); tempo.receive(view); }),
    setOnline: value => both(() => { single.setOnline(value); tempo.setOnline(value); }),
    open: () => active().open(), close: () => active().close(), choose: value => active().choose(value),
    submit: () => active().submit(), selectTempo: id => tempo.select(id),
    order: id => single.order(id), restart: () => single.restart(),
  };
}
