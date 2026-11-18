import type { Primitive } from './kit';

// Glow, the cheap form (the craft book, "Glow" and "Additive light"): one soft dot, tinted, blended as light,
// that swells and goes. It costs one image, where the filter costs a framebuffer and a pass each frame.
export const glow: Primitive = (kit, play) => {
  const { beat, at, rate } = play;
  const lasts = beat.lasts! / rate, scale = at.radius * beat.reach! / 100 / 16;
  const light = kit.image(play, 'dot').setScale(scale * .6).setAlpha(0);
  kit.tween({ targets: light, alpha: beat.strength!, scaleX: scale, scaleY: scale, duration: lasts * .3, ease: 'Quad.Out',
    onComplete: () => kit.tween({ targets: light, alpha: 0, duration: lasts * .7, ease: 'Quad.In', onComplete: () => kit.release(light) }) });
};
