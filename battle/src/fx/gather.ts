import type Phaser from 'phaser';
import { GROUND, placed, sized, type Primitive } from './kit';

type Landing = Phaser.GameObjects.Particles.Particle & { fxX: number; fxY: number };

/**
 * Where a gather's pieces land, as a share of the figure's radius (*house*): on its outline, each on the side it
 * came from. Landed on the centre, hooks sank into the body and a swallow into the motif: a piece showed only on
 * its way in (#942).
 */
const LAND = .6;

// Gather (the craft book, "Gravity well and convergence"): particles start on a ring around the figure and close
// on its outline as the beat ends. Drawn inward they read as a charge or a drain. At the feet the ring lies on the ground.
export const gather: Primitive = (kit, play) => {
  const { beat, rate } = play, at = placed(play.at, beat.level), slant = beat.level === 'feet' ? GROUND : 1;
  const count = kit.room(beat.count!);
  if (!count) return;
  const life = beat.lasts! / rate, size = sized(beat, at.radius / 60, .35);
  const far = at.radius * beat.reach! / 100, ring = new kit.engine.Geom.Circle(0, 0, far);
  // How much of the way in a piece goes: from the ring to the outline. A ring drawn inside the outline closes to its centre.
  const travel = far > at.radius * LAND ? 1 - at.radius * LAND / far : 1;
  const emitter = kit.emitter({ ...play, at }, count, {
    emitting: false, lifespan: { min: life * .7, max: life }, speed: 0,
    emitZone: { type: 'random', source: { getRandomPoint: point => { ring.getPoint(Math.random(), point as Phaser.Math.Vector2); point.y *= slant; } } },
    // The engine has placed the piece on the ring before it asks for anything: where it starts is where it is now.
    x: { onEmit: particle => { (particle as Landing).fxX = particle!.x; return 0; }, onUpdate: (particle, _key, t) => (particle as Landing).fxX * (1 - travel * t) },
    y: { onEmit: particle => { (particle as Landing).fxY = particle!.y; return 0; }, onUpdate: (particle, _key, t) => (particle as Landing).fxY * (1 - travel * t) },
    scale: { start: size, end: size * .3 },
    // A piece appears as it leaves the ring and goes as it lands: pieces that arrived whole piled into one pale disc.
    alpha: { onEmit: () => 0, onUpdate: (_particle, _key, t) => (t < .6 ? t / .6 : (1 - t) / .4) },
  });
  emitter.explode(count);
  kit.after(life, () => kit.release(emitter));
};
