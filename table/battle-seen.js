// The last result each seat of each game was shown, so a page opened again plays what happened while it was closed
// instead of starting from the board as it is. This browser keeps its own copy, the newest games only; the host keeps
// the seat's (ADR 0194), so another device knows it too, and the larger of the two is read.
const KEY = 'downfall.battle.seen.v1';
const KEPT = 20;

/** How often at most a page tells the host what its seat was shown: results play several a minute. */
export const SEND_EVERY_MS = 5000;

function read(storage) {
  try { const value = JSON.parse(storage?.getItem(KEY) ?? 'null'); return Array.isArray(value) ? value : []; } catch { return []; }
}

/**
 * The last sequence `game` (a table and seat) was shown: the larger of what this browser showed and what the host
 * kept (`kept`, the seat view's `seen`), or null when neither knows.
 * @param {Storage | null | undefined} storage
 * @param {string} game
 * @param {{ sequence?: unknown } | null | undefined} [kept]
 * @returns {number | null}
 */
export function seenSince(storage, game, kept = null) {
  const found = read(storage).find(one => one?.game === game);
  const marks = [found?.sequence, kept?.sequence].filter(Number.isInteger);
  return marks.length ? Math.max(...marks) : null;
}

/** Remembers what `game` was shown up to; the newest games first, the oldest let go of. */
export function saveSeen(storage, game, sequence) {
  if (!game || !Number.isInteger(sequence)) return;
  const kept = read(storage).filter(one => one && typeof one.game === 'string' && one.game !== game);
  try { storage?.setItem(KEY, JSON.stringify([{ game, sequence }, ...kept].slice(0, KEPT))); } catch { /* A full or refused store resumes from the board. */ }
}

/**
 * Tells the host what this seat was shown (ADR 0194), on the moments the page remembers it: at once, then at most
 * once every `SEND_EVERY_MS`, the last mark held for the end of the wait, and at once when the page is hidden
 * (`flush`). The wait is one-shot and only follows a mark; nothing is sent on a clock. A refused mark is held again,
 * for the next mark, the end of the next wait or the page hidden.
 * @param {{ send: (sequence: number) => unknown, now?: () => number,
 *   later?: (run: () => void, ms: number) => unknown, cancel?: (id: any) => void }} options
 */
export function createSeenSender({ send, now = () => Date.now(), later = (run, ms) => setTimeout(run, ms), cancel = id => clearTimeout(id) }) {
  let sent = null, sentAt = -Infinity, held = null, timer = null;
  function deliver(sequence) {
    const before = sent;
    sent = sequence; sentAt = now();
    const refused = () => {
      if (sent === sequence) sent = before;
      if (held === null || held < sequence) held = sequence;
    };
    let answer;
    try { answer = send(sequence); } catch { refused(); return; }
    Promise.resolve(answer).then(result => { if (!result?.ok) refused(); }, refused);
  }
  function due() {
    timer = null;
    if (held === null) return;
    const sequence = held; held = null; deliver(sequence);
  }
  return {
    shown(sequence) {
      if (!Number.isInteger(sequence) || sequence < 0) return;
      if (sequence <= (sent ?? -1) || sequence < (held ?? -1)) return;
      const waited = now() - sentAt;
      if (timer === null && waited >= SEND_EVERY_MS) { held = null; deliver(sequence); return; }
      held = sequence;
      if (timer === null) timer = later(due, SEND_EVERY_MS - waited);
    },
    flush() {
      if (timer !== null) { cancel(timer); timer = null; }
      due();
    },
  };
}
