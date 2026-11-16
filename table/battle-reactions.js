// Reactions (ADR 0173): a fixed set of short messages two players send each other during a match, never free text.
// This is what the battle view knows of them, with no DOM: which ones may be sent and when, and which bubble shows.
// The host decides whether a table may react (two people, the match begun, not one account in both seats); a page
// holding both seats of a shared device offers none either, since the other player is beside it.

/**
 * Every reaction, in the menu's order; the ids are the host's (TableReactions.Catalogue).
 * @type {ReadonlyArray<{ readonly id: string, readonly glyph: string, readonly label: string }>}
 */
export const REACTIONS = Object.freeze([
  { id: 'hello', glyph: '👋', label: 'Hello!' },
  { id: 'good-luck', glyph: '🍀', label: 'Good luck!' },
  { id: 'well-played', glyph: '👏', label: 'Well played!' },
  { id: 'nice-move', glyph: '🎯', label: 'Nice move!' },
  { id: 'oops', glyph: '😅', label: 'Oops!' },
  { id: 'thinking', glyph: '🤔', label: 'Thinking…' },
  { id: 'thanks', glyph: '🙏', label: 'Thanks!' },
  { id: 'good-game', glyph: '🤝', label: 'Good game!' },
].map(one => Object.freeze(one)));

/** How long a bubble stays: long enough to read twice, short enough not to sit over the board. */
export const BUBBLE_MS = 4500;

const SEEN_KEY = 'downfall.arena.reactions-seen.v1';
// The games whose last shown reaction is remembered; older ones are let go.
const KEPT_GAMES = 30;

/** The reaction of that id, or none: a reaction this page does not know is not shown. */
export const reactionOf = id => REACTIONS.find(one => one.id === id) ?? null;

/** How long ago a reaction was sent, for one sent while this player was away: nothing under a minute. */
export function sentAgo(at, now) {
  const minutes = Math.floor((now - Date.parse(at)) / 60000);
  if (!Number.isFinite(minutes) || minutes < 1) return '';
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.floor(minutes / 60);
  return hours < 24 ? `${hours} h ago` : `${Math.floor(hours / 24)} d ago`;
}

function readSeen(storage) {
  try { const all = JSON.parse(storage?.getItem(SEEN_KEY) ?? '[]'); return Array.isArray(all) ? all.filter(one => typeof one?.game === 'string') : []; } catch { return []; }
}
/** The last reaction this browser showed for that game and seat, by number; 0 for none. */
export function seenReaction(storage, game) {
  return Number(readSeen(storage).find(one => one.game === game)?.sequence) || 0;
}
export function saveSeenReaction(storage, game, sequence) {
  const all = readSeen(storage);
  if ((Number(all.find(one => one.game === game)?.sequence) || 0) >= sequence) return;
  try { storage?.setItem(SEEN_KEY, JSON.stringify([{ game, sequence }, ...all.filter(one => one.game !== game)].slice(0, KEPT_GAMES))); } catch { /* Shown again once at most. */ }
}

/**
 * The reactions of the selected seat: `reset` with the seat's transport when one is chosen, `receive` each view,
 * `send` from the menu. Bubbles: `mine` for what this seat sent, `theirs` for the other seat's newest one this
 * browser has not shown -- the latest only, so a page opened after a while shows one reaction, not a burst.
 * @param {{ onChange: () => void, storage?: Storage | null, now?: () => number, schedule?: (run: () => void, ms: number) => any, cancel?: (id: any) => void }} options
 */
