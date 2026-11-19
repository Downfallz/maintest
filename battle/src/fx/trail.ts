import { flightAt } from '../../../table/battle-motion.js';
import { placed, sized, type Primitive } from './kit';

// Trail (the craft book, "Trail"): particles left behind a head that travels from the caster to a target. They
// stay where they were left, so they draw the path. The head carries the meaning; the trail is dimmer and smaller.
// At the feet of both figures the path runs along the ground: a crack from the caster to its target. A crack
// opens at an even pace and stays open for the whole beat: a head that eased in crossed the field in its last
// frame, and pieces that lived 220 ms were gone before a film could show a line (#918). Behind a projectile the
// head is the quill's own place at each moment (`flightAt`): the same arc at the same even pace, so the trail
// leaves the caster with the quill and never runs ahead of it (#909).
export const trail: Primitive = (kit, play) => {
  const { beat, rate } = play, at = placed(play.at, beat.level);
  const cap = kit.room(beat.count!);
  if (!cap || !play.from) return;
  const thrown = play.flight === true, origin = play.from;
  const from = thrown ? flightAt(origin, at, 0) : placed(origin, beat.level);
  const crack = beat.level === 'feet' && !thrown, travel = beat.lasts! / rate;
  const life = crack ? travel : Math.min(220 / rate, travel / 2), size = sized(beat, at.radius / 110, .2);
  const head = { x: from.x, y: from.y };
  const emitter = kit.emitter({ ...play, at: { ...at, x: 0, y: 0 } }, cap, {
    lifespan: life, frequency: life / cap, quantity: 1, speed: 0,
    scale: { start: size, end: 0 }, alpha: { start: .7, end: 0 },
  });
  emitter.startFollow(head);
  // The head travels for the whole beat. What it left fades for one life more, or until the window changes.
  if (thrown) {
    const along = { share: 0 };
    kit.tween({ targets: along, share: 1, duration: travel, ease: 'Linear', onComplete: () => emitter.stop(),
      onUpdate: () => { const now = flightAt(origin, play.at, along.share); head.x = now.x; head.y = now.y; } });
  } else {
    // A trail that is no projectile's eases in, as a thing that gathers speed; a crack opens at an even pace.
    kit.tween({ targets: head, x: at.x, y: at.y, duration: travel, ease: crack ? 'Linear' : 'Cubic.In', onComplete: () => emitter.stop() });
  }
  kit.after(travel + life, () => kit.release(emitter));
};
