import { boardPresentation } from './presentation.js';

// The host has already filtered this board for one seat. No sample, inferred action or replay belongs here.
export function livePresentation(view) {
  const board = boardPresentation(view?.board);
  const roster = [...board.allies, ...board.enemies];
  const creatures = roster.map((one, index) => ({
    ...one, label: roster.filter(other => other.name === one.name).length > 1 ? `${one.name} · ${one.id}` : one.name,
    id: String(one.id), enemy: index >= board.allies.length,
    // Provisional artwork denotes a position, never a class or an unlocked ability.
    form: ['horns', 'wings', 'veil'][index % 3],
  }));
  return { ...board, creatures, frame: { creatures, stage: 'decision', target: null,
    actor: board.turn ? String(board.turn.slot.creature) : null, targets: [] } };
}

// One selected seat, one poll at a time. Notifications during a poll owe another poll. Every continuation
// belongs to its connection, so a late response cannot repaint a different seat or a closed page.
export function createLiveConnection({ onState, onView, schedule = setTimeout, cancel = clearTimeout, pollMs = 5000 }) {
  let active = null;
  function stop() {
    const previous = active;
    active = null;
    if (!previous) return;
    previous.abort.abort();
    cancel(previous.pollTimer);
    cancel(previous.streamTimer);
  }
  // A catalogue already read (by this page, before it was suspended) is handed back in: it cannot change while
  // the host runs, so a resumed seat reads its board and nothing else.
  function start(transport, catalogue) {
    stop();
    const connection = { abort: new AbortController(), pollTimer: null, streamTimer: null,
      busy: false, owed: false, since: 0, catalogue: catalogue ?? null, retry: 1000, streams: 0, revision: 0, writing: false };
    active = connection;
    const current = () => active === connection;
    function refused(answer) {
      if (answer.status !== 401 && answer.status !== 403) return false;
      stop();
      onView(null, null);
      onState(answer.status === 401 ? 'signin' : 'revoked');
      return true;
    }
    async function refresh() {
      if (!current()) return;
      if (connection.busy || connection.writing) { connection.owed = true; return; }
      connection.owed = false;
      cancel(connection.pollTimer);
      connection.busy = true;
      const revision = connection.revision;
      try {
        if (!connection.catalogue) {
          const catalogue = await transport.catalogue();
          if (!current() || refused(catalogue)) return;
          if (!catalogue.ok) throw new Error('Catalogue unavailable');
          connection.catalogue = catalogue.body;
        }
        // Ordinary reads never acknowledge a question. The combat controls do that only after being opened.
        const answer = await transport.seat(connection.since);
        if (!current() || refused(answer)) return;
        if (revision !== connection.revision) { connection.owed = true; return; }
        if (!answer.ok || !answer.body?.board) throw new Error('Board unavailable');
        if (Number.isInteger(answer.body.feedNext)) connection.since = Math.max(connection.since, answer.body.feedNext);
        onView(answer.body, connection.catalogue);
        onState('live');
      } catch {
        if (current() && revision === connection.revision) onState('stale');
      } finally {
        connection.busy = false;
        if (current()) {
          const owed = connection.owed;
          connection.owed = false;
          connection.pollTimer = schedule(refresh, owed ? 0 : pollMs);
        }
      }
    }
    async function stream() {
      // The stream opens with `ready`. On the first stream the poll already in flight is the board as it
      // stands, so that `ready` owes nothing; on a reconnect it says what may have changed meanwhile.
      const first = connection.streams++ === 0;
      try {
        const answer = await transport.events(event => {
          if (!current()) return;
          connection.retry = 1000;
          if (!(first && event?.event === 'ready')) void refresh();
        }, connection.abort.signal);
        if (!current() || (answer && refused(answer))) return;
      } catch { if (!current()) return; }
      // Polling keeps snapshots fresh while the notification channel reconnects.
      if (current()) {
        connection.streamTimer = schedule(stream, connection.retry);
        connection.retry = Math.min(30000, connection.retry * 2);
      }
    }
    connection.refresh = refresh;
    connection.shown = async asked => {
      const answer = await transport.seat(connection.since, asked);
      if (!current() || refused(answer)) return null;
      return answer;
    };
    connection.submit = async decision => {
      if (connection.writing) return null;
      connection.writing = true;
      connection.revision++;
      cancel(connection.pollTimer);
      try {
        const answer = await transport.decide(decision);
        return !current() || refused(answer) ? null : answer;
      } finally {
        connection.writing = false;
        connection.revision++;
        if (current()) void refresh();
      }
    };
    onView(null, null);
    onState('connecting');
    void refresh();
    void stream();
  }
  return { start, stop, refresh: () => active?.refresh(),
    shown: asked => active?.shown(asked) ?? Promise.resolve(null),
    submit: decision => active?.submit(decision) ?? Promise.resolve(null) };
}
