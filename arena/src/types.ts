export interface Creature {
  id: string; name: string; form: string; health: number; maxHealth: number;
  energy: number; totalDefense: number; conditions: string[]; isAlive: boolean;
}
export interface LabState {
  stage: string; target: string | null; inspected: string;
  board: { allies: Creature[]; enemies: Creature[]; roundNumber: number; phase: string;
    timeline: { creature: string; owner: string; speed: string }[]; slot: string };
  changes: { creature: string; health?: number; energy?: number; defeated?: boolean }[];
}
export interface Placement { x: number; y: number; size: number; hitWidth: number; }
export const creaturesOf = (state: LabState) => [...state.board.allies, ...state.board.enemies];
export const isPlaying = (state: LabState) => ['windup', 'impact'].includes(state.stage);
