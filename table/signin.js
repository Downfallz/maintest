// The page a provider returns a sign-in to, /signin/discord or /signin/microsoft (sign-in methods plan, P3 and P6): it
// checks the state against the tab's, posts the code with its verifier (to sign in, or to add the provider from
// Settings, P4), and goes back to the lobby, which follows `next` and opens the name window for a new player. The code
// is taken off the address at once, so neither the history nor a shared link keeps it.

import { accountRequest, recentSignIn } from './account.js';
import { codeReturn, flowLabel, keepTicket, providerOfReturn, redirectFor, returnPath, takeKept } from './codeflow.js';
import { WAYS_ROUTE, keepWaysNote } from './ways.js';

/** The lobby, carrying `next` when the sign-in was started on the way somewhere. */
export function lobbyPath(next) {
  return next ? `/lobby?next=${encodeURIComponent(next)}` : '/lobby';
}

/** What the page says when a provider could not be added from Settings, whatever the reason. */
export const notAdded = provider => `${flowLabel(provider)} was not added. Open Settings, Account, and try again.`;

/** What the page says when it cannot sign the player in, by why. */
export function returnProblem(problem, provider = 'discord') {
  if (problem === 'mismatch') return 'This sign-in was not started from this tab, so it was not used. Sign in again from the lobby.';
  if (problem === 'failed') return `${flowLabel(provider)} could not sign you in. Try again from the lobby.`;
  return 'This sign-in ran out. Sign in again from the lobby.';
}

/** The longest wait a refusal may ask for and still be waited out on this page, in seconds (#1204). */
export const RETRY_WITHIN_S = 5;

/**
 * Whether a refusal is one to wait out once with the same code: the host was too busy to ask the provider (429 for the
 * address's sign-ins at once, 503 for the provider's), so the code was never spent, and it said to come back shortly.
 * Only the host's own codes: a proxy's 503 can come after the host already spent the code.
 */
const SHORT_REFUSALS = { 429: 'Account.TooManySignIns', 503: 'Account.ProviderUnavailable' };
export const worthAnotherTry = error => Object.hasOwn(SHORT_REFUSALS, error?.status ?? '') && error.code === SHORT_REFUSALS[error.status]
  && Number.isInteger(error.retryAfter) && error.retryAfter <= RETRY_WITHIN_S;

const sleep = ms => new Promise(resolve => { setTimeout(resolve, ms); });

export async function finishCodeFlow({ win = globalThis, doc = globalThis.document, storage = win.sessionStorage, request = accountRequest, pause = sleep, random = Math.random } = {}) {
  // The page answers only at a provider's return path; anything else reads as Discord's, the first one.
  const provider = providerOfReturn(win.location.pathname) ?? 'discord';
  const label = flowLabel(provider);
  const answer = codeReturn(win.location.search, takeKept(storage, provider));
  win.history.replaceState(null, '', returnPath(provider));
  const back = lobbyPath(answer.next);
  const say = text => {
    doc.getElementById('signin-state').textContent = text;
    doc.getElementById('signin-back').href = back;
    doc.getElementById('signin-back').hidden = false;
  };
  const proof = { code: answer.code, verifier: answer.verifier, redirect: redirectFor(win.location.origin, provider) };
  // The code is off the address and the verifier out of the tab, so both are held here: a short refusal is waited out
  // once and the same proof posted again, rather than sending the player back through the provider (#1204).
  const post = async (path, body) => {
    try {
      return await request(path, body);
    } catch (error) {
      if (!worthAnotherTry(error)) throw error;
      doc.getElementById('signin-state').textContent = 'Checking your sign-in…';
      // Up to a second more, at random: the pages a room's burst refused do not all post again at the same moment.
      await pause(error.retryAfter * 1000 + Math.floor(random() * 1000));
      return request(path, body);
    }
  };
  // Added from Settings (sign-in methods plan, P4): the code adds the provider to the account signed in, and Settings
  // opens again on the lobby with a line saying so, whether it was added or not.
  doc.getElementById('signin-heading').textContent = answer.link ? `Adding ${label}` : `Signing in with ${label}`;
  if (answer.problem === 'cancelled') {
    if (answer.link) keepWaysNote(storage, `${label} was not added.`);
    win.location.replace(back);
    return 'cancelled';
  }
  if (answer.problem) { say(answer.link ? notAdded(provider) : returnProblem(answer.problem, provider)); return answer.problem; }
  if (answer.link) {
    try {
      await post(`${WAYS_ROUTE}/${provider}`, { ...proof, recent: recentSignIn(storage) ?? '' });
      keepWaysNote(storage, `${label} added. You can sign in with it now.`);
      win.location.replace(back);
      return 'linked';
    } catch (error) {
      say(error.message);
      return 'refused';
    }
  }
  try {
    const signedIn = await post(`/api/account/${provider}`, proof);
    if (signedIn.needsUsername) keepTicket(storage, signedIn.ticket);
    win.location.replace(back);
    return 'signed-in';
  } catch (error) {
    say(error.message);
    return 'refused';
  }
}

// Run on the return page itself; a test imports the module without a page.
if (globalThis.document?.getElementById('signin-state')) await finishCodeFlow();
