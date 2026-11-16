// What a page reads on instead of a clock (ADR 0164): a stream the host says "read again" on, kept open while the
// page is in front of somebody, opened again when it drops, and read on once more each time it opens. Nothing here
// asks the host what changed: each page reads its own routes when it is told to.

// How long a stream may stay silent before it is taken for dead. The host says it is still there every twenty
// seconds (ADR 0119), so two missed in a row is a connection a sleeping phone or a proxy dropped without a word.
export const SILENCE = 45000;

// Reads a server-sent stream from `path`, telling `onEvent` of each `ready` and `changed`, until the host closes it,
// `signal` aborts it, or it stays silent for `silence`. A silent stream ends as if closed (`silent: true`), so the
// caller opens it again; an abort from `signal` is thrown, as `fetch` throws it.
export async function streamEvents(fetchImpl, path, headers, onEvent, signal, { silence = SILENCE, schedule = setTimeout, cancel = clearTimeout } = {}) {
  const local = new AbortController();
  const forward = () => local.abort();
  if (signal?.aborted) local.abort();
  else signal?.addEventListener?.('abort', forward, { once: true });
  let timer = null, silent = false;
  const heard = () => { cancel(timer); timer = schedule(() => { silent = true; local.abort(); }, silence); };
  const ended = new Promise(resolve => local.signal.addEventListener('abort', () => resolve({ done: true, aborted: true }), { once: true }));
  try {
    heard();
    const response = await fetchImpl(path, { headers, signal: local.signal, credentials: 'same-origin' });
    if (!response.ok) return refusal(response);
    if (!response.body) return { status: response.status, ok: false };
    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let rest = '';
    for (;;) {
      const { value, done, aborted } = await Promise.race([reader.read(), ended]);
      if (aborted) { reader.cancel?.().catch?.(() => {}); break; }
      if (done) break;
      heard();
      const parsed = parseEvents(rest + decoder.decode(value, { stream: true }));
      rest = parsed.rest;
      for (const event of parsed.events) if (event.event === 'changed' || event.event === 'ready') onEvent(event);
    }
    if (signal?.aborted) throw new DOMException('The page let the stream go.', 'AbortError');
    return { status: response.status, ok: true, silent };
  } catch (failure) {
    if (silent && !signal?.aborted) return { status: 0, ok: true, silent: true };
    throw failure;
  } finally {
    cancel(timer);
    signal?.removeEventListener?.('abort', forward);
  }
}

// Where a page whose host has a relay (ADR 0165) hears its changes: a WebSocket to the service at the address the host
// signs for it, said as the stream says them -- `ready` once it is open, `changed` with each message -- and ending
// when the connection closes, as a stream ends. A host without a relay answers 404, a relay that refuses this
// connection (its connections are all taken, say) closes it before it opens: either way the page reads the host's own
// stream instead, and for the rest of its life, so a host at its relay's limit is not asked again at every reconnect.
// What a refused stream says: a void match's 410 carries why it ended (ADR 0176), which its page shows.
async function refusal(response) {
  const said = response.status === 410 ? await Promise.resolve(response.json?.()).catch(() => null) : null;
  return typeof said?.message === 'string' ? { status: response.status, ok: false, body: { message: said.message } } : { status: response.status, ok: false };
}

const viaStream = new Set();

export async function socketOrStream(fetchImpl, base, headers, onEvent, signal, { WebSocketImpl = globalThis.WebSocket, ...options } = {}) {
  if (typeof WebSocketImpl === 'function' && !viaStream.has(base)) {
    const answer = await fetchImpl(`${base}/negotiate`, { headers, signal, credentials: 'same-origin' });
    if ([401, 403, 410].includes(answer.status)) return refusal(answer);
    if (answer.ok) {
      const url = (await answer.json().catch(() => null))?.url;
      const heard = typeof url === 'string' ? await socket(WebSocketImpl, url, onEvent, signal) : { opened: false };
      if (heard.opened) return { status: 200, ok: true, socket: true };
    }
    viaStream.add(base);
  }
  return streamEvents(fetchImpl, `${base}/events`, headers, onEvent, signal, options);
}

// One connection to the relay, until it closes or the page lets it go (an abort, thrown as fetch throws it).
function socket(WebSocketImpl, url, onEvent, signal) {
  return new Promise((resolve, reject) => {
    let opened = false, connection;
    try { connection = new WebSocketImpl(url); } catch { resolve({ opened: false }); return; }
    const close = () => { try { connection.close(); } catch { /* already closed */ } };
    const abort = () => { reject(new DOMException('The page let the connection go.', 'AbortError')); close(); };
    if (signal?.aborted) { abort(); return; }
    signal?.addEventListener?.('abort', abort, { once: true });
    connection.onopen = () => { opened = true; onEvent({ event: 'ready', data: '' }); };
    connection.onmessage = message => onEvent({ event: 'changed', data: String(message.data ?? '') });
    connection.onclose = () => { signal?.removeEventListener?.('abort', abort); resolve({ opened }); };
    connection.onerror = () => close();
  });
}

