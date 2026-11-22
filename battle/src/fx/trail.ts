import { flightAt } from '../../../table/battle-motion.js';
import { placed, sized, type Primitive } from './kit';

// Trail (the craft book, "Trail"): particles left behind a head that travels from the caster to a target. They
// stay where they were left, so they draw the path. The head carries the meaning; the trail is dimmer and smaller.
// Every head goes at an even pace and its pieces live 240 ms, whatever the beat lasts: a head that eased in crossed
// the field in its last frames, and pieces that lived half a short beat were gone before a film could show a line
// between a caster and its ally (#918, #947). At the feet of both figures the path runs along the ground: a crack
// from the caster to its target, which stays open for the whole beat. Behind a projectile the head is the quill's
// own place at each moment (`flightAt`): the same arc, so the trail leaves the caster with the quill and never runs
// ahead of it (#909). A trail leaves its caster where the caster stands when it starts (#933).
const PIECE_MS = 240;
export const trail: Primitive = (kit, play) => {
  const { beat, rate } = play, at = placed(play.at, beat.level);
  const cap = kit.room(beat.count!);
  if (!cap || !play.from) return;
  // The quill leaves from its caster's place, as the scene draws it: only a trail that is no quill's moves its start.
  const thrown = play.flight === true, moved = (!thrown && play.fromDrift?.()) || { x: 0, y: 0 };
  const origin = { ...play.from, x: play.from.x + moved.x, y: play.from.y + moved.y };
  const from = thrown ? flightAt(origin, at, 0) : placed(origin, beat.level);
  const crack = beat.level === 'feet' && !thrown, travel = beat.lasts! / rate;
  const life = crack ? travel : PIECE_MS / rate, size = sized(beat, at.radius / 110, .2);
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
    kit.tween({ targets: head, x: at.x, y: at.y, duration: travel, ease: 'Linear', onComplete: () => emitter.stop() });
  }
  kit.after(travel + life, () => kit.release(emitter));
};
