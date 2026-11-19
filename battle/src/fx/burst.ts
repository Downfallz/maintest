import type Phaser from 'phaser';
import { headingOf, sized, type Primitive } from './kit';

type Flying = Phaser.GameObjects.Particles.Particle & { fxX: number; fxY: number; fxFall: number };

// Burst (the craft book, "Burst"): many particles at once from one point, each on its own heading inside a fan,
// fading as it goes. The impact's debris. A piece leaves fast and slows, as a thrown thing does: it is clear of
// the figure within a few frames, where a steady pace left it over the body for most of its life. Then it falls
// a little, since debris has weight.
export const burst: Primitive = (kit, play) => {
  const { beat, at, rate } = play;
  const count = kit.room(beat.count!);
  if (!count) return;
  const life = beat.lasts! / rate, reach = at.radius * beat.reach! / 100;
  const centre = headingOf(beat.direction, play.heading), fan = beat.spread! * Math.PI / 180, size = sized(beat, at.radius / 34, .5);
  const out = (t: number) => 1 - (1 - t) ** 3;
  const emitter = kit.emitter(play, count, {
    emitting: false, lifespan: { min: life * .6, max: life }, speed: 0,
    // The engine asks for a particle's place first, then its turn: the heading drawn here serves both, so a
    // spark points where it flies.
    x: { onEmit: particle => {
      const piece = particle as Flying, heading = centre + (Math.random() - .5) * fan, far = reach * (.55 + Math.random() * .45);
      piece.fxX = Math.cos(heading) * far; piece.fxY = Math.sin(heading) * far; piece.fxFall = reach * .18;
      piece.angle = heading * 180 / Math.PI;
      return 0;
    }, onUpdate: (particle, _key, t) => (particle as Flying).fxX * out(t) },
    y: { onEmit: () => 0, onUpdate: (particle, _key, t) => (particle as Flying).fxY * out(t) + (particle as Flying).fxFall * t * t },
    rotate: { onEmit: particle => particle!.angle },
    scale: { start: size, end: size * .4 }, // The pieces start stacked on one point: they come in over their first tenth, so the stack is never one solid
    // disc on the figure, then fade as they slow.
    alpha: { onEmit: () => 0, onUpdate: (_particle, _key, t) => Math.min(1, t / .1) * (1 - t ** 3) },
  });
  emitter.explode(count);
  kit.after(life, () => kit.release(emitter));
};