// Server-sent events as they arrive, cut at the blank line that ends each: the complete ones, and what is left
// of the one still coming. A comment line (`: keep-alive`) is nothing; a field the page does not know is kept
// and ignored.
export function parseEvents(text) {
  const blocks = text.split('\n\n');
  const rest = blocks.pop() ?? '';
  const events = [];
  for (const block of blocks) {
    const event = { event: 'message', data: '' };
    for (const line of block.split('\n')) {
      if (line.startsWith(':') || line.trim() === '') continue;
      const colon = line.indexOf(':');
      const field = colon < 0 ? line : line.slice(0, colon);
      const value = colon < 0 ? '' : line.slice(colon + 1).replace(/^ /, '');
      if (field === 'event') event.event = value;
      else if (field === 'data') event.data = event.data ? `${event.data}\n${value}` : value;
    }
    events.push(event);
  }
  return { events, rest };
}

// Keeps one stream open: `open(onEvent, signal)` is called again after it ends, a little later each time it ends
// without having said anything, until it is stopped or the host refuses it (401, 403, 404), which no retry mends.
export function keepWatching({ open, onEvent, onRefused = () => {}, schedule = setTimeout, cancel = clearTimeout, first = 1000, most = 30000 }) {
  let stopped = false, controller = null, timer = null, pause = first;
  async function connect() {
    timer = null;
    if (stopped) return;
    const mine = new AbortController();
    controller = mine;
    let answer = null;
    try { answer = await open(event => { if (!stopped && controller === mine) { pause = first; onEvent(event); } }, mine.signal); }
    catch { /* The stream broke; it is opened again below. */ }
    if (controller === mine) controller = null;
    if (stopped || mine.signal.aborted) return;
    if (answer && [401, 403, 404].includes(answer.status)) { stopped = true; onRefused(answer); return; }
    timer = schedule(connect, pause);
    pause = Math.min(pause * 2, most);
  }
  void connect();
  return {
    stop() { stopped = true; cancel(timer); timer = null; controller?.abort(); controller = null; },
    // Waiting to open again (the network came back, the page is in view again): opens now rather than after the wait.
    now() { if (stopped || timer === null) return; cancel(timer); pause = first; void connect(); },
  };
}

// The lobby's stream (`GET /api/lobby/events`), one for the page however many parts of it listen: the lists, the
// bell, the Friends panel, the ladder. Closed while the page is hidden, so a phone in a pocket holds no connection
// and keeps no host awake, and opened again when it is in view, whose `ready` is the read on coming back.
export function createLobbyChanges({ doc = globalThis.document, win = globalThis, fetchImpl = (...args) => globalThis.fetch(...args), WebSocketImpl = globalThis.WebSocket, schedule = setTimeout, cancel = clearTimeout } = {}) {
  const listeners = new Set();
  let watcher = null, refused = false;
  const tell = event => { for (const listener of [...listeners]) listener(event); };
  const open = () => {
    if (watcher || refused || listeners.size === 0 || doc?.hidden) return;
    watcher = keepWatching({
      open: (onEvent, signal) => socketOrStream(fetchImpl, '/api/lobby', {}, onEvent, signal, { WebSocketImpl, schedule, cancel }),
      onEvent: tell, onRefused: () => { refused = true; watcher = null; }, schedule, cancel,
    });
  };
  const close = () => { watcher?.stop(); watcher = null; };
  doc?.addEventListener?.('visibilitychange', () => { if (doc.hidden) close(); else open(); });
  win?.addEventListener?.('online', () => { if (watcher) watcher.now(); else open(); });
  return {
    subscribe(listener) {
      listeners.add(listener);
      open();
      return () => { listeners.delete(listener); if (listeners.size === 0) close(); };
    },
    // The host reads who is signed in when the stream opens: a sign-in, a sign-out or another account opens it again,
    // and its `ready` reads the new player's lists.
    reopen() { close(); refused = false; open(); },
  };
}

let shared = null;

// Listens to the page's one lobby stream; answers the way to stop listening.
export function onLobbyChange(listener) {
  shared ??= createLobbyChanges();
  return shared.subscribe(listener);
}

// Who is signed in changed: the page's lobby stream, if it has one, is opened again for them.
export function signedInChanged() {
  shared?.reopen();
}

// A read that failed is tried again a second later, then two, up to thirty, until one answers: a retry, not a
// clock, since the change that asked for it will not be announced twice. `failed()` after a failure schedules
// `run`; `answered()` after a success starts the wait over; `stop()` gives up what is due.
export function backoff(run, { schedule = setTimeout, cancel = clearTimeout, first = 1000, most = 30000 } = {}) {
  let timer = null, pause = first;
  return {
    failed() { cancel(timer); timer = schedule(() => { timer = null; run(); }, pause); pause = Math.min(pause * 2, most); },
    answered() { cancel(timer); timer = null; pause = first; },
    stop() { cancel(timer); timer = null; pause = first; },
  };
}

// Whether an event is one the signed-in player's own lists should be read again on: the stream opening (whatever
// happened while it was closed) or their own lists changing.
export const accountMoved = event => event?.event === 'ready' || String(event?.data ?? '').startsWith('account');