export function createBattleReactions({ onChange, storage = null, now = () => Date.now(), schedule = (run, ms) => setTimeout(run, ms), cancel = id => clearTimeout(id) }) {
  let context = null;
  const timers = { mine: null, theirs: null, cooling: null };
  const emit = owner => { if (owner === context) onChange(); };
  function clearTimers() { for (const key of Object.keys(timers)) { if (timers[key] !== null) cancel(timers[key]); timers[key] = null; } }

  /** @param {any} transport @param {string | null} game the table and seat @param {string | null} seat @param {{ hotseat?: boolean }} options */
  function reset(transport = null, game = null, seat = null, { hotseat = false } = {}) {
    clearTimers();
    context = transport ? { transport, game, seat, hotseat, available: false, shown: true, cooldownMs: 4000, coolingUntil: 0,
      sending: false, message: '', mine: null, theirs: null } : null;
    onChange();
  }

  function bubble(owner, side, reaction) {
    owner[side] = reaction;
    if (timers[side] !== null) cancel(timers[side]);
    timers[side] = reaction ? schedule(() => { timers[side] = null; if (owner === context) { owner[side] = null; onChange(); } }, BUBBLE_MS) : null;
  }

  function receive(view) {
    const owner = context;
    if (!owner) return;
    const reactions = view?.reactions;
    owner.available = Boolean(reactions?.available) && !owner.hotseat && !view?.replay;
    if (Number.isFinite(reactions?.cooldownMs)) owner.cooldownMs = reactions.cooldownMs;
    const recent = Array.isArray(reactions?.recent) ? reactions.recent : [];
    const seen = seenReaction(storage, owner.game);
    const unseen = recent.filter(one => Number(one?.sequence) > seen && one.seat !== owner.seat && reactionOf(one.reaction));
    const newest = Math.max(seen, ...recent.map(one => Number(one?.sequence) || 0));
    if (newest > seen) saveSeenReaction(storage, owner.game, newest);
    const latest = unseen.at(-1);
    if (latest && owner.available && owner.shown) bubble(owner, 'theirs', { ...reactionOf(latest.reaction), sequence: latest.sequence, ago: sentAgo(latest.at, now()) });
    if (!owner.available) { bubble(owner, 'theirs', null); bubble(owner, 'mine', null); }
    onChange();
  }

  /** Whether the opponent's reactions show in this game; hiding them clears the one on screen. */
  function setShown(shown) {
    if (!context) return;
    context.shown = shown !== false;
    if (!context.shown) bubble(context, 'theirs', null);
    onChange();
  }

  function cooling(owner, ms) {
    owner.coolingUntil = now() + Math.max(0, ms);
    if (timers.cooling !== null) cancel(timers.cooling);
    timers.cooling = schedule(() => { timers.cooling = null; emit(owner); }, Math.max(0, ms));
  }

  async function send(id) {
    const owner = context, reaction = reactionOf(id);
    if (!owner || !reaction || !owner.available || owner.sending || now() < owner.coolingUntil) return false;
    owner.sending = true; owner.message = '';
    // Shown at once: the tap is the reaction, and the host's answer only confirms it.
    bubble(owner, 'mine', { ...reaction, sequence: null, ago: '' });
    onChange();
    let answer = null;
    try { answer = await owner.transport.react(id); } catch { answer = null; }
    if (owner !== context) return false;
    owner.sending = false;
    if (answer?.ok) {
      cooling(owner, owner.cooldownMs);
      // What was shown moves on only with a view: the other player's reaction sent just before this one may not
      // have reached this page yet, and taking this one's number as read would skip it.
      onChange();
      return true;
    }
    bubble(owner, 'mine', null);
    if (answer?.status === 429) {
      cooling(owner, Number(answer.body?.retryAfterMs) || owner.cooldownMs);
      owner.message = 'Wait a moment before reacting again.';
    } else {
      if (answer?.status === 409) owner.available = false;
      owner.message = answer?.body?.message ?? 'The reaction was not sent. Try again.';
    }
    onChange();
    return false;
  }

  function model() {
    if (!context) return { available: false, shown: true, sending: false, coolingMs: 0, message: '', mine: null, theirs: null };
    return { available: context.available, shown: context.shown, sending: context.sending, coolingMs: Math.max(0, context.coolingUntil - now()),
      message: context.message, mine: context.mine, theirs: context.theirs };
  }

  return { reset, receive, send, setShown, model };
}
