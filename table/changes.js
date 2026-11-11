// What a page reads on instead of a clock (ADR 0163): a stream the host says "read again" on, kept open while the
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
    if (!response.ok || !response.body) return { status: response.status, ok: false };
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
export function createLobbyChanges({ doc = globalThis.document, win = globalThis, fetchImpl = (...args) => globalThis.fetch(...args), schedule = setTimeout, cancel = clearTimeout } = {}) {
  const listeners = new Set();
  let watcher = null, refused = false;
  const tell = event => { for (const listener of [...listeners]) listener(event); };
  const open = () => {
    if (watcher || refused || listeners.size === 0 || doc?.hidden) return;
    watcher = keepWatching({
      open: (onEvent, signal) => streamEvents(fetchImpl, '/api/lobby/events', {}, onEvent, signal, { schedule, cancel }),
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
  };
}

let shared = null;

// Listens to the page's one lobby stream; answers the way to stop listening.
export function onLobbyChange(listener) {
  shared ??= createLobbyChanges();
  return shared.subscribe(listener);
}

// Whether an event is one the signed-in player's own lists should be read again on: the stream opening (whatever
// happened while it was closed) or their own lists changing.
export const accountMoved = event => event?.event === 'ready' || String(event?.data ?? '').startsWith('account');
