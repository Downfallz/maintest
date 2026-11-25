import { answeredWith, keyBytes, sameKey, noticeButton, pushSupported, showOwnNotice, testForgot, testReading, TAG, UNREACHABLE } from './push.js';

/** Where the rest of the notifications is chosen, said under the match settings' switch. */
export const WHERE = 'This switch is this device\'s, for every game. Signed in, Settings, Notifications, in the menu, chooses what pops up. The bell keeps everything.';
/** Where the offer's Turn on points afterwards: this page's own switch, which Settings does not show with accounts off (#1160). */
export const MATCH_WHERE = 'in Match settings';

// Optional notices use the existing seat endpoints, never the decision/acknowledgement path.
// Each asynchronous continuation belongs to one selected seat and can only update that seat's UI.
export function createBattleServices({ onChange, scope = globalThis }) {
  let current = null, view = null, online = false;
  const emit = context => { if (context === current) onChange(); };
  const valid = context => context === current && online;
  /** @param {any} transport @param {any[]} held @param {string | null} key the table and seat it belongs to */
  function reset(transport = null, held = [], key = null) {
    current = transport ? { transport, held, key, started: false,
      push: { supported: pushSupported(scope.navigator, scope), keyed: false, permission: scope.Notification?.permission ?? 'default',
        subscribed: false, busy: false, hotseat: held.length > 1,
        ios: /iPad|iPhone|iPod/.test(scope.navigator?.userAgent ?? ''), secure: scope.isSecureContext !== false },
      registration: null, message: '', publicKey: null } : null;
    view = null; online = false; onChange();
  }
  // A practice's seat is the practice's own token, which no lobby route answers: no switch there, and no offer
  // through it (the end of a tutorial is offered through the account's switch, push-offer.js).
  function receive(next) {
    view = next;
    if (!next) {
      reset();
      return;
    }
    if (current) current.push.practice = Boolean(next.practice);
    onChange();
  }
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
    // A subscription the browser already holds is every page's (the account's device among them): this page lets go
    // only of one it made itself, when the host would not keep it.
    const held = await context.registration.pushManager.getSubscription();
    if (!valid(context)) return;
    const made = await context.registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: keyBytes(context.publicKey) });
    if (!valid(context)) return;
    const answers = await Promise.all(context.held.map(transport => transport.push(made.toJSON())));
    if (!valid(context)) return;
    context.push.subscribed = answers.every(answer => answer.ok);
    if (!context.push.subscribed) {
      if (!held) await made.unsubscribe();
      // A push service the host does not send to stays one: a retry cannot help (#1175). Said in the host's own
      // sentence, so the page and Settings never say it two ways (#1233); the page's copy only when it sends none.
      const unknown = answers.find(answer => answer.body?.error === 'Lobby.UnknownPushService');
      context.message = unknown
        ? unknown.body.message || 'This browser\'s notification service is not one this table can send to. Try another browser.'
        : 'This table could not save notifications. Try again.';
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
      context.message = testReading({ page, host: answer.body?.message ?? answeredWith(answer.status), hostOk: answer.ok });
      if (testForgot({ status: answer.status, error: answer.body?.error, message: answer.body?.message })) {
        const held = await context.registration.pushManager.getSubscription();
        if (!valid(context)) return;
        await held?.unsubscribe(); context.push.subscribed = false;
      }
    } catch { if (valid(context)) context.message = UNREACHABLE; }
    finally { context.push.busy = false; emit(context); }
  }
  function model() {
    const notice = noticeButton(current?.push ?? {});
    return { owner: current?.key ?? null, loaded: Boolean(view), notice: { ...notice, disabled: notice.disabled || !online || !current?.registration },
      testShown: Boolean(current?.push.subscribed && notice.offered),
      canTest: Boolean(online && current?.push.subscribed && notice.offered && !current.push.busy), message: current?.message ?? '',
      // One switch a device (ADR 0202): this one is the browser's, for every game, and Settings says what pops up.
      where: notice.offered ? WHERE : '' };
  }
  /**
   * Where the switch stands, as the offer of pop-ups reads it (push-offer.js): ready once the seat's worker is read,
   * or once a host with keys was heard by a browser that cannot push, which may still be given the steps to install.
   */
  function offerState() {
    const push = current?.push;
    if (!push || !online || view?.replay || push.practice) return { ready: false, where: MATCH_WHERE };
    return { ready: !push.busy && (Boolean(current.registration) || (push.keyed && !push.supported)),
      keyed: push.keyed, subscribed: push.subscribed, permission: push.permission, hotseat: push.hotseat, where: MATCH_WHERE };
  }
  /** The offer's Turn on: the match settings' switch turned on, through the seats it plays; the state it ends in. */
  async function turnOn() {
    if (current && !current.push.subscribed) await toggle();
    return offerState();
  }
  scope.navigator?.serviceWorker?.addEventListener?.('message', event => {
    if (event.data?.kind !== 'subscription-changed') return;
    const context = current;
    if (context?.registration && valid(context)) void refreshHeld(context).then(() => emit(context)).catch(() => {});
  });
  return { reset, receive, setOnline, model, toggle, test, offerState, turnOn };
}
