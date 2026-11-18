import type { Primitive } from './kit';

// The most the view moves, as a part of its size, at a strength of 1. A strength of .25 is the tremor the view
// had for a physical blow before the kit (0.0015 to 0.003).
const FULL = .01;

// Shake (the craft book, "Shake from trauma"): the view trembles and the tremor decays to nothing, squared, so
// a small one is barely felt. The scene's own clear resets the camera; the kit resets it too when it started it.
export const shake: Primitive = (kit, play) => {
  const { beat, rate } = play;
  const camera = kit.scene.cameras.main, peak = FULL * beat.strength!;
  camera.shake(beat.lasts! / rate, peak, true, (_camera: unknown, progress: number) => {
    const left = (1 - progress) ** 2;
    camera.shakeEffect.intensity.set(peak * left, peak * left);
  });
  const reset = kit.restore(() => camera.shakeEffect.reset());
  kit.after(beat.lasts! / rate, reset);
};
