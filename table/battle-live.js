import { creatureAppearance } from './battle-art.js';
import { boardPresentation } from './presentation.js';

// How every surface of the battle names a creature off a seat's board (the scene, the journal, the guide):
// its name, and its id beside it only when another creature on the board shares that name. A creature the
// board does not know, or one the host left unnamed, is named by its id alone.
export function creatureLabel(board, id) {
  const roster = [...(board?.allies ?? []), ...(board?.enemies ?? [])];
  const one = roster.find(creature => creature.id === id);
  if (!one?.name) return `Creature ${id ?? '?'}`;
  return roster.filter(other => other.name === one.name).length > 1 ? `${one.name} · ${id}` : one.name;
}

// The host has already filtered this board for one seat. No sample, inferred action or replay belongs here.
// The catalogue's packages say how an acquired tier shows on a creature; without them a creature is its base.
export function livePresentation(view, packages = new Map()) {
  const board = boardPresentation(view?.board);
  const roster = [...board.allies, ...board.enemies];
  const creatures = roster.map((one, index) => ({
    ...one, label: creatureLabel(board, one.id),
    id: String(one.id), enemy: index >= board.allies.length,
    ...creatureAppearance(one, packages),
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
    connection.history = async () => {
      const answer = await transport.seat(0);
      return !current() || refused(answer) ? null : answer;
    };
    const write = async operation => {
      if (connection.writing) return null;
      connection.writing = true;
      connection.revision++;
      cancel(connection.pollTimer);
      try {
        const answer = await operation();
        return !current() || refused(answer) ? null : answer;
      } finally {
        connection.writing = false;
        connection.revision++;
        if (current()) void refresh();
      }
    };
    connection.submit = decision => write(() => transport.decide(decision));
    connection.concede = () => write(() => transport.concede());
    onView(null, null);
    onState('connecting');
    void refresh();
    if (transport.events) void stream();
  }
  return { start, stop, refresh: () => active?.refresh(),
    shown: asked => active?.shown(asked) ?? Promise.resolve(null),
    history: () => active?.history() ?? Promise.resolve(null),
    concede: () => active?.concede() ?? Promise.resolve(null),
    submit: decision => active?.submit(decision) ?? Promise.resolve(null) };
}
