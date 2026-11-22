// The match's windows as one family (UX12, docs/art/journeys/windows.md). Each window's own module opens it, closes
// it and gives the focus back to what opened it. Two things hold for all of them, and are kept here once:
// - While a window is modal, Tab and Shift+Tab go round its own controls. The browser makes the field inert, but
//   lets the focus leave the page past the window's last control (WCAG 2.4.3, 2.1.2).
// - When a window closes and nothing took the focus (its opener closed with it, as Help's History button does, or
//   left the frame), the focus goes to the control on the frame that reaches that window again.

const FOCUSABLE = 'button, [href], input, select, textarea, summary, [tabindex]';

/** The controls Tab stops on, in the window's order. */
function focusables(dialog: HTMLDialogElement) {
  return Array.from(dialog.querySelectorAll<HTMLElement>(FOCUSABLE)).filter(one => one.tabIndex >= 0
    && !one.matches(':disabled') && !one.closest('[inert], [hidden]') && one.checkVisibility({ visibilityProperty: true }));
}

const usable = (one: Element | null): one is HTMLElement => one instanceof HTMLElement && one.isConnected
  && !one.matches(':disabled') && !one.closest('[inert], [hidden], dialog:not([open])') && one.getClientRects().length > 0;

/**
 * Holds every window of the page to the family's contract. `frame` names, for a window, the controls of the frame
 * that reach it, in the order tried when the focus has nowhere to go back to.
 */
export function windowFamily(frame: Record<string, string[]>) {
  // The windows in the order they opened: the page's order says nothing of which is on top (the Mutation Tree opened
  // from the inspection comes earlier in the page than the inspection under it).
  const opened: HTMLDialogElement[] = [];
  const watch = new MutationObserver(records => {
    for (const { target } of records) {
      const dialog = target as HTMLDialogElement, at = opened.indexOf(dialog);
      if (at >= 0) opened.splice(at, 1);
      if (dialog.open) opened.push(dialog);
    }
  });
  const topModal = () => opened.filter(one => one.open && one.matches(':modal')).at(-1) ?? null;

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

  const wired = new WeakSet<HTMLDialogElement>();
  function wire(dialog: HTMLDialogElement) {
    if (wired.has(dialog)) return;
    wired.add(dialog);
    watch.observe(dialog, { attributes: true, attributeFilter: ['open'] });
    if (dialog.open) opened.push(dialog);
    // Added after the window's own module's, so this runs once that module gave the focus back, or did not.
    dialog.addEventListener('close', () => {
      const active = document.activeElement;
      // The page itself holding the focus is nothing having taken it.
      const nowhere = !active || active === document.body || active === document.documentElement;
      if (!nowhere && usable(active) && !dialog.contains(active)) return;
      // Another window opened as this one closed (Settings hands over to Concede), or stays under it: it holds the focus.
      const above = topModal();
      if (above && above !== dialog) { if (!above.contains(active)) focusables(above)[0]?.focus({ preventScroll: true }); return; }
      const to = (frame[dialog.id] ?? []).map(selector => document.querySelector(selector)).find(usable);
      to?.focus({ preventScroll: true });
    });
  }
  document.querySelectorAll('dialog').forEach(wire);
  // A window a script adds later (the feedback sheet) is wired at its first opening or closing: a listener added to
  // it while the event is captured here still runs at it, after its own module's.
  for (const type of ['toggle', 'close']) {
    document.addEventListener(type, event => { if (event.target instanceof HTMLDialogElement) wire(event.target); }, true);
  }
}
