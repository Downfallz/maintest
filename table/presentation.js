// The read-only boundary between a seat controller and a renderer (ADR 0123, A3).
// Inputs are already seat-filtered by the host. No transport, tokens, DOM, catalogue content or lab fixtures
// belong here. Returned snapshots borrow the host's objects: consumers must treat them as immutable.
import { askedKey, slotView } from './slot.js';
import { playbackBoard } from './replay.js';
import { cursorOf, rollText, side, withCursor } from './timeline.js';

// The current legal question, adapted for a local spell choice. Choosing or going back is a redraw, never a
// fetch. A choice belongs to the seat AND the host's asking, regardless of which Action screen is displayed.
// The choice's three fields are optional and compared as they come: a default of null would type them as
// null for the analyser, which then reads every comparison with a key as always false.
export function decisionView(seat, latest, { asked, chosen, answered } = {}) {
  if (latest?.waitingFor === 'Action' && `${seat}/${latest.waitingAsked}` === answered) {
    // The host accepted the command but the next poll has not arrived. Do not offer that action twice.
    return { ...latest, waitingFor: null, waitingCreature: null };
  }
  return slotView(latest, askedKey(seat, latest) === asked ? chosen : null);
}

// Displaying a recorded frame never replaces the current legal question. Controllers keep `latest` for
// decisions and acknowledge it only once it is visible, outside playback and the hotseat fence.
//
// What goes is the question itself: what is asked and what may be answered. Who is asked stays, because the
// person reading the frame is still that creature's player, and the spellbook, its guidance and the Mutation
// Tree follow that creature while they read rather than jumping to the first ally.
export function presentedView(latest, replay = null, entries = null) {
  // A replay pointing past its actions has nothing to show: the current view, rather than a page that cannot draw.
  const action = replay?.actions[replay.index];
  if (!action) return latest;
  return {
    ...latest,
    board: playbackBoard(replay, latest.board),
    waitingFor: null, options: {},
    roundEvents: (entries ?? latest.roundEvents ?? []).filter(entry => entry.sequence < action.sequence
      || (replay.stage === 'after' && entry.sequence === action.sequence)),
  };
}

// Display facts shared by the table and the future live battle. Preserve the host's team/slot order and all
// creature fields (including dead creatures and conditions); never rebuild stats or read private choices.
export function boardPresentation(board) {
  const cursor = cursorOf(board);
  const timeline = withCursor(board?.timeline, cursor).map((slot, index) => ({
    ...slot, position: index + 1, side: side(slot, board?.slot), roll: rollText(board?.rollOffs, slot.creature),
  }));
  return {
    allies: board?.allies ?? [],
    enemies: board?.enemies ?? [],
    round: board?.roundNumber,
    phase: board?.phase,
    subPhase: board?.subPhase,
    cursor,
    timeline,
    turn: activeTurn(board, cursor),
  };
}

// The slot the round is on, during Activation only (timeline.js): never one inferred while planning or cleaning up.
export function activeTurn(board, cursor = cursorOf(board)) {
  const slot = board?.timeline?.[cursor];
  return slot ? { slot, position: cursor + 1, total: board.timeline.length } : null;
}
