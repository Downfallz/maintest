// The page Discord returns to (sign-in methods plan, P3): it checks the state against the tab's, posts the code with
// its verifier (to sign in, or to add Discord from Settings, P4), and goes back to the lobby, which follows `next` and opens the name window for a new player. The code
// is taken off the address at once, so neither the history nor a shared link keeps it.

import { accountRequest } from './account.js';
import { DISCORD_RETURN, discordRedirect, discordReturn, keepTicket, takeKept } from './discord.js';
import { WAYS_ROUTE, keepWaysNote } from './ways.js';

/** The lobby, carrying `next` when the sign-in was started on the way somewhere. */
export function lobbyPath(next) {
  return next ? `/lobby?next=${encodeURIComponent(next)}` : '/lobby';
}

/** What the page says when it cannot sign the player in, by why. */
export function returnProblem(problem) {
  if (problem === 'mismatch') return 'This sign-in was not started from this tab, so it was not used. Sign in again from the lobby.';
  if (problem === 'failed') return 'Discord could not sign you in. Try again from the lobby.';
  return 'This sign-in ran out. Sign in again from the lobby.';
}

export async function finishDiscord({ win = globalThis, doc = globalThis.document, storage = win.sessionStorage, request = accountRequest } = {}) {
  const answer = discordReturn(win.location.search, takeKept(storage));
  win.history.replaceState(null, '', DISCORD_RETURN);
  const back = lobbyPath(answer.next);
  const say = text => {
    doc.getElementById('signin-state').textContent = text;
    doc.getElementById('signin-back').href = back;
    doc.getElementById('signin-back').hidden = false;
  };
  if (answer.problem === 'cancelled') { win.location.replace(back); return 'cancelled'; }
  if (answer.problem) { say(returnProblem(answer.problem)); return answer.problem; }
  // Added from Settings (sign-in methods plan, P4): the code adds Discord to the account signed in, and Settings
  // opens again on the lobby with a line saying so.
  if (answer.link) {
    doc.getElementById('signin-heading').textContent = 'Adding Discord';
    try {
      await request(`${WAYS_ROUTE}/discord`, { code: answer.code, verifier: answer.verifier, redirect: discordRedirect(win.location.origin) });
      keepWaysNote(storage, 'Discord added. You can sign in with it now.');
      win.location.replace(back);
      return 'linked';
    } catch (error) {
      say(error.message);
      return 'refused';
    }
  }
  try {
    const signedIn = await request(`/api/account/discord`, { code: answer.code, verifier: answer.verifier, redirect: discordRedirect(win.location.origin) });
    if (signedIn.needsUsername) keepTicket(storage, signedIn.ticket);
    win.location.replace(back);
    return 'signed-in';
  } catch (error) {
    say(error.message);
    return 'refused';
  }
}

// Run on the return page itself; a test imports the module without a page.
if (globalThis.document?.getElementById('signin-state')) await finishDiscord();
