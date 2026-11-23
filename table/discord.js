// Signing in with Discord (sign-in methods plan, P3): the authorization code flow with PKCE, the app a public client,
// so neither the page nor the host holds a secret. The page makes a verifier and a state, keeps them in this tab's
// sessionStorage and nowhere else, and goes to Discord's authorize page; Discord returns to /signin/discord, whose
// page (signin.js) checks the state, posts the code with the verifier, and goes back to the lobby. The host does the
// exchange and keeps only the Discord user's id (scope identify: no email).

export const DISCORD_AUTHORIZE = 'https://discord.com/oauth2/authorize';
export const DISCORD_RETURN = '/signin/discord';
// What the tab keeps between leaving for Discord and coming back: the state, the verifier and where to go after.
const KEPT = 'downfall.discordSignIn';
// A name to choose, handed from the return page to the lobby's name window in the same tab.
const TICKET = 'downfall.signInTicket';
// A return later than this is a sign-in started long ago, or in another visit: it is started again.
export const KEPT_FOR_MS = 10 * 60 * 1000;

/** Bytes as base64url without padding, the alphabet of a PKCE verifier and challenge (RFC 7636). */
export function base64Url(bytes) {
  let text = '';
  for (const byte of bytes) text += String.fromCharCode(byte);
  return btoa(text).replaceAll('+', '-').replaceAll('/', '_').replace(/=+$/, '');
}

/** A fresh verifier (32 random bytes, 43 characters) and its S256 challenge. */
export async function pkcePair(cryptoImpl = globalThis.crypto) {
  const verifier = base64Url(cryptoImpl.getRandomValues(new Uint8Array(32)));
  const digest = await cryptoImpl.subtle.digest('SHA-256', new TextEncoder().encode(verifier));
  return { verifier, challenge: base64Url(new Uint8Array(digest)) };
}

/** Discord's authorize page for this host: the code flow, scope identify, the challenge, back to the return page. */
export function discordAuthorizeUrl({ clientId, redirect, state, challenge }) {
  const query = new URLSearchParams({
    response_type: 'code',
    client_id: clientId,
    scope: 'identify',
    redirect_uri: redirect,
    state,
    code_challenge: challenge,
    code_challenge_method: 'S256',
    // A player who has allowed the app already is not asked again.
    prompt: 'none',
  });
  return `${DISCORD_AUTHORIZE}?${query}`;
}

/** The address Discord returns to on this host: the exchange repeats it, and the app lists it. */
export const discordRedirect = origin => `${origin}${DISCORD_RETURN}`;

/**
 * Leaves for Discord: keeps the state, the verifier and `next` in the tab, then goes. A tab without storage cannot
 * come back signed in, so it says so rather than leaving.
 */
export async function startDiscord({ clientId, origin, next = null, storage, cryptoImpl = globalThis.crypto, now = Date.now(), go }) {
  const { verifier, challenge } = await pkcePair(cryptoImpl);
  const state = base64Url(cryptoImpl.getRandomValues(new Uint8Array(16)));
  try {
    storage.setItem(KEPT, JSON.stringify({ state, verifier, next, at: now }));
  } catch {
    throw new Error('This browser keeps nothing for the page, so Discord cannot bring you back signed in. Try another browser.');
  }
  const url = discordAuthorizeUrl({ clientId, redirect: discordRedirect(origin), state, challenge });
  go(url);
  return url;
}

/** What the tab kept when it left for Discord, read once: a second read finds nothing. */
export function takeKept(storage) {
  try {
    const kept = JSON.parse(storage?.getItem(KEPT) ?? 'null');
    storage?.removeItem(KEPT);
    return kept && typeof kept === 'object' ? kept : null;
  } catch { return null; }
}

/**
 * What Discord's return says, against what the tab kept: the code and its verifier to post, or why not. `cancelled`
 * is the player saying no at Discord; `lapsed` a return this tab did not start, or started too long ago; `mismatch` a
 * return whose state is not the one this tab sent, which is somebody else's code and is never posted.
 */
export function discordReturn(search, kept, now = Date.now()) {
  const query = new URLSearchParams(search ?? '');
  const next = typeof kept?.next === 'string' ? kept.next : null;
  if (query.get('error')) return { problem: 'cancelled', next };
  if (!kept || typeof kept.state !== 'string' || typeof kept.verifier !== 'string' || !(now - kept.at < KEPT_FOR_MS)) return { problem: 'lapsed', next: null };
  const code = query.get('code');
  if (!code || query.get('state') !== kept.state) return { problem: 'mismatch', next: null };
  return { code, verifier: kept.verifier, next };
}

/** Hands a name to choose to the lobby, in this tab only. */
export function keepTicket(storage, ticket) {
  try { storage?.setItem(TICKET, ticket); } catch { /* The lobby then asks the player to sign in again. */ }
}

/** The name to choose a return page handed over, read once. */
export function takeTicket(storage) {
  try {
    const ticket = storage?.getItem(TICKET) ?? null;
    storage?.removeItem(TICKET);
    return typeof ticket === 'string' && ticket ? ticket : null;
  } catch { return null; }
}
