import type Phaser from 'phaser';
import { GROUND, placed, sized, type Primitive } from './kit';

// Gather (the craft book, "Gravity well and convergence"): particles start on a ring around the figure and land
// on it as the beat ends. Drawn inward they read as a charge or a drain. At the feet the ring lies on the ground.
export const gather: Primitive = (kit, play) => {
  const { beat, rate } = play, at = placed(play.at, beat.level), slant = beat.level === 'feet' ? GROUND : 1;
  const count = kit.room(beat.count!);
  if (!count) return;
  const life = beat.lasts! / rate, size = sized(beat, at.radius / 60, .35);
  const ring = new kit.engine.Geom.Circle(0, 0, at.radius * beat.reach! / 100);
  const emitter = kit.emitter({ ...play, at }, count, {
    emitting: false, lifespan: { min: life * .7, max: life },
    emitZone: { type: 'random', source: { getRandomPoint: point => { ring.getPoint(Math.random(), point as Phaser.Math.Vector2); point.y *= slant; } } },
    moveToX: 0, moveToY: 0,
    scale: { start: size, end: size * .3 },
    // A piece appears as it leaves the ring and goes as it lands: pieces that arrived whole piled into one pale disc.
    alpha: { onEmit: () => 0, onUpdate: (_particle, _key, t) => (t < .6 ? t / .6 : (1 - t) / .4) },
  });
  emitter.explode(count);
  kit.after(life, () => kit.release(emitter));
};
