// The match's windows as one family (UX12, docs/art/journeys/windows.md). Each window's own module opens it, closes
// it and gives the focus back to what opened it. Two things hold for all of them, and are kept here once:
// - While a window is modal, Tab and Shift+Tab go round its own controls. The browser makes the field inert, but
//   lets the focus leave the page past the window's last control (WCAG 2.4.3, 2.1.2).
// - When a window closes and nothing took the focus (its opener closed with it, as Help's History button does, or
//   left the frame), the focus goes to the control on the frame that reaches that window again.

const FOCUSABLE = 'button, [href], input, select, textarea, summary, [tabindex]:not([tabindex="-1"])';

function focusables(dialog: HTMLDialogElement) {
  return Array.from(dialog.querySelectorAll<HTMLElement>(FOCUSABLE)).filter(one =>
    !one.matches(':disabled') && !one.closest('[inert], [hidden]') && one.getClientRects().length > 0);
}

const usable = (one: Element | null): one is HTMLElement => one instanceof HTMLElement && one.isConnected
  && !one.matches(':disabled') && !one.closest('[inert], [hidden], dialog:not([open])') && one.getClientRects().length > 0;

/** The open modal window on top, if any: the last one opened is the last in the top layer. */
function topModal() {
  return Array.from(document.querySelectorAll<HTMLDialogElement>('dialog:modal')).at(-1) ?? null;
}

/**
 * Holds every window of the page to the family's contract. `frame` names, for a window, the controls of the frame
 * that reach it, in the order tried when the focus has nowhere to go back to.
 */
export function windowFamily(frame: Record<string, string[]>) {
  document.addEventListener('keydown', event => {
    if (event.key !== 'Tab' || event.defaultPrevented || event.altKey || event.ctrlKey || event.metaKey) return;
    const dialog = topModal();
    if (!dialog) return;
    const items = focusables(dialog), active = document.activeElement;
    if (!items.length) { event.preventDefault(); return; }
    const first = items[0], last = items[items.length - 1];
    const wrap = (to: HTMLElement) => { event.preventDefault(); to.focus(); };
    if (!dialog.contains(active)) wrap(event.shiftKey ? last : first);
    else if (event.shiftKey && active === first) wrap(last);
    else if (!event.shiftKey && active === last) wrap(first);
  });
  for (const dialog of Array.from(document.querySelectorAll<HTMLDialogElement>('dialog'))) {
    // Registered after each window's own module, so this runs once that module gave the focus back, or did not.
    dialog.addEventListener('close', () => {
      const active = document.activeElement;
      if (usable(active) && !dialog.contains(active)) return;
      // Another window opened as this one closed (Settings hands over to Concede): it holds the focus.
      const above = topModal();
      if (above && above !== dialog) { if (!above.contains(active)) focusables(above)[0]?.focus({ preventScroll: true }); return; }
      const to = (frame[dialog.id] ?? []).map(selector => document.querySelector(selector)).find(usable);
      to?.focus({ preventScroll: true });
    });
  }
}
