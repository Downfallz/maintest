// The player-facing words for values the host serves under their domain names (docs/domain/player-terminology.md).
//
// The host and every contract keep `Quick`, `Standard`, `StartOfRound`, `Planning`... ; the page shows these
// labels instead. This is the one place they are spelled: everything else asks here.

const TEMPO = { Quick: 'Instinct', Standard: 'Precision' };

const PHASE = { StartOfRound: 'Upkeep', Planning: 'Command', Combat: 'Combat', EndOfRound: 'Aftermath' };

// The internal speed values, in the order the engine plays them: what a tempo chooser sends.
export const SPEEDS = Object.freeze(Object.keys(TEMPO));

// A served speed as a player reads it; an unknown value keeps its own word rather than vanishing.
export function tempoLabel(speed) {
  return TEMPO[speed] ?? speed ?? '';
}

// A served round phase as a player reads it.
export function phaseLabel(phase) {
  return PHASE[phase] ?? phase ?? '';
}

// A served decision kind (`waitingFor`) as a player reads it: only the speed question is renamed.
export function decisionLabel(kind) {
  return kind === 'Speed' ? 'Tempo' : kind ?? '';
}

// A served sub-phase as one short label: its words spaced, and the speed step read as the tempo.
export function stepLabel(subPhase) {
  if (subPhase === 'Speed') return 'Tempo';
  return (subPhase ?? '').replace(/([a-z])([A-Z])/g, '$1 $2');
}

// A Tier's level as a player reads it.
export function stageLabel(level) {
  return `Stage ${level}`;
}

// The one-line rule every tempo hint repeats.
export const TEMPO_RULE = 'Instinct acts before Precision; only Precision can crit.';
