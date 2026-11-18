// Whether the step a journey answered is behind the seat, read from the seat's latest view.
// A team's tempos are one asking per creature, which the page sends one after another from a single sheet
// (table/battle-tempo.js), and between two of them the host serves a view that asks nothing. So a tempo step
// ends only once the round has left its tempos; any other step ends once the seat is asked something else (#787).
export function stepAnswered(view, latest) {
  if (!latest) return false;
  if (latest.over) return true;
  if (view.waitingFor !== 'Speed') return latest.waitingAsked !== view.waitingAsked;
  return latest.board.roundNumber !== view.board.roundNumber || (latest.waitingFor != null && latest.waitingFor !== 'Speed');
}
