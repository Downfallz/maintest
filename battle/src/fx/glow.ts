import { WAVE_RADIUS, type Primitive } from './kit';

// How flat the ground lies under the eye: a shock ring is drawn on it, as the `wave` and `roar` motifs draw theirs.
const GROUND = .42;

// Glow, the cheap form (the craft book, "Glow" and "Additive light"): one soft dot, tinted, blended as light,
// that swells and goes. It costs one image, where the filter costs a framebuffer and a pass each frame.
// With `shape: ring` the light is a shock ring: a thin ring lying on the ground under the figure, which opens
// to its reach over the whole beat and fades as it goes.
export const glow: Primitive = (kit, play) => {
  const { beat, at, rate } = play;
  const lasts = beat.lasts! / rate, reach = at.radius * beat.reach! / 100;
  if (beat.shape === 'ring') {
    const full = reach / WAVE_RADIUS;
    const ring = kit.image({ ...play, at: { ...at, y: at.y + at.radius * .6 } }, 'wave').setScale(full * .2, full * .2 * GROUND).setAlpha(beat.strength!);
    kit.tween({ targets: ring, scaleX: full, scaleY: full * GROUND, duration: lasts, ease: 'Cubic.Out' });
    kit.tween({ targets: ring, alpha: 0, duration: lasts, ease: 'Quad.In', onComplete: () => kit.release(ring) });
    return;
  }
  const scale = reach / 16;
  const light = kit.image(play, 'dot').setScale(scale * .6).setAlpha(0);
  kit.tween({ targets: light, alpha: beat.strength!, scaleX: scale, scaleY: scale, duration: lasts * .3, ease: 'Quad.Out',
    onComplete: () => kit.tween({ targets: light, alpha: 0, duration: lasts * .7, ease: 'Quad.In', onComplete: () => kit.release(light) }) });
};
