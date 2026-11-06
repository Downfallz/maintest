import { savedSeats, seatsFromLocation } from './session.js';

// Read saved capabilities without replacing /play's active pair. An invitation is never claimed here.
export async function battleSeats(search, storage, fetchImpl = globalThis.fetch.bind(globalThis)) {
  const query = new URLSearchParams(search);
  const linked = seatsFromLocation(search);
  const tokens = [...new Set([...savedSeats(storage), ...linked].map(one => one.token))].slice(-128);
  if (!tokens.length) return { choices: [], invitations: [], held: [] };
  async function post(path, body) {
    const response = await fetchImpl(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
    if (!response.ok) { const error = new Error('Could not verify saved seats'); error.status = response.status; throw error; }
    return response.json();
  }
  const answer = await post('/api/lobby/resume', { tokens });
  const choices = (answer.tables ?? []).flatMap(({ table, seats }) => seats.map(one => ({ ...one, table })));
  const matching = choices.filter(one => (!query.get('table') || one.table.id === query.get('table'))
    && (!linked.length || linked.some(link => link.seat === one.seat && link.token === one.token)));
  const invitations = linked.length ? (await post('/api/lobby/invitations', { tokens: linked.map(one => one.token) })).invitations ?? [] : [];
  return { choices: matching, held: choices, invitations: invitations.filter(one => one.kind === 'invitation') };
}

export function seatPath(path, choice, storage) {
  // A table identifier selects independently of another tab changing the active pair. If this capability
  // has never been saved, retain it in the link so a storage-disabled browser can still return to play.
  const query = new URLSearchParams({ table: choice.table.id, seat: choice.seat });
  if (!savedSeats(storage).some(one => one.token === choice.token)) query.set(choice.seat, choice.token);
  return `${path}?${query}`;
}
