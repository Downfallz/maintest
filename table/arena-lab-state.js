import { arenaFixture, sampleAction, sampleFrame } from './arena-lab-fixture.js';
import { combatChanges, combatSnapshot } from './combat-fx.js';

export function initialLabState() {
  return { stage: 'decision', target: null, inspected: sampleAction.actor, board: arenaFixture(), changes: [] };
}

// A presentation state machine. Only the lab's authored frames can change its displayed board.
export function labTransition(state, action) {
  switch (action.type) {
    case 'reset': return initialLabState();
    case 'inspect':
      return [...state.board.allies, ...state.board.enemies].some(one => one.id === action.id)
        ? { ...state, inspected: action.id } : state;
    case 'choose':
      return state.stage === 'decision' ? { ...state, stage: 'targeting' } : state;
    case 'target':
      return state.stage === 'targeting' && sampleAction.targets.includes(action.id)
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
      if (!['windup', 'impact'].includes(state.stage)) return state;
      const applied = state.stage === 'windup' ? labTransition(state, { type: 'impact' }) : state;
      return { ...applied, stage: 'resolved' };
    }
    default: return state;
  }
}

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
      for (const [delay, type] of [[420, 'impact'], [1250, 'finish']]) {
        timers.push(schedule(() => { if (current === generation) emit({ type }); }, delay / rate));
      }
    },
  };
}
