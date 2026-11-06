export interface MutationLayer { id: string; family: string; stage: number; motif: string; slot: string; }
export interface Creature {
  id: string; name: string; form: string; health: number; maxHealth: number;
  energy: number; totalDefense: number; conditions: string[]; isAlive: boolean;
}
// The one action the lab offers, carried by the state so no module hardcodes who acts or who may be hit.
export interface SampleAction { actor: string; label: string; targets: readonly string[]; }
// The stages of the one sample, in the order they are reached; `windup` and `impact` are the cast in flight.
export type Stage = 'decision' | 'targeting' | 'windup' | 'impact' | 'resolved';
export interface LabState {
  stage: Stage; target: string | null; inspected: string; action: SampleAction;
  board: { allies: Creature[]; enemies: Creature[]; roundNumber: number; phase: string;
    timeline: { creature: string; owner: string; speed: string }[]; slot: string };
  changes: { creature: string; health?: number; energy?: number; defeated?: boolean }[];
}
export interface Placement { x: number; y: number; size: number; hitWidth: number; }
export interface SpellVisual { family: string; motif: string; delivery: string; }
export const creaturesOf = (state: LabState) => [...state.board.allies, ...state.board.enemies];
export const isEnemy = (state: LabState, id: string) => state.board.enemies.some(one => one.id === id);

// A render frame has presentation state only. The live adapter never imports the lab's sample or reducer.
export interface UpkeepMarker {
  id: string; name: string; side: string; lines: { text: string; tone: string }[];
}
export interface SceneFrame {
  upkeep?: { markers: UpkeepMarker[]; recorded: boolean; round: number };
  stage: Stage; target: string | null; actor: string | null; targets: readonly string[]; selected?: readonly string[];
  cue?: { kind: string; struck: string[]; effects: { creature: string; text: string; tone: string; wound: boolean }[];
    visual: SpellVisual; recipients: string[]; contact: string[];
    changes: { creature: number; health?: number; energy?: number; defeated?: boolean }[] };
  creatures: { id: string; form: string; layers?: MutationLayer[]; isAlive: boolean; enemy: boolean }[];
}
export const labSceneFrame = (state: LabState): SceneFrame => ({
  stage: state.stage, target: state.target, actor: state.action.actor, targets: state.action.targets,
  creatures: creaturesOf(state).map(one => ({ ...one, enemy: isEnemy(state, one.id) })),
});
