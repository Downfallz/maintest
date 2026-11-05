import { createArenaMatch } from '../../table/arena-match.js';

type Controller = ReturnType<typeof createArenaMatch>;
const el = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
const text = (id: string, value: string) => { if (el(id).textContent !== value) el(id).textContent = value; };
export function matchControls(controller: Controller, beforeOpen: () => void, beforeReview: () => void) {
  const result = el<HTMLDialogElement>('match-dialog'), concede = el<HTMLDialogElement>('concede-dialog');
  let notesKey = '', roundsKey = '', resultKey = '';
  el('match-open').addEventListener('click', () => { beforeOpen(); result.showModal(); });
  el('round-review').addEventListener('click', () => { beforeReview(); el<HTMLDialogElement>('round-dialog').showModal(); });
  el('match-replay').addEventListener('click', async () => { beforeOpen(); await controller.replay(); if (controller.model().active) { result.close(); el('replay-next').focus(); } });
  el('replay-previous').addEventListener('click', () => controller.move(-1));
  el('replay-next').addEventListener('click', () => controller.move(1));
  el('replay-close').addEventListener('click', () => { controller.closeReplay(); result.showModal(); });
  el('replay-round').addEventListener('change', () => controller.jump(Number(el<HTMLSelectElement>('replay-round').value)));
  el('concede-open').addEventListener('click', () => { el<HTMLDialogElement>('settings-dialog').close(); beforeOpen(); concede.showModal(); });
  el('concede-confirm').addEventListener('click', () => { void controller.concede(); });
  concede.addEventListener('cancel', event => { if (controller.model().busy) event.preventDefault(); });
  function render() {
    const model = controller.model();
    el('match-open').hidden = !model.over || model.active;
    el('round-review').hidden = !model.notes.length || model.active;
    el('concede-open').hidden = !model.canConcede;
    el<HTMLButtonElement>('concede-confirm').disabled = model.busy || !model.canConcede || el('arena').dataset.connection !== 'live';
    el<HTMLButtonElement>('concede-cancel').disabled = model.busy;
    if (!model.canConcede) concede.close();
    el<HTMLButtonElement>('match-replay').disabled = model.busy;
    if (!model.over) result.close();
    text('match-title', model.result?.title ?? 'Match complete');
    result.dataset.tone = model.result?.tone ?? 'unknown';
    text('match-replay-error', model.error);
    const resultData = JSON.stringify(model.result);
    if (resultData !== resultKey) {
      resultKey = resultData;
      el('match-stats').replaceChildren(...(model.result?.teams ?? []).map((team: { label: string; text: string }) => {
        const section = document.createElement('section'), label = document.createElement('h3'), stats = document.createElement('strong');
        label.textContent = team.label; stats.textContent = team.text; section.append(label, stats); return section;
      }));
    }
    text('match-outcome', model.outcome); text('concede-error', model.error); text('match-error', model.error);
    el('match-error').hidden = !model.error; el('replay-controls').hidden = !model.active;
    el('arena').dataset.replay = String(model.active);
    text('replay-text', model.summary); text('replay-note', model.note);
    text('replay-position', `Round ${model.round} · ${model.index + 1} of ${model.count}`);
    el<HTMLButtonElement>('replay-previous').disabled = !model.previous;
    el<HTMLButtonElement>('replay-next').disabled = !model.next;
    const rounds = JSON.stringify(model.rounds);
    if (rounds !== roundsKey) {
      roundsKey = rounds;
      el('replay-round').replaceChildren(...model.rounds.map((round: number) => { const option = document.createElement('option'); option.value = String(round); option.textContent = `Round ${round}`; return option; }));
    }
    el<HTMLSelectElement>('replay-round').value = String(model.round);
    const notes = JSON.stringify(model.notes);
    if (notes !== notesKey) {
      notesKey = notes;
      el('round-notes').replaceChildren(...model.notes.map((note: string) => { const node = document.createElement('li'); node.textContent = note; return node; }));
    }
  }
  return { render };
}
