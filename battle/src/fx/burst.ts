import type Phaser from 'phaser';
import { GROUND, grounded, headingOf, placed, sized, type Primitive } from './kit';

type Flying = Phaser.GameObjects.Particles.Particle & { fxX: number; fxY: number; fxFall: number };

// Burst (the craft book, "Burst"): many particles at once from one point, each on its own heading inside a fan,
// fading as it goes. The impact's debris. A piece leaves fast and slows, as a thrown thing does: it is clear of
// the figure within a few frames, where a steady pace left it over the body for most of its life. Then it falls
// a little, since debris has weight. On the ground (the craft book, "A beat on the ground") a piece's travel
// toward or away from the eye is shortened, and one thrown along it from the feet stays on it and does not fall.
// Aimed `down`, the pieces drop off the figure and fall as far again: what a body sheds, not what a blow throws.
export const burst: Primitive = (kit, play) => {
  const { beat, rate } = play, at = placed(play.at, beat.level);
  const count = kit.room(beat.count!);
  if (!count) return;
  const life = beat.lasts! / rate, reach = at.radius * beat.reach! / 100;
  const centre = headingOf(beat.direction, play.heading), fan = beat.spread! * Math.PI / 180, size = sized(beat, at.radius / 34, .5);
  const out = (t: number) => 1 - (1 - t) ** 3;
  const slant = grounded(beat) ? GROUND : 1;
  let fall = beat.level === 'feet' && beat.direction !== 'up' ? 0 : reach * .18;
  if (beat.direction === 'down') fall = reach * .6;
  // Each piece's heading is drawn where the engine places it, which it does before it asks any of the piece's
  // values: its place and its turn then read the same heading, whatever order the engine asks them in (#936).
  let drawn = { x: 0, y: 0, angle: 0 };
  const emitter = kit.emitter({ ...play, at }, count, {
    emitting: false, lifespan: { min: life * .6, max: life }, speed: 0,
    emitZone: { type: 'random', source: { getRandomPoint: point => {
      const heading = centre + (Math.random() - .5) * fan, far = reach * (.55 + Math.random() * .45);
      const x = Math.cos(heading) * far, y = Math.sin(heading) * far * slant;
      drawn = { x, y, angle: Math.atan2(y, x) * 180 / Math.PI };
      point.x = 0; point.y = 0;
    } } },
    x: { onEmit: particle => {
      const piece = particle as Flying;
      piece.fxX = drawn.x; piece.fxY = drawn.y; piece.fxFall = fall;
      return 0;
    }, onUpdate: (particle, _key, t) => (particle as Flying).fxX * out(t) },
    y: { onEmit: () => 0, onUpdate: (particle, _key, t) => (particle as Flying).fxY * out(t) + (particle as Flying).fxFall * t * t },
    rotate: { onEmit: () => drawn.angle },
    scale: { start: size, end: size * .4 }, // The pieces start stacked on one point: they come in over their first tenth, so the stack is never one solid
    // disc on the figure, then fade as they slow.
    alpha: { onEmit: () => 0, onUpdate: (_particle, _key, t) => Math.min(1, t / .1) * (1 - t ** 3) },
  });
  emitter.explode(count);
  kit.after(life, () => kit.release(emitter));
};
