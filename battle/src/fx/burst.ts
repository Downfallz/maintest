import { degrees, headingOf, type Primitive } from './kit';

// Burst (the craft book, "Burst"): many particles at once from one point, each on its own heading inside a fan,
// fading as it goes. The impact's debris.
export const burst: Primitive = (kit, play) => {
  const { beat, at, rate } = play;
  const count = kit.room(beat.count!);
  if (!count) return;
  const life = beat.lasts! / rate, reach = at.radius * beat.reach! / 100;
  const centre = degrees(headingOf(beat.direction, play.heading)), size = Math.max(.45, at.radius / 45);
  // The engine turns a particle before it gives it a heading: one draw serves both, so a spark points where it flies.
  let heading = centre;
  const emitter = kit.emitter(play, count, {
    emitting: false, lifespan: { min: life * .6, max: life },
    rotate: { onEmit: () => (heading = centre + (Math.random() - .5) * beat.spread!) },
    angle: { onEmit: () => heading },
    speed: { min: reach / life * 600, max: reach / life * 1000 },
    scale: { start: size, end: size * .35 }, alpha: { start: 1, end: 0, ease: 'Quad.In' },
  });
  emitter.explode(count);
  kit.after(life, () => kit.release(emitter));
};
