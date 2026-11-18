import type Phaser from 'phaser';
import type { Primitive } from './kit';

// Gather (the craft book, "Gravity well and convergence"): particles start on a ring around the figure and land
// on it as the beat ends. Drawn inward they read as a charge or a drain.
export const gather: Primitive = (kit, play) => {
  const { beat, at, rate } = play;
  const count = kit.room(beat.count!);
  if (!count) return;
  const life = beat.lasts! / rate, size = Math.max(.25, at.radius / 90);
  const ring = new kit.engine.Geom.Circle(0, 0, at.radius * beat.reach! / 100);
  const emitter = kit.emitter(play, count, {
    emitting: false, lifespan: { min: life * .7, max: life },
    emitZone: { type: 'random', source: { getRandomPoint: point => { ring.getPoint(Math.random(), point as Phaser.Math.Vector2); } } },
    moveToX: 0, moveToY: 0,
    scale: { start: size, end: size * .3 }, alpha: { start: 0, end: 1, ease: 'Quad.Out' },
  });
  emitter.explode(count);
  kit.after(life, () => kit.release(emitter));
};
