// Whether the step a journey answered is behind the seat, read from the seat's latest view.
// A team's tempos are one asking per creature, which the page sends one after another from a single sheet
// (table/battle-tempo.js), and between two of them the host serves a view that asks nothing. So a tempo step
// ends only once the round has left its tempos; any other step ends once the seat is asked something else (#787).
export function stepAnswered(view, latest) {
  if (!latest) return false;
  if (latest.over) return true;
  if (view.waitingFor !== 'Speed') return latest.waitingAsked !== view.waitingAsked;
  return latest.board.roundNumber !== view.board.roundNumber || (latest.waitingFor != null && latest.waitingFor !== 'Speed');
}

// What shows the game moved on the seat's view: a new feed entry, a new phase or asking, the end.
const progress = (latest) => latest && JSON.stringify({ round: latest.board?.roundNumber, subPhase: latest.board?.subPhase,
  waitingFor: latest.waitingFor ?? null, waitingAsked: latest.waitingAsked ?? null, feed: latest.feed?.at(-1)?.sequence ?? null, over: Boolean(latest.over) });

// Waits until `read()`, the seat's latest view, shows the step answered. A loaded runner serves the host's next views
// late but steadily, which outlasted one 10 s window set at the call (#858). So the window restarts at each view that
// shows the game moved, as `acknowledge` restarts at each press. A seat that shows nothing new for `timeout` is stuck.
// A host that keeps moving without answering the step stops at three times `timeout`. The error says what the seat showed.
export async function waitAnswered(view, read, { timeout = 10000, interval = 100, now = Date.now, sleep = (ms) => new Promise((done) => setTimeout(done, ms)) } = {}) {
  const cap = now() + 3 * timeout;
  let end = now() + timeout, last = progress(read()), moves = 0;
  for (;;) {
    const latest = read();
    if (stepAnswered(view, latest)) return;
    const seen = progress(latest);
    if (seen !== last) { last = seen; moves++; end = Math.min(cap, now() + timeout); }
    if (now() >= end) {
      const why = now() >= cap ? `within ${3 * timeout / 1000} s, after ${moves} moves, though the seat kept moving` : `the seat showed nothing new for ${timeout / 1000} s after ${moves} moves`;
      throw new Error(`The ${view.waitingFor} step (asking ${view.waitingAsked}) was not answered: ${why}. The seat showed ${seen ?? 'no view'}.`);
    }
    await sleep(interval);
  }
}
