import { createArenaServices } from '../../table/arena-services.js';
import { TAPPED } from '../../table/notes.js';
import { createFeedback } from '../../table/feedback.js';
const el = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
export function serviceControls(controller: ReturnType<typeof createArenaServices>, storage: Storage | null) {
  const feedback = createFeedback(window, storage);
  el<HTMLInputElement>('haptics').checked = feedback.settings().haptics;
  el('haptics').addEventListener('change', () => feedback.update({ haptics: el<HTMLInputElement>('haptics').checked }));
  document.addEventListener('click', event => {
    if (!document.hidden && event.target instanceof Element && event.target.closest('button:not(:disabled)')) feedback.cue('select', { sound: false });
  });
  el('notices-toggle').addEventListener('click', () => { void controller.toggle(); });
  el('notices-test').addEventListener('click', () => { void controller.test(); });
  el('note-buttons').replaceChildren(...TAPPED.map(({ kind, label }: { kind: string; label: string }) => {
    const button = document.createElement('button'); button.type = 'button'; button.textContent = label;
    button.addEventListener('click', () => { void controller.note(kind); }); return button;
  }));
  el('comment-save').addEventListener('click', async () => {
    const box = el<HTMLTextAreaElement>('end-comment'), value = box.value;
    if (await controller.note('Comment', value) && box.value === value) box.value = '';
  });
  function render() {
    const model = controller.model();
    el('playtest-open').hidden = !model.notes;
    el('match-notes').hidden = !model.comment;
    el('playtest-notes').hidden = !model.notes;
    if (!model.notes) { el<HTMLDialogElement>('notes-dialog').close(); el<HTMLTextAreaElement>('end-comment').value = ''; }
    el('comment-form').hidden = !model.comment;
    el('noted').textContent = model.noteText;
    for (const button of el('notes-dialog').querySelectorAll<HTMLButtonElement>('#note-buttons button, #comment-save')) button.disabled = !model.canNote;
    el('notices-tools').hidden = model.notice.hidden;
    el<HTMLButtonElement>('notices-toggle').disabled = model.notice.disabled;
    el('notices-toggle').textContent = model.notice.label;
    el('notices-hint').textContent = model.notice.hint;
    el('notices-problem').textContent = model.message;
    el('notices-test').hidden = !model.testShown;
    el<HTMLButtonElement>('notices-test').disabled = !model.canTest;
  }
  el('playtest-open').addEventListener('click', () => { el<HTMLDialogElement>('settings-dialog').close(); el<HTMLDialogElement>('notes-dialog').showModal(); });
  el('match-notes').addEventListener('click', () => el<HTMLDialogElement>('notes-dialog').showModal());
  return { render };
}
