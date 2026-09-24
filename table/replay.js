// Historical presentation only: every stat comes from the engine's action boundary snapshots.
export function playbackBoard(replay, board) {
  const action = replay?.actions[replay.index];
  if (!action?.frame) return board;
  const creatures = action.frame[replay.stage];
  const allies = new Set((board.allies ?? []).map(creature => creature.id));
  return {
    ...board,
    roundNumber: replay.round,
    phase: 'Combat', subPhase: 'ActionResolution',
    allies: creatures.filter(creature => allies.has(creature.id)),
    enemies: creatures.filter(creature => !allies.has(creature.id)),
    timeline: action.frame.timeline, rollOffs: action.frame.rollOffs,
    resolveCursor: replay.index, revealCursor: action.frame.timeline.length,
    revealedActions: replay.actions.map(item => item.action).filter(Boolean),
    intents: [], speedChoices: [], evolutionChoices: [], outcome: null,
  };
}

export function playbackChanges(action, creature) {
  const before = action?.frame?.before.find(one => one.id === creature.id);
  if (!before) return [];
  return [['health', 'HP'], ['energy', 'Energy'], ['totalDefense', 'Defense'], ['currentInitiative', 'Initiative']]
    .filter(([key]) => Number.isFinite(before[key]) && Number.isFinite(creature[key]) && before[key] !== creature[key])
    .map(([key, label]) => ({
      text: `${label} ${before[key]} → ${creature[key]}`,
      tone: creature[key] < before[key] ? 'harm' : 'recovery',
    }));
}
