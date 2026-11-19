import type Phaser from 'phaser';
import { GROUND, WAVE_RADIUS, degrees, headingOf, onGround, placed, type BeatPlay, type KitContext, type Primitive } from './kit';

// Glow, the cheap form (the craft book, "Glow" and "Additive light"): one soft dot, tinted, blended as light,
// that swells and goes. It costs one image, where the filter costs a framebuffer and a pass each frame.
// With `shape: ring` the light is a shock ring: a thin ring lying on the ground at the figure's feet, which opens
// to its reach over the whole beat and fades as it goes. Aimed along the blow or back against it, the ring is an
// arc on that side only (the craft book, "A beat on the ground"): it does not sweep through a neighbour.
export const glow: Primitive = (kit, play) => {
  const { beat, rate } = play;
  const lasts = beat.lasts! / rate, reach = play.at.radius * beat.reach! / 100;
  if (beat.shape === 'ring') {
    const ground = placed(play.at, 'feet');
    // A ring aimed at nobody, a cast on its own caster, has no side to open on: it opens all around.
    if ((beat.direction === 'along' || beat.direction === 'back') && play.heading !== null) { arc(kit, { ...play, at: ground }, lasts, reach); return; }
    const full = reach / WAVE_RADIUS;
    const ring = kit.image({ ...play, at: ground }, 'wave').setScale(full * .2, full * .2 * GROUND).setAlpha(beat.strength!);
    kit.tween({ targets: ring, scaleX: full, scaleY: full * GROUND, duration: lasts, ease: 'Cubic.Out' });
    kit.tween({ targets: ring, alpha: 0, duration: lasts, ease: 'Quad.In', onComplete: () => kit.release(ring) });
    return;
  }
  const at = placed(play.at, beat.level), scale = reach / 16;
  const light = kit.image({ ...play, at }, 'dot').setScale(scale * .6).setAlpha(0);
  kit.tween({ targets: light, alpha: beat.strength!, scaleX: scale, scaleY: scale, duration: lasts * .3, ease: 'Quad.Out',
    onComplete: () => kit.tween({ targets: light, alpha: 0, duration: lasts * .7, ease: 'Quad.In', onComplete: () => kit.release(light) }) });
};

// The ring's arc, drawn again at each step as it opens: the wave's two strokes, a wide faint one under a thin full one.
const POINTS = 20;
function arc(kit: KitContext, play: BeatPlay, lasts: number, reach: number) {
  const { beat, color } = play;
  const heading = headingOf(beat.direction, play.heading), middle = onGround(heading), half = beat.spread! * Math.PI / 360;
  const shape = kit.graphics(play);
  // Where its middle is drawn, on the screen: what the browser spec checks against the blow.
  shape.setData('facing', degrees(Math.atan2(Math.sin(middle) * GROUND, Math.cos(middle))));
  const opening = { radius: reach * .2, alpha: beat.strength! };
  const draw = () => {
    if (!shape.active) return;
    const points = Array.from({ length: POINTS + 1 }, (_, index) => {
      const angle = middle - half + 2 * half * index / POINTS;
      return { x: Math.cos(angle) * opening.radius, y: Math.sin(angle) * opening.radius * GROUND };
    });
    shape.clear();
    for (const [width, alpha] of [[9, .3], [3.5, 1]]) shape.lineStyle(width, color, alpha * opening.alpha).strokePoints(points as Phaser.Math.Vector2[]);
  };
  draw();
  kit.tween({ targets: opening, radius: reach, duration: lasts, ease: 'Cubic.Out', onUpdate: draw });
  kit.tween({ targets: opening, alpha: 0, duration: lasts, ease: 'Quad.In', onComplete: () => kit.release(shape) });
}
