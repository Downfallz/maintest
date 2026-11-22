// A signed-in player's pop-ups (notifications plan, N3, ADR 0202). The bell keeps every notice whatever is chosen
// here: this is about pop-ups only. One switch a device: the browser's one push subscription, registered with the
// account as a device and, by the seat pages, with each seat it plays (push.js, ADR 0118); turning it off here lets
// go of the subscription itself, so no page of this browser is told anything until it is turned on again. Three
// choices an account: what pops up on every one of its devices.
//
// The pure part is here: what the switch says and what the choices send. The page does the asking.

import { accountRequest } from './account.js';
import { keyBytes, noticeButton, pushSupported, sameKey } from './push.js';

// Where a browser kept, before ADR 0202, that it was turned off for an account while it held its subscription.
const OptOutKey = 'downfall.table.device-opt-out';

/**
 * Whether this browser was turned off for the account under the old switch, which kept the subscription: the page
 * then lets go of the subscription as the new switch does, and forgets the note only once that is done, so the choice holds.
 */
export function oldOptOut(storage, accountId) {
  try {
    return Boolean(accountId) && storage?.getItem(OptOutKey) === accountId;
  } catch {
    return false;
  }
}

/** The old note forgotten, once the subscription it stood for is let go of. */
export function forgetOldOptOut(storage) {
  try {
    storage?.removeItem(OptOutKey);
  } catch {
    // A browser that keeps nothing reads no note either.
  }
}

/** The kinds the account chooses, in the order Settings shows them: the names the host reads. */
export const KINDS = ['turns', 'invitations', 'results'];

/**
 * What the device's switch shows, from where the page stands: on when this browser is a device of the account, and
 * the reason when it cannot be one, said about the account rather than a seat.
 */
export function deviceSwitch(state) {
  const shown = noticeButton({ ...state, practice: false, hotseat: false });
  if (shown.hidden) return { hidden: true, disabled: true, checked: false, hint: '' };
  if (!shown.disabled || state.busy) {
    return {
      hidden: false,
      disabled: shown.disabled,
      checked: Boolean(state.subscribed),
      hint: state.subscribed ? 'On: what you choose below pops up here, even with the browser closed.' : 'Off: nothing pops up here.',
    };
  }
  return { hidden: false, disabled: true, checked: false, hint: `${shown.label}. ${shown.hint.replaceAll('to be told when it is your turn', 'to see pop-ups')}` };
}

/** The body that saves the account's choices: each kind the boxes say, a kind with no box kept on. */
export function kindsBody(checked) {
  return Object.fromEntries(KINDS.map(kind => [kind, checked?.[kind] !== false]));
}

/** Whether the host's answer to a registration says the browser's subscription is dead and a new one is needed. */
export function needsFreshSubscription(error) {
  return error?.status === 410;
}

/**
 * The panel: offered to a signed-in player on a host with push keys. The switch is this browser's: off tells the
 * host to forget this device and lets go of the subscription, which every page of the browser shares. The kinds
 * are the account's, read with its settings and saved as soon as a box changes.
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
    kinds: null,
    saving: false,
    pending: false,
    ios: /iPhone|iPad|iPod/.test(nav?.userAgent ?? '') && !win.MSStream,
    secure: win.isSecureContext !== false,
  };
  const problem = (text = '') => { if (el('device-problem')) { el('device-problem').textContent = text; el('device-problem').hidden = !text; } };
  const result = (text = '') => { if (el('device-result')) { el('device-result').textContent = text; el('device-result').hidden = !text; } };

  function render() {
    const panel = el('device-panel');
    if (!panel) return;
    const shown = deviceSwitch(state);
    panel.hidden = !state.account || shown.hidden;
    if (el('device-unavailable')) el('device-unavailable').hidden = !panel.hidden;
    if (el('device-bell')) el('device-bell').hidden = !state.account;
    const toggle = el('device-toggle');
    toggle.checked = shown.checked;
    toggle.disabled = shown.disabled;
    el('device-hint').textContent = shown.hint;
    el('device-test').hidden = !state.subscribed;
    const kinds = el('device-kinds');
    if (kinds) {
      kinds.hidden = !state.kinds;
      // Never disabled while saving: a box that holds the focus would lose it. A change during a save waits for it.
      kinds.setAttribute?.('aria-busy', String(state.saving));
      // While a save is on its way the boxes stay as the player left them: a tick made meanwhile is saved next.
      for (const kind of state.saving ? [] : KINDS) {
        const box = kinds.querySelector(`[data-kind="${kind}"]`);
        if (box) box.checked = Boolean(state.kinds?.[kind] ?? true);
      }
    }
  }

  // The account's choices, read with its settings: none on a host that keeps no settings, and the boxes stay hidden.
  async function readKinds() {
    try {
      state.kinds = (await request('/api/account/preferences')).notices ?? null;
    } catch {
      state.kinds = null;
    }
  }

  async function saveKinds() {
    const kinds = el('device-kinds');
    if (!kinds || !state.kinds) return;
    if (state.saving) { state.pending = true; return; }
    const checked = Object.fromEntries(KINDS.map(kind => [kind, kinds.querySelector(`[data-kind="${kind}"]`)?.checked !== false]));
    const before = state.kinds;
    state.saving = true;
    state.kinds = kindsBody(checked);
    problem();
    render();
    try {
      state.kinds = (await request('/api/account/preferences/notices', kindsBody(checked), undefined, 'PUT')).notices ?? state.kinds;
    } catch (error) {
      state.kinds = before;
      problem(error.message || 'Could not reach the host. Try again.');
    } finally {
      state.saving = false;
      if (state.pending) {
        state.pending = false;
        void saveKinds();
      } else {
        render();
      }
    }
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

  // What this browser already holds is told to the host on every visit: the host forgets a device whose push
  // service let it go, and a page that stays quiet would leave the account believing it can be reached here. A
  // browser the player turned off holds no subscription, so it is told nothing.
  async function sync() {
    state.subscribed = false;
    state.kinds = null;
    problem();
    result();
    if (!state.account) { render(); return; }
    try {
      const key = await request('/api/lobby/push');
      state.publicKey = key.publicKey ?? null;
      state.keyed = Boolean(state.publicKey);
      if (state.keyed) await readKinds();
      if (!state.keyed || !state.supported) { render(); return; }
      await nav.serviceWorker.register('/sw.js');
      state.registration = await nav.serviceWorker.ready;
      const subscription = await current();
      if (subscription && oldOptOut(storage, state.account.id)) {
        // A seat page may have registered it since: the host forgets it first, then the browser lets go of it.
        await request('/api/account/push', { endpoint: subscription.endpoint }, undefined, 'DELETE');
        await subscription.unsubscribe();
        forgetOldOptOut(storage);
      } else if (subscription) {
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
        if (subscription) {
          await request('/api/account/push', { endpoint: subscription.endpoint }, undefined, 'DELETE');
          await subscription.unsubscribe();
        }
        state.subscribed = false;
      } else {
        state.permission = await win.Notification.requestPermission();
        if (state.permission === 'granted') {
          const subscription = (await current()) ?? await state.registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: keyBytes(state.publicKey) });
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

  el('device-toggle')?.addEventListener('change', () => void toggle());
  el('device-kinds')?.addEventListener('change', () => void saveKinds());
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
