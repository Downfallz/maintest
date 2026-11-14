// A match scenario's practice in the battle view (ADR 0179, docs/tabletop/scenarios-plan.md, S4): the scenario an
// address names, the practice it opens, and what the band over the field says of it. The page asks the host for the
// account's practice of that scenario first, so a reload resumes it, and starts one from the cut when there is none or
// when the player restarts the round.

const SCENARIO = /^sc-[A-Za-z0-9-]{1,60}$/;

/** The scenario an address names (`/battle?scenario=<id>`), or none. */
export function practiceOf(search) {
  const id = new URLSearchParams(search ?? '').get('scenario');
  return id && SCENARIO.test(id) ? id : null;
}

/** What the band over the field says: the practice's title, where it starts, and what it does not touch. */
export function practiceBand(label) {
  return {
    title: label?.title || 'Practice',
    detail: `From the top of round ${label?.round ?? '—'} · the recorded match is unchanged`,
    goal: label?.goal ?? '',
  };
}

/**
 * The account's practice of a scenario: the one it has when `fresh` is false and there is one, a new one from the cut
 * otherwise. Resolves to the seat the battle view opens (`{ seat, token, table, practice }`); rejects with the host's
 * words and status, 401 when nobody is signed in.
 */
export async function openPractice(id, request, { fresh = false } = {}) {
  const path = `/api/scenarios/${encodeURIComponent(id)}/practice`;
  let response = fresh ? null : await request(path);
  if (!response || response.status === 404) {
    response = await request(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}' });
  }
  const body = await response.json().catch(() => ({}));
  if (!response.ok) {
    const error = new Error(body.message || `The host answered ${response.status}.`);
    error.status = response.status;
    throw error;
  }
  return body;
}
