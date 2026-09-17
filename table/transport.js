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

  function parse(text) {
    try {
      return JSON.parse(text);
    } catch {
      return { message: text };
    }
  }

  return {
    seat: () => send('GET', `/api/seat/${seat}`),
    session: () => send('GET', '/api/session'),
    decide: decision => send('POST', `/api/seat/${seat}/decision`, decision),
  };
}

// Every seat the link carries a token for, in board order. Hotseat is two people at one browser, so the page
// holds both tokens and follows whichever seat the match asks; a link with one names one seat and the page
// plays that one alone, on its own device. A page opened with neither has nothing to ask for.
export function seatsFromLocation(search) {
  const query = new URLSearchParams(search);
  return ['player1', 'player2']
    .map(seat => ({ seat, token: query.get(seat) }))
    .filter(held => held.token);
}
