import { arenaGuidance, helpPreferences } from '../../table/arena-guidance.js';

type Model = ReturnType<typeof arenaGuidance>;
const el = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
const text = (id: string, value: string) => { if (el(id).textContent !== value) el(id).textContent = value; };

export function guidanceControls(storage: Storage | null, beforeOpen: () => void, onVisibility: () => void) {
  const dialog = el<HTMLDialogElement>('help-dialog'), preferences = helpPreferences(storage);
  const buttons = Array.from(document.querySelectorAll<HTMLButtonElement>('[data-help-open]'));
  let model: Model | null = null, opener: HTMLElement | null = null, opening = false, key = '';
  function open(source: HTMLElement) {
    if (!model?.available || dialog.open || opening) return;
    opener = source; opening = true;
    beforeOpen();
    dialog.showModal(); preferences.markSeen(model.topic); opening = false;
    onVisibility();
  }
  buttons.forEach(button => button.addEventListener('click', () => open(button)));
  el('help-close').addEventListener('click', () => dialog.close());
  dialog.addEventListener('cancel', event => { event.preventDefault(); dialog.close(); });
  dialog.addEventListener('close', () => {
    onVisibility();
    const target = opener?.isConnected && !opener.matches(':disabled') && opener.getClientRects().length ? opener : el('help-open');
    if (model?.available && !dialog.open) target.focus({ preventScroll: true });
    opener = null;
  });
  el('help-mute').addEventListener('change', event => {
    preferences.setMuted((event.target as HTMLInputElement).checked); onVisibility();
  });

  function render(next: Model, canAuto: boolean) {
    model = next;
    buttons.forEach(button => { button.disabled = !next.available; });
    if (!next.available) {
      if (dialog.open) dialog.close();
      for (const id of ['help-title', 'help-round', 'help-context', 'help-objective', 'help-next', 'help-explanation', 'help-flow']) el(id).replaceChildren();
      opener = null; key = ''; return;
    }
    text('help-title', next.title); text('help-round', next.round); text('help-context', next.context);
    text('help-objective', next.objective); text('help-next', next.next);
    el('help-next').hidden = !next.next;
    el<HTMLInputElement>('help-mute').checked = preferences.muted();
    const nextKey = JSON.stringify([next.lines, next.flow, next.phase]);
    if (nextKey !== key) {
      el('help-explanation').replaceChildren(...next.lines.map((line: string) => {
        const p = document.createElement('p'); p.textContent = line; return p;
      }));
      el('help-flow').replaceChildren(...next.flow.map(step => {
        const item = document.createElement('li');
        const title = document.createElement('strong'); title.textContent = step.title;
        const detail = document.createElement('p'); detail.textContent = step.text;
        if (step.phase === next.phase) item.setAttribute('aria-current', 'step');
        item.append(title, detail); return item;
      }));
      key = nextKey;
    }
    if (dialog.open) preferences.markSeen(next.topic);
    // Callers only offer auto-help on the live, unobstructed board, after queued presentation has settled.
    if (!opening && !dialog.open && canAuto && preferences.shouldShow(next.topic)) open(el('help-open'));
  }
  return { render };
}
