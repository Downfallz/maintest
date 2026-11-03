import { arenaFixture, sampleAction, sampleFrame } from './arena-lab-fixture.js';
import { combatChanges, combatSnapshot } from './combat-fx.js';

// The two stages during which a presentation is in flight: the only ones a skip can cut short.
const PLAYING = Object.freeze(['windup', 'impact']);

export function initialLabState() {
  return { stage: 'decision', target: null, inspected: sampleAction.actor, action: sampleAction, board: arenaFixture(), changes: [] };
}

// Whether a cast is being shown: between the confirmation and the resolved frame.
export function isLabPlaying(state) {
  return PLAYING.includes(state.stage);
}

// A presentation state machine. Only the lab's authored frames can change its displayed board, and the one
// action it offers travels in the state, so nothing downstream has to know which creature acts or which
// may be targeted.
export function labTransition(state, action) {
  switch (action.type) {
    case 'reset': return initialLabState();
    case 'inspect':
      return [...state.board.allies, ...state.board.enemies].some(one => one.id === action.id)
        ? { ...state, inspected: action.id } : state;
    case 'choose':
      return state.stage === 'decision' ? { ...state, stage: 'targeting' } : state;
    case 'target':
      return state.stage === 'targeting' && state.action.targets.includes(action.id)
        ? { ...state, target: action.id, inspected: action.id } : state;
    case 'cancel':
      return state.stage === 'targeting' ? { ...state, stage: 'decision', target: null } : state;
    case 'play':
      return state.stage === 'targeting' && state.target
        ? { ...state, stage: 'windup' } : state;
    case 'impact': {
      if (state.stage !== 'windup') return state;
      const frame = sampleFrame(state.target);
      return { ...state, stage: 'impact', board: frame.after,
        changes: combatChanges(combatSnapshot(frame.before), combatSnapshot(frame.after)) };
    }
    case 'finish':
      return state.stage === 'impact' ? { ...state, stage: 'resolved' } : state;
    case 'skip': {
      if (!isLabPlaying(state)) return state;
      const applied = state.stage === 'windup' ? labTransition(state, { type: 'impact' }) : state;
      return { ...applied, stage: 'resolved' };
    }
    default: return state;
  }
}

// The sample's pacing: the impact lands, then the frame settles. Halved at 2×; any other speed plays at 1×.
const CUES = Object.freeze([[420, 'impact'], [1250, 'finish']]);

// Reset/skip/unmount invalidate pending work, even if an old callback has already been queued.
export function createLabPlayback(schedule = setTimeout, unschedule = clearTimeout) {
  let generation = 0;
  let timers = [];
  function cancel() {
    generation++;
    timers.forEach(unschedule);
    timers = [];
  }
  return {
    cancel,
    play(emit, { reducedMotion = false, speed = 1 } = {}) {
      cancel();
      const current = generation;
      if (reducedMotion) {
        emit({ type: 'impact' });
        emit({ type: 'finish' });
        return;
      }
      const rate = speed === 2 ? 2 : 1;
      for (const [delay, type] of CUES) {
        timers.push(schedule(() => { if (current === generation) emit({ type }); }, delay / rate));
      }
    },
  };
}
