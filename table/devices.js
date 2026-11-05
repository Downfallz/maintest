// A signed-in player is told on this device (friends plan, stage 1): a friend's request, an invitation, the end
// of a match -- what a seat's own "your turn" (push.js, ADR 0118) cannot carry, because the player it is for
// holds no seat yet. The browser's one push subscription is registered with the account as a device; the seat
// pages keep registering the same subscription for their seats, and the host tells each endpoint once.
//
// The pure part is here: what the button says. The page does the asking.

import { accountRequest } from './account.js';
import { keyBytes, noticeButton, pushSupported, sameKey } from './push.js';

/** What the one button of the lobby's notices panel says, from where the page stands, in the account's words rather than a seat's. */
export function deviceButton(state) {
  const shown = noticeButton({ ...state, practice: false, hotseat: false });
  if (shown.hidden) return shown;
  if (!shown.disabled || state.busy) {
    return state.subscribed
      ? { ...shown, label: 'Stop notifying this device', hint: 'This device is told about your friends, your invitations and your games.' }
      : { ...shown, label: 'Notify me on this device', hint: 'Friend requests, invitations and the end of your games, even with the browser closed.' };
  }
  return { ...shown, hint: shown.hint.replace(/to be told when it is your turn/g, 'to be told about your friends and your games') };
}

/** Whether the host's answer to a registration says the browser's subscription is dead and a new one is needed. */
export function needsFreshSubscription(error) {
  return error?.status === 410;
}

const OptOutKey = 'downfall.table.device-opt-out';

/**
 * Whether this browser was told to stop notifying this account: the opt-out is the browser's, kept beside the
 * seats it saves, since the subscription itself stays for the seat pages and cannot carry it.
 */
export function optedOut(storage, accountId) {
  try {
    return Boolean(accountId) && storage?.getItem(OptOutKey) === accountId;
  } catch {
    return false;
  }
}

export function rememberOptOut(storage, accountId, out) {
  try {
    if (out) storage?.setItem(OptOutKey, accountId);
    else storage?.removeItem(OptOutKey);
  } catch {
    // A browser that keeps nothing asks again on its next visit.
  }
}

/**
 * The panel: offered to a signed-in player on a host with push keys, in a browser that takes a push. The
 * subscription is the browser's one and only; stopping tells the host to forget this device and leaves the
 * subscription to the seats that use it.
 */
export function mountDevices(doc, { request = accountRequest, nav = globalThis.navigator, win = globalThis, storage = null } = {}) {
  const el = id => doc.getElementById(id);
  const state = {
    account: null,
    supported: pushSupported(nav, win),
    keyed: false,
    publicKey: null,
    permission: win.Notification?.permission ?? 'default',
    subscribed: false,
    busy: false,
    registration: null,
    ios: /iPhone|iPad|iPod/.test(nav?.userAgent ?? '') && !win.MSStream,
    secure: win.isSecureContext !== false,
  };
  const problem = (text = '') => { if (el('device-problem')) { el('device-problem').textContent = text; el('device-problem').hidden = !text; } };
  const result = (text = '') => { if (el('device-result')) { el('device-result').textContent = text; el('device-result').hidden = !text; } };

  function render() {
    const panel = el('device-panel');
    if (!panel) return;
    const shown = deviceButton(state);
    panel.hidden = !state.account || shown.hidden;
    if (el('device-unavailable')) el('device-unavailable').hidden = !panel.hidden;
    const button = el('device-toggle');
    button.textContent = shown.label;
    button.disabled = shown.disabled;
    el('device-hint').textContent = shown.hint;
    el('device-test').hidden = !state.subscribed;
  }

  async function held() {
    return state.registration ? state.registration.pushManager.getSubscription() : null;
  }

  async function register(subscription) {
    const answer = await request('/api/account/push', subscription.toJSON(), undefined, 'PUT');
    state.subscribed = Boolean(answer.notices);
  }

  // A subscription made with another key is nobody's to send to: let go of, so the next tap makes a fresh one.
  async function current() {
    const subscription = await held();
    if (subscription && !sameKey(subscription, state.publicKey)) {
      await subscription.unsubscribe();
      return null;
    }
    return subscription;
  }

  // What this browser already holds is told to the host on every visit, unless the player stopped it here: the
  // host forgets a device whose push service let it go, and a page that stays quiet would leave the account
  // believing it can be reached here.
  async function sync() {
    state.subscribed = false;
    problem();
    result();
    if (!state.account) { render(); return; }
    try {
      const key = await request('/api/lobby/push');
      state.publicKey = key.publicKey ?? null;
      state.keyed = Boolean(state.publicKey);
      if (!state.keyed || !state.supported) { render(); return; }
      await nav.serviceWorker.register('/sw.js');
      state.registration = await nav.serviceWorker.ready;
      const subscription = await current();
      if (subscription && !optedOut(storage, state.account.id)) {
        try { await register(subscription); }
        catch (error) {
          if (!needsFreshSubscription(error)) throw error;
          await subscription.unsubscribe();
        }
      }
    } catch {
      // A worker that will not register, or a host that cannot be asked: the panel says so by its absence.
      state.keyed = false;
    }
    render();
  }

  async function toggle() {
    if (state.busy || !state.registration) return;
    state.busy = true;
    problem();
    result();
    render();
    try {
      if (state.subscribed) {
        const subscription = await held();
        if (subscription) await request('/api/account/push', { endpoint: subscription.endpoint }, undefined, 'DELETE');
        rememberOptOut(storage, state.account.id, true);
        state.subscribed = false;
      } else {
        state.permission = await win.Notification.requestPermission();
        if (state.permission === 'granted') {
          const subscription = (await current()) ?? await state.registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: keyBytes(state.publicKey) });
          rememberOptOut(storage, state.account.id, false);
          try { await register(subscription); }
          catch (error) {
            if (!needsFreshSubscription(error)) throw error;
            await subscription.unsubscribe();
            await register(await state.registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: keyBytes(state.publicKey) }));
          }
        }
      }
    } catch (error) {
      problem(error.message || 'Could not reach the host. Try again.');
    } finally {
      state.busy = false;
      render();
    }
  }

  async function test() {
    const button = el('device-test');
    if (!button || button.disabled) return;
    button.disabled = true;
    result('Sending…');
    try {
      const subscription = await held();
      const answer = await request('/api/account/push/test', { endpoint: subscription?.endpoint ?? '' });
      result(answer.message);
    } catch (error) {
      result(error.message || 'Could not reach the host. Try again.');
      if (/gone/.test(error.message ?? '')) {
        // Gone from the service, and the browser still holds it: let go of here too, or the next tap would get
        // the same dead subscription back from the browser and the host's 410 for it.
        await (await held())?.unsubscribe();
        state.subscribed = false;
        render();
      }
    } finally {
      button.disabled = false;
    }
  }

  el('device-toggle')?.addEventListener('click', () => void toggle());
  el('device-test')?.addEventListener('click', () => void test());
  nav?.serviceWorker?.addEventListener?.('message', event => { if (event.data?.kind === 'subscription-changed') void sync(); });
  render();
  return {
    /** Told by the sign-in whenever who is signed in changes. */
    setAccount(account) {
      if ((account?.id ?? null) === (state.account?.id ?? null)) return;
      state.account = account;
      void sync();
    },
  };
}
