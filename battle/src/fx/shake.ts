import type { Primitive } from './kit';

// Shake (the craft book, "Shake from trauma"): the beat adds its strength to the view's one trauma value, which
// the scene keeps, decays and lays over the camera. So a staged tremor and the tremor of the health lost add up
// under one ceiling, and the scene's cancel path ends both. The kit starts no camera effect of its own.
export const shake: Primitive = (kit, play) => { kit.view?.shake(play.beat.strength!); };
