import { commentNote, tappedNote, notesAreKept, commentIsOpen, noted, NOTHING_TO_RECORD } from './notes.js';
import { keyBytes, sameKey, noticeButton, pushSupported, showOwnNotice, testReading, TAG } from './push.js';

// Notes and optional notices use the existing seat endpoints, never the decision/acknowledgement path.
// Each asynchronous continuation belongs to one selected seat and can only update that seat's UI.
export function createArenaServices({ onChange, scope = globalThis }) {
  let current = null, view = null, online = false;
  const emit = context => { if (context === current) onChange(); };
  const valid = context => context === current && online;
  /** @param {any} transport @param {any[]} held */
  function reset(transport = null, held = []) {
    current = transport ? { transport, held, noteBusy: false, noteText: '', started: false,
      push: { supported: pushSupported(scope.navigator, scope), keyed: false, permission: scope.Notification?.permission ?? 'default',
        subscribed: false, busy: false, hotseat: held.length > 1,
        ios: /iPad|iPhone|iPod/.test(scope.navigator?.userAgent ?? ''), secure: scope.isSecureContext !== false },
      registration: null, message: '', publicKey: null } : null;
    view = null; online = false; onChange();
  }
  function receive(next) { view = next; if (!next) reset(); else onChange(); }
  function setOnline(value) {
    online = value;
    const context = current;
    if (value && context && !context.started && view && !view.replay) {
      context.started = true; void prepare(context);
    }
    onChange();
  }
  async function prepare(context) {
    try {
      const key = await context.transport.pushKey();
      if (!valid(context)) return;
      context.publicKey = key.ok ? key.body?.publicKey : null;
      context.push.keyed = Boolean(context.publicKey);
      if (!context.push.keyed || !context.push.supported) return;
      await scope.navigator.serviceWorker.register('/sw.js');
      if (!valid(context)) return;
      const registration = await scope.navigator.serviceWorker.ready;
      if (!valid(context)) return;
      context.registration = registration;
      await refreshHeld(context);
      await closeNotices(context);
    } catch { if (valid(context)) context.message = 'Notifications could not be prepared. Reopen this seat to retry.'; }
    finally {
      if (!online && !context.registration) context.started = false;
      emit(context);
    }
  }
  async function refreshHeld(context) {
    const held = await context.registration.pushManager.getSubscription();
    if (!valid(context)) return;
    if (!held) { context.push.subscribed = false; return; }
    if (!sameKey(held, context.publicKey)) { await held.unsubscribe(); context.push.subscribed = false; return; }
    // The host suppresses notices when both held seats share an endpoint (ADR 0120).
    const answers = await Promise.all(context.held.map(transport => transport.push(held.toJSON())));
    if (!valid(context)) return;
    if (answers.some(answer => answer.status === 410)) { await held.unsubscribe(); context.push.subscribed = false; }
    else context.push.subscribed = answers.every(answer => answer.ok);
  }
  async function closeNotices(context) {
    const notices = await context.registration?.getNotifications?.() ?? [];
    if (valid(context)) for (const notice of notices) if (String(notice.tag).startsWith(`${TAG}:`)) notice.close();
  }
  async function stopNotices(context) {
    const held = await context.registration.pushManager.getSubscription();
    if (!valid(context)) return;
    await held?.unsubscribe();
    if (!valid(context)) return;
    await Promise.all(context.held.map(transport => transport.forgetPush()));
    context.push.subscribed = false;
  }
  async function subscribe(context) {
    const permission = await scope.Notification.requestPermission();
    if (!valid(context)) return;
    context.push.permission = permission;
    if (permission !== 'granted') return;
    const made = await context.registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: keyBytes(context.publicKey) });
    if (!valid(context)) return;
    const answers = await Promise.all(context.held.map(transport => transport.push(made.toJSON())));
    if (!valid(context)) return;
    context.push.subscribed = answers.every(answer => answer.ok);
    if (!context.push.subscribed) {
      await made.unsubscribe();
      context.message = 'The host could not save notifications. Try again.';
    }
  }
  async function toggle() {
    const context = current;
    if (!context || !valid(context) || model().notice.disabled || !context.registration) return;
    context.push.busy = true; context.message = ''; emit(context);
    try {
      if (context.push.subscribed) await stopNotices(context);
      else await subscribe(context);
    } catch { if (valid(context)) context.message = 'Notifications could not be changed. Check browser permissions and try again.'; }
    finally { context.push.busy = false; emit(context); }
  }
  async function test() {
    const context = current;
    if (!context || !valid(context) || !model().canTest) return;
    context.push.busy = true; context.message = 'Sending…'; emit(context);
    try {
      const page = await showOwnNotice(context.registration, scope);
      if (!valid(context)) return;
      const answer = await context.transport.pushTest();
      if (!valid(context)) return;
      context.message = testReading({ page, host: answer.body?.message ?? `The host answered ${answer.status}.`, hostOk: answer.ok });
      if (answer.status === 502 && /gone/.test(answer.body?.message ?? '')) {
        const held = await context.registration.pushManager.getSubscription();
        if (!valid(context)) return;
        await held?.unsubscribe(); context.push.subscribed = false;
      }
    } catch { if (valid(context)) context.message = 'Could not reach the host. Try again.'; }
    finally { context.push.busy = false; emit(context); }
  }
  async function note(kind, text = '') {
    const context = current;
    if (!context || !valid(context) || !model().canNote || (kind === 'Comment' && !commentIsOpen(view))) return false;
    const body = kind === 'Comment' ? commentNote(text) : tappedNote(kind);
    if (!body) { context.noteText = NOTHING_TO_RECORD; emit(context); return false; }
    context.noteBusy = true; context.noteText = 'Saving…'; emit(context);
    try {
      const answer = await context.transport.note(body);
      if (!valid(context)) return false;
      context.noteText = answer.ok ? noted(kind) : answer.body?.message ?? `The host answered ${answer.status}.`;
      return answer.ok;
    } catch {
      if (valid(context)) context.noteText = 'Could not save the note. Check your connection and try again.';
      return false;
    }
    finally { context.noteBusy = false; emit(context); }
  }
  function model() {
    const notes = Boolean(current && notesAreKept(view) && !view?.replay);
    const notice = noticeButton(current?.push ?? {});
    return { notes, comment: notes && commentIsOpen(view), canNote: notes && online && !current.noteBusy,
      noteText: current?.noteText ?? '', notice: { ...notice, disabled: notice.disabled || !online || !current?.registration },
      testShown: Boolean(current?.push.subscribed && notice.offered),
      canTest: Boolean(online && current?.push.subscribed && notice.offered && !current.push.busy), message: current?.message ?? '' };
  }
  scope.navigator?.serviceWorker?.addEventListener?.('message', event => {
    if (event.data?.kind !== 'subscription-changed') return;
    const context = current;
    if (context?.registration && valid(context)) void refreshHeld(context).then(() => emit(context)).catch(() => {});
  });
  return { reset, receive, setOnline, model, toggle, test, note };
}
