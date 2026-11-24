// Signing in by the authorization code flow with PKCE (sign-in methods plan, P3 and P6): Discord and Microsoft, each
// app a public client, so neither the page nor the host holds a secret. The page makes a verifier and a state, keeps
// them in this tab's sessionStorage and nowhere else, and goes to the provider's authorize page; the provider returns
// to /signin/<provider>, whose page (signin.js) checks the state, posts the code with the verifier, and goes back to
// the lobby. The host does the exchange and keeps only the provider's id for the player: no name, no email.

// What each provider is asked for: Discord the scope identify, a player who allowed the app already not asked again;
// Microsoft personal accounts only (consumers), the scope openid alone, the code returned in the query.
export const CODE_FLOWS = {
  discord: {
    label: 'Discord',
    authorize: 'https://discord.com/oauth2/authorize',
    query: { scope: 'identify', prompt: 'none' },
  },
  microsoft: {
    label: 'Microsoft',
    authorize: 'https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize',
    query: { scope: 'openid', response_mode: 'query' },
  },
};

// A name to choose, handed from the return page to the lobby's name window in the same tab.
const TICKET = 'downfall.signInTicket';
// A return later than this is a sign-in started long ago, or in another visit: it is started again.
export const KEPT_FOR_MS = 10 * 60 * 1000;

// What the tab keeps between leaving for the provider and coming back: the state, the verifier and where to go after.
// One per provider, so Discord's keeps the name it had before Microsoft joined.
const keptKey = provider => `downfall.${provider}SignIn`;

/** The provider as a sentence names it. */
export const flowLabel = provider => CODE_FLOWS[provider]?.label ?? provider;

/** The path a provider returns to on this host: the exchange repeats it, and the app lists it. */
export const returnPath = provider => `/signin/${provider}`;

/** The address a provider returns to on this host. */
export const redirectFor = (origin, provider) => `${origin}${returnPath(provider)}`;

/** The provider a return page's path names, or null for a path that is not one of theirs. */
export function providerOfReturn(pathname) {
  const provider = /^\/signin\/([a-z]+)$/.exec(pathname ?? '')?.[1];
  return provider && Object.hasOwn(CODE_FLOWS, provider) ? provider : null;
}

/** Bytes as base64url without padding, the alphabet of a PKCE verifier and challenge (RFC 7636). */
export function base64Url(bytes) {
  let text = '';
  for (const byte of bytes) text += String.fromCodePoint(byte);
  // '=' is only ever the padding at the end.
  return btoa(text).replaceAll('+', '-').replaceAll('/', '_').replaceAll('=', '');
}

/** A fresh verifier (32 random bytes, 43 characters) and its S256 challenge. */
export async function pkcePair(cryptoImpl = globalThis.crypto) {
  const verifier = base64Url(cryptoImpl.getRandomValues(new Uint8Array(32)));
  const digest = await cryptoImpl.subtle.digest('SHA-256', new TextEncoder().encode(verifier));
  return { verifier, challenge: base64Url(new Uint8Array(digest)) };
}

/** A provider's authorize page for this host: the code flow, its scope, the challenge, back to the return page. */
export function authorizeUrl(provider, { clientId, redirect, state, challenge }) {
  const flow = CODE_FLOWS[provider];
  const query = new URLSearchParams({
    response_type: 'code',
    client_id: clientId,
    redirect_uri: redirect,
    state,
    code_challenge: challenge,
    code_challenge_method: 'S256',
    ...flow.query,
  });
  return `${flow.authorize}?${query}`;
}

/**
 * Leaves for the provider: keeps the state, the verifier, `next` and the purpose in the tab, then goes. The purpose is
 * `signin`, or `link` when a signed-in player adds the provider from Settings (sign-in methods plan, P4). A tab without
 * storage cannot come back signed in, so it says so rather than leaving.
 */
export async function startCodeFlow({ provider, clientId, origin, next = null, purpose = 'signin', storage, cryptoImpl = globalThis.crypto, now = Date.now(), go }) {
  const { verifier, challenge } = await pkcePair(cryptoImpl);
  const state = base64Url(cryptoImpl.getRandomValues(new Uint8Array(16)));
  try {
    storage.setItem(keptKey(provider), JSON.stringify({ state, verifier, next, purpose, at: now }));
  } catch {
    throw new Error(`This browser keeps nothing for the page, so ${flowLabel(provider)} cannot bring you back signed in. Try another browser.`);
  }
  const url = authorizeUrl(provider, { clientId, redirect: redirectFor(origin, provider), state, challenge });
  go(url);
  return url;
}

/** What the tab kept when it left for the provider, read once: a second read finds nothing. */
export function takeKept(storage, provider) {
  try {
    const kept = JSON.parse(storage?.getItem(keptKey(provider)) ?? 'null');
    storage?.removeItem(keptKey(provider));
    return kept && typeof kept === 'object' ? kept : null;
  } catch { return null; }
}

/**
 * What a provider's return says, against what the tab kept: the code and its verifier to post, or why not. `cancelled`
 * is the player saying no at the provider; `failed` any other error it returned with; `lapsed` a return this tab did
 * not start, or started too long ago; `mismatch` a return whose state is not the one this tab sent, which is somebody
 * else's code and is never posted.
 */
export function codeReturn(search, kept, now = Date.now()) {
  const query = new URLSearchParams(search ?? '');
  const next = typeof kept?.next === 'string' ? kept.next : null;
  // Whether the tab left to add the provider from Settings (P4), so the return page speaks of adding, not signing in.
  const link = kept?.purpose === 'link';
  // The player saying no goes straight back; any other error (a misconfigured app, the provider failing) is said.
  if (query.get('error') === 'access_denied') return { problem: 'cancelled', next, link };
  if (query.get('error')) return { problem: 'failed', next, link };
  if (!kept || typeof kept.state !== 'string' || typeof kept.verifier !== 'string' || typeof kept.at !== 'number' || now - kept.at >= KEPT_FOR_MS) return { problem: 'lapsed', next: null, link };
  const code = query.get('code');
  if (!code || query.get('state') !== kept.state) return { problem: 'mismatch', next: null, link };
  return { code, verifier: kept.verifier, next, link };
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
