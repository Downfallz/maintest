// What a seat decided, as the page sends it to a seat's /decision, without how long the asking was drawn
// before the tap (`elapsedMs`, inventory question 3). That measure is the run's own timing, so a journey
// compares the decision itself and reads the measure apart, with `drawnFor`.
export function decided(request) {
  const { elapsedMs: _drawnFor, ...decision } = request.postDataJSON() ?? {};
  return decision;
}

// How long the page says the asking was drawn before the tap, in milliseconds; undefined when it sent none.
export const drawnFor = request => request.postDataJSON()?.elapsedMs;
