import { askedKey, combatDecision, emptyCastAllowed } from './slot.js';
import { decisionView } from './presentation.js';
import { declarableSpells } from './hand.js';

export function combatQuestion(view) {
  return Boolean(view && !view.over && !view.playedByBot && Number.isInteger(view.waitingAsked) &&
    ['Action', 'Intent', 'Target'].includes(view.waitingFor));
}

// The player's draft belongs to one asking of one selected seat. This controller never infers options from
// health, ownership or a catalogue card: every selectable spell and target comes from the host's question.
export function createArenaCombat({ connection, onChange, visible = () => true }) {
  let generation = 0, seat = null, latest = null, key = '', online = false;
  let opened = false, chosen = null, picked = [], sending = false, answered = null, error = '';
  let acknowledgement = null, draft = 0, acknowledgementFailed = false;
  const drawn = () => decisionView(seat, latest, { asked: key, chosen });
  const available = () => combatQuestion(latest) && online && key !== answered;
  const editable = () => available() && opened && !sending && !acknowledgementFailed && visible();
  const options = () => drawn()?.options?.intent?.creatures?.find(one => one.creature === latest?.waitingCreature);

  function model() {
    const view = drawn();
    const target = opened && view?.waitingFor === 'Target' ? view.options?.target : null;
    const actor = latest?.board?.allies?.find(one => one.id === latest?.waitingCreature);
    const slot = latest?.board?.timeline?.find(one => one.creature === latest?.waitingCreature);
    return { available: available(), opened, chosen, picked: [...picked], sending, error,
      waitingFor: latest?.waitingFor, actor: latest?.waitingCreature,
      reference: { energy: actor?.energy, round: latest?.board?.roundNumber, phase: latest?.board?.phase,
        tempo: slot?.speed, turn: slot?.turn, guidance: latest?.guidance ?? [] },
      question: key, spells: offeredForLatest(), known: latest?.board?.allies?.find(one => one.id === latest?.waitingCreature)?.knownSpells ?? [], short: options()?.unaffordableSpells ?? [],
      target, canEdit: editable(), canSubmit: editable() && combatDecision(view, chosen, picked) !== null,
      emptyCast: emptyCastAllowed(target?.legalTargets),
      accepted: answered === key, online };
  }
  const emit = () => onChange(model());
  // What the host answered outlives the refresh the write itself triggered: a 409 arrives with the next asking,
  // which changes the key, and the player still has to read why their action was refused. Only their own next
  // move (open, choose, back, close) takes the message down.
  function clearDraft({ keepError = false } = {}) {
    acknowledgementFailed = false; draft++; opened = false; chosen = null; picked = []; acknowledgement = null;
    if (!keepError) error = '';
  }
  function reset(nextSeat) {
    generation++; seat = nextSeat ?? null; latest = null; key = ''; online = false;
    sending = false; answered = null; clearDraft(); emit();
  }
  function receive(next) {
    const nextKey = askedKey(seat, next);
    if (nextKey !== key) { key = nextKey; clearDraft({ keepError: true }); }
    latest = next;
    // A poll may change the offered set without changing the question identity. Keep only what it still offers.
    if (chosen && !offeredForLatest().includes(chosen)) { chosen = null; picked = []; }
    const legal = drawn()?.options?.target?.legalTargets;
    picked = emptyCastAllowed(legal) ? [] : picked.filter(id => legal?.candidates.includes(id)).slice(0, legal?.maxTargets ?? 0);
    if (!combatQuestion(next) && opened) clearDraft({ keepError: true });
    emit();
  }
  function offeredForLatest() {
    if (latest?.waitingFor === 'Action') return (latest.options?.action?.spells ?? []).map(one => one.spell);
    return declarableSpells(latest?.options?.intent?.creatures?.find(one => one.creature === latest?.waitingCreature));
  }
  function setOnline(value) { online = value; emit(); }
  function open() {
    if (!available() || sending || !visible()) return;
    draft++; acknowledgement = null; acknowledgementFailed = false; opened = true; error = ''; emit();
    // onChange draws the controls first. An unseen or covered question must not start a decision clock.
    if (visible()) void shown();
  }
  function close() { if (sending) return; clearDraft(); emit(); }
  function choose(spell) {
    if (!editable() || !offeredForLatest().includes(spell)) return;
    chosen = spell; picked = []; error = ''; emit();
  }
  function back() { if (!editable() || latest.waitingFor !== 'Action') return; chosen = null; picked = []; error = ''; emit(); }
  function pick(id) {
    if (!editable()) return;
    const legal = drawn()?.options?.target?.legalTargets;
    if (emptyCastAllowed(legal) || !legal?.candidates.includes(id)) return;
    if (picked.includes(id)) picked = picked.filter(one => one !== id);
    else if (legal.maxTargets === 1) picked = [id];
    else if (picked.length < legal.maxTargets) picked = [...picked, id];
    emit();
  }
  function shown() {
    if (!opened || !available() || !visible()) return Promise.resolve(null);
    if (acknowledgement) return acknowledgement;
    const current = generation, question = key, attempt = draft;
    acknowledgement = (async () => {
      const answer = await connection.shown(latest.waitingAsked).catch(() => null);
      if (current !== generation || question !== key || attempt !== draft || !opened) return null;
      if (!answer?.ok) { acknowledgementFailed = true; error = 'Could not confirm the connection. Close and reopen the controls to retry.'; emit(); return null; }
      if (askedKey(seat, answer.body) !== key) {
        opened = false; chosen = null; picked = []; error = 'The question changed. Refreshing the board…'; emit();
        void connection.refresh(); return null;
      }
      return answer;
    })();
    return acknowledgement;
  }
  async function submit() {
    if (!editable()) return;
    const command = combatDecision(drawn(), chosen, picked);
    if (!command) return;
    const current = generation, question = key, asked = latest.waitingAsked;
    sending = true; error = ''; emit();
    try {
      const ack = await shown();
      if (current !== generation || key !== question || !opened || !online || !visible() || !ack?.ok) return;
      // An option may disappear while acknowledgement is in flight. Revalidate the exact draft before posting.
      if (JSON.stringify(command) !== JSON.stringify(combatDecision(drawn(), chosen, picked))) {
        error = 'The available choices changed. Review your selection.'; return;
      }
      const answer = await connection.submit({ ...command, asked });
      if (current !== generation || question !== key) return;
      if (answer?.ok) { answered = question; clearDraft(); }
      else {
        error = answer?.body?.message ?? 'The host could not confirm this action. Reopen the controls after the board refreshes.';
        opened = false; chosen = null; picked = []; acknowledgement = null;
      }
    } catch {
      if (current !== generation || question !== key) return;
      error = 'The connection was lost. The action may have arrived; check the refreshed board before trying again.';
      opened = false; chosen = null; picked = []; acknowledgement = null;
    } finally {
      if (current === generation) { sending = false; emit(); }
    }
  }
  return { reset, receive, setOnline, open, close, choose, back, pick, submit, model };
}
