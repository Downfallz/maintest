// The cues of a played action, in one place: the scene's lunge and the moment the queue shows the impact have
// to agree, or a longer lunge lands after the hit it is meant to deliver. The laboratory times its sample on
// the same windup.

// Every motion below is played at half the pace it first had: what was 2× is now 1×, and 0.5× is a quarter of
// that first pace. Hard-coded flourishes in the scene multiply by the same factor so they keep in step.
export const MOTION_SCALE = 2;

// A draw back, then the strike: the windup is the two together.
export const DRAW_MS = 120 * MOTION_SCALE;
export const STRIKE_MS = 300 * MOTION_SCALE;
export const WINDUP_MS = DRAW_MS + STRIKE_MS;

// How long the frame after the action stays before it settles, and the pause before the next one.
export const IMPACT_MS = 850 * MOTION_SCALE;
export const SETTLE_MS = 250 * MOTION_SCALE;

// With less motion there is no lunge: the frame after the action is shown at once, for this long, at any speed.
export const REDUCED_MS = 650;

// Choreography stays within the impact receipt; renderer and queue use the same playback rate.
export const RETURN_MS = 360 * MOTION_SCALE;
export const RETURN_DELAY_MS = 60 * MOTION_SCALE;
export const VFX_MS = 600 * MOTION_SCALE;
export const playbackRate = value => [0.5, 1, 1.5, 2].includes(value) ? value : 1;
