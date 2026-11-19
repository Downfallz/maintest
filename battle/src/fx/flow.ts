import { degrees, headingOf, sized, type Primitive } from './kit';

// Flow (the craft book, "Flow"): a steady stream for as long as the beat lasts, a few particles at a time,
// from all over the figure. Embers above a fire, a mending rising from a body.
export const flow: Primitive = (kit, play) => {
  const { beat, at, rate } = play;
  const cap = kit.room(beat.count!);
  if (!cap) return;
  const lasts = beat.lasts! / rate, life = Math.min(400 / rate, lasts / 2);
  const centre = degrees(headingOf(beat.direction, play.heading)), half = beat.spread! / 2;
  const reach = at.radius * beat.reach! / 100, size = sized(beat, at.radius / 70, .3);
  const emitter = kit.emitter(play, cap, {
    lifespan: life, frequency: life / cap, quantity: 1,
    x: { min: -at.radius * .6, max: at.radius * .6 }, y: { min: -at.radius * .4, max: at.radius * .4 },
    angle: { min: centre - half, max: centre + half }, speed: { min: reach / life * 500, max: reach / life * 1000 },
    scale: { start: size, end: 0 }, alpha: { start: .9, end: 0 },
  });
  // The stream stops one life before the beat ends, so the last particle dies inside the beat.
  kit.after(lasts - life, () => emitter.stop());
  kit.after(lasts, () => kit.release(emitter));
};
