import { createBattleServices } from '../../table/battle-services.js';
import { createFeedback } from '../../table/feedback.js';
const el = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
export function serviceControls(controller: ReturnType<typeof createBattleServices>, storage: Storage | null) {
  const feedback = createFeedback(window, storage);
  el<HTMLInputElement>('haptics').checked = feedback.settings().haptics;
  el('haptics').addEventListener('change', () => feedback.update({ haptics: el<HTMLInputElement>('haptics').checked }));
  document.addEventListener('click', event => {
    if (!document.hidden && event.target instanceof Element && event.target.closest('button:not(:disabled)')) feedback.cue('select', { sound: false });
  });
  el('notices-toggle').addEventListener('click', () => { void controller.toggle(); });
  el('notices-test').addEventListener('click', () => { void controller.test(); });
  function render() {
    const model = controller.model();
    el('notices-tools').hidden = model.notice.hidden;
    el<HTMLButtonElement>('notices-toggle').disabled = model.notice.disabled;
    el('notices-toggle').textContent = model.notice.label;
    el('notices-hint').textContent = model.notice.hint;
    el('notices-problem').textContent = model.message;
    el('notices-test').hidden = !model.testShown;
    el<HTMLButtonElement>('notices-test').disabled = !model.canTest;
  }
  return { render };
}
