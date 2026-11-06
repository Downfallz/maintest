// The cues of a played action, in one place: the scene's lunge and the moment the queue shows the impact have
// to agree, or a longer lunge lands after the hit it is meant to deliver. The laboratory times its sample on
// the same windup.

// A draw back, then the strike: the windup is the two together.
export const DRAW_MS = 120;
export const STRIKE_MS = 300;
export const WINDUP_MS = DRAW_MS + STRIKE_MS;

// How long the frame after the action stays before it settles, and the pause before the next one.
export const IMPACT_MS = 850;
export const SETTLE_MS = 250;

// With less motion there is no lunge: the frame after the action is shown at once, for this long.
export const REDUCED_MS = 650;

// Choreography stays within the impact receipt; renderer and queue use the same playback rate.
export const RETURN_MS = 360;
export const RETURN_DELAY_MS = 60;
export const VFX_MS = 600;
export const playbackRate = value => [1, 1.5, 2].includes(value) ? value : 1;
