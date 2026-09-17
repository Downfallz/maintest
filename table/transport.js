// What the page uses to reach its own host. It is one factory so a test can hand the page a stub instead of a
// network: nothing else in the page knows that fetch exists.
//
// It speaks for exactly one seat. The host gives a token per seat and refuses a token that asks for the other
// one, so a transport that could change seats would only ever be able to ask for a 403.
export function httpTransport(seat, token, fetchImpl = globalThis.fetch.bind(globalThis)) {
  if (seat !== 'player1' && seat !== 'player2') throw new Error(`'${seat}' is neither player1 nor player2.`);
  if (!token) throw new Error('A transport needs the seat token the host printed.');

  async function send(method, path, body) {
    const options = { method, headers: { 'X-Seat-Token': token } };
    if (body !== undefined) {
      options.headers['Content-Type'] = 'application/json';
      options.body = JSON.stringify(body);
    }
    const response = await fetchImpl(path, options);
    const text = await response.text();
    // A refusal carries the reason the host gave, which is the difference between "late" and "illegal", and
    // the only thing the page can tell the player.
    return { status: response.status, ok: response.ok, body: text ? parse(text) : null };
  }

  return {
    // `since` is the first feed entry this page has not seen. It is the seat route's one query parameter and
    // it only trims the feed: the board and the options come whole on every poll, because they are a snapshot
    // and not a log.
    seat: (since = 0) => send('GET', `/api/seat/${seat}${Number.isInteger(since) && since > 0 ? `?since=${since}` : ''}`),
    session: () => send('GET', '/api/session'),
    catalogue: () => send('GET', '/api/catalogue'),
    decide: decision => send('POST', `/api/seat/${seat}/decision`, decision),
  };
}

// A host answers a refusal as JSON when it has a code to give and as a line of text when it does not; the page
// reads both the same way.
function parse(text) {
  try {
    return JSON.parse(text);
  } catch {
    return { message: text };
  }
}
