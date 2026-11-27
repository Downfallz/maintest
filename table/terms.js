// The player-facing words for values the host serves under their domain names (docs/domain/player-terminology.md).
//
// The host and every contract keep `Quick`, `Standard`, `StartOfRound`, `Planning`... ; the page shows these
// labels instead. This is the one place they are spelled: everything else asks here.

const TEMPO = { Quick: 'Instinct', Standard: 'Precision' };

const PHASE = { StartOfRound: 'Round start', Planning: 'Command', Combat: 'Combat', EndOfRound: 'Round end' };

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

// The round's two automatic moments, as a label reads them alone: the journal's categories and a receipt's title.
export const ROUND_START = PHASE.StartOfRound;
export const ROUND_END = PHASE.EndOfRound;

// A label beside its round's number. A moment drops its "Round" there: `Round 3 · Start`, never `Round 3 · Round start`.
const BESIDE_A_ROUND = { [ROUND_START]: 'Start', [ROUND_END]: 'End' };
export function besideRound(label) {
  return BESIDE_A_ROUND[label] ?? label;
}
export function inRound(round, label) {
  return `Round ${round ?? '—'} · ${besideRound(label)}`;
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

// A creature definition's name as a player reads it. The one base creature is named Main in the content; a
// player reads it as a creature, which is also how the event log names one (Creature 4).
const CREATURE = { Main: 'Creature' };
export function creatureName(name) {
  return CREATURE[name] ?? name ?? '';
}

// A Tier's level as a player reads it: in Roman numerals (docs/domain/player-terminology.md). A level without
// one (none, or past the four the families hold) reads as written, so a page never shows an empty Stage.
const NUMERALS = ['', 'I', 'II', 'III', 'IV'];
export function stageNumeral(level) {
  return NUMERALS[level] || String(level ?? '?');
}

export function stageLabel(level) {
  return `Stage ${stageNumeral(level)}`;
}

// A Stage as a screen reader speaks it: with its number, since "Stage I" may be read as the pronoun I (#769).
// What is shown keeps the numeral; an announcement or an aria-label says this. No level is said in words.
export function stageSpoken(level) {
  return level == null ? 'Stage unavailable' : `Stage ${level}`;
}

// The four Stages a family climbs, in order, as the Mutation Tree's help reads them.
export function stageClimb() {
  return [1, 2, 3, 4].map(stageLabel).join(' → ');
}

// The mark of a place a tie settled at random gave, in the order of play (ADR 0208).
export const TIE_MARK = 'tie';

// The one-line rule every tempo hint repeats.
export const TEMPO_RULE = 'Instinct acts before Precision; only Precision can crit.';
