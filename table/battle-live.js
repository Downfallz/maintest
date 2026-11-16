import { creatureAppearance } from './battle-art.js';
import { resolveEvolution } from './battle-evolution.js';
import { boardPresentation } from './presentation.js';
import { creatureName } from './terms.js';

// How every surface of the battle names a creature off a seat's board (the scene, the journal, the guide):
// its name, and its id beside it only when another creature on the board shares that name. A creature the
// board does not know, or one the host left unnamed, is named by its id alone.
export function creatureLabel(board, id) {
  const roster = [...(board?.allies ?? []), ...(board?.enemies ?? [])];
  const one = roster.find(creature => creature.id === id);
  if (!one?.name) return `Creature ${id ?? '?'}`;
  const name = creatureName(one.name);
  if (name === 'Creature') return `Creature ${id}`;
  return roster.filter(other => other.name === one.name).length > 1 ? `${name} · ${id}` : name;
}

// The host has already filtered this board for one seat. No sample, inferred action or replay belongs here.
// The catalogue's packages say how an acquired tier shows on a creature; without them a creature is its base.
// `history` holds the seat's purchase log, which orders what each creature on the board owns: which illustration
// each creature shows (ADR 0151).
export function livePresentation(view, packages = new Map(), history = null) {
  const board = boardPresentation(view?.board);
  const roster = [...board.allies, ...board.enemies];
  const creatures = roster.map((one, index) => ({
    ...one, label: creatureLabel(board, one.id),
    id: String(one.id), enemy: index >= board.allies.length,
    ...creatureAppearance(one, packages),
    evolution: resolveEvolution({ ...one, id: String(one.id) }, packages, history),
  }));
  return { ...board, creatures, frame: { creatures, round: board.round ?? null, stage: 'decision', target: null,
    actor: board.turn ? String(board.turn.slot.creature) : null, targets: [] } };
}

// One selected seat, one read at a time. The seat is read when the host says it changed and on nothing else
// (ADR 0164): no clock. A notification during a read owes one more read. Every continuation belongs to its
// connection, so a late response cannot repaint a different seat or a closed page.
export function createLiveConnection({ onState, onView, schedule = setTimeout, cancel = clearTimeout,
  now = () => performance.now() }) {
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
    // `drawn` keeps, per asking, when this page first put the question in front of the player: the host keeps the
    // same moment, and reads this one only when it lost its own (a deploy, a table picked up again; inventory Q3).
    const connection = { abort: new AbortController(), pollTimer: null, streamTimer: null, drawn: new Map(),
      busy: false, owed: false, over: false, since: 0, catalogue: catalogue ?? null, retry: 1000, failed: 0, streams: 0, revision: 0, writing: false };
    active = connection;
    const current = () => active === connection;
    // 401 asks for the seat's account, 403 is a seat this page no longer holds, and 410 a match that ended void,
    // because the game was updated under it (ADR 0163) or the operator voided it, saying why (ADR 0176), which is
    // handed on: each clears the board and stops for good.
    function refused(answer) {
      const states = { 401: 'signin', 403: 'revoked', 410: 'ended' };
      if (!(answer.status in states)) return false;
      stop();
      onView(null, null);
      if (answer.status === 410 && answer.body?.message) onState(states[answer.status], answer.body.message);
      else onState(states[answer.status]);
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
        connection.over = answer.body.over === true;
        // A finished match changes no more: a reconnect queued before its last board arrived is given up.
        if (connection.over) { cancel(connection.streamTimer); connection.streamTimer = null; }
        onView(answer.body, connection.catalogue);
        onState('live');
        connection.failed = 0;
      } catch {
        // A read that failed is tried again, later each time: a retry until it answers, not a clock.
        if (current() && revision === connection.revision) {
          onState('stale');
          connection.owed = false;
          connection.failed++;
          connection.pollTimer = schedule(refresh, Math.min(30000, 1000 * 2 ** (connection.failed - 1)));
        }
      } finally {
        connection.busy = false;
        if (current() && connection.owed) {
          connection.owed = false;
          connection.pollTimer = schedule(refresh, 0);
        }
      }
    }
    async function stream() {
      // Every `ready` is read on, the first one too: a change between the first read and the stream opening
      // would otherwise be missed, and no clock comes to find it. A read in flight owes one more.
      connection.streamTimer = null;
      if (!current() || connection.over) return;
      connection.streams++;
      let answer = null;
      try {
        answer = await transport.events(event => {
          if (!current()) return;
          connection.retry = 1000;
          void refresh();
        }, connection.abort.signal);
        if (!current() || (answer && refused(answer))) return;
      } catch { if (!current()) return; }
      // A finished match changes no more: its stream is not opened again. A host without the route reads once.
      if (!current() || connection.over || answer?.status === 404) return;
      connection.streamTimer = schedule(stream, connection.retry);
      connection.retry = Math.min(30000, connection.retry * 2);
    }
    // A page back in view or back online opens its stream now rather than after the wait, and its `ready` reads.
    connection.reconnect = () => {
      if (!current() || connection.over || connection.streamTimer === null) return;
      cancel(connection.streamTimer);
      connection.streamTimer = null;
      connection.retry = 1000;
      void stream();
    };
    connection.refresh = refresh;
    connection.shown = async asked => {
      if (!connection.drawn.has(asked)) connection.drawn.set(asked, now());
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
    // A decision carries how long this page had it in front of the player, when the page drew it.
    connection.submit = decision => {
      const drawn = connection.drawn.get(decision?.asked);
      connection.drawn.delete(decision?.asked);
      const timed = drawn === undefined ? decision : { ...decision, elapsedMs: Math.max(0, Math.round(now() - drawn)) };
      return write(() => transport.decide(timed));
    };
    connection.concede = () => write(() => transport.concede());
    onView(null, null);
    onState('connecting');
    void refresh();
    if (transport.events) void stream();
  }
  return { start, stop, refresh: () => active?.refresh(), reconnect: () => active?.reconnect?.(),
    shown: asked => active?.shown(asked) ?? Promise.resolve(null),
    history: () => active?.history() ?? Promise.resolve(null),
    concede: () => active?.concede() ?? Promise.resolve(null),
    submit: decision => active?.submit(decision) ?? Promise.resolve(null) };
}
